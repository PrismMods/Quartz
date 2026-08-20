using System.Runtime.InteropServices;
using Quartz.Core;
namespace Quartz.Features.Discord;
public sealed class DaveSession : IDisposable {
    private static readonly DaveNative.MlsFailure FailureCallback = OnMlsFailure;
    private readonly Dictionary<string, IntPtr> decryptors = [];
    private readonly Dictionary<string, IntPtr> ratchets = [];
    private readonly Dictionary<uint, string> ssrcToUser = [];
    private readonly List<string> roster = [];
    private readonly string selfUserId;
    private readonly uint selfSsrc;
    private IntPtr session;
    private IntPtr encryptor;
    private bool disposed;
    private long decryptOk;
    public ushort Version { get; private set; }
    public bool KeysReady { get; private set; }
    public bool Ready => session != IntPtr.Zero;
    public int RosterCount => roster.Count;
    public DaveSession(ushort version, ulong groupId, string userId, uint ssrc) {
        Version = version;
        selfUserId = userId;
        selfSsrc = ssrc;
        session = DaveNative.SessionCreate(null, FailureCallback);
        if(session == IntPtr.Zero) {
            MainCore.Log.Wrn("[Discord] daveSessionCreate returned null");
            return;
        }
        DaveNative.SessionInit(session, version, groupId, userId);
        encryptor = DaveNative.EncryptorCreate();
        if(encryptor != IntPtr.Zero) {
            DaveNative.EncryptorAssignCodec(encryptor, selfSsrc, DaveNative.Codec.Opus);
            DaveNative.EncryptorPassthrough(encryptor, true);
        }
        MainCore.Log.Msg($"[Discord] dave session init v{version} group={groupId} self={userId} ssrc={ssrc}");
    }
    private static void OnMlsFailure(IntPtr source, IntPtr reason, IntPtr userData) =>
        MainCore.Log.Wrn(
            "[Discord] dave MLS failure ["
            + (Marshal.PtrToStringAnsi(source) ?? "?") + "]: " + (Marshal.PtrToStringAnsi(reason) ?? "?"));
    public void SetExternalSender(byte[] payload) {
        if(disposed || !Ready || payload == null) return;
        DaveNative.SessionSetExternalSender(session, payload);
        MainCore.Log.Msg($"[Discord] dave external-sender set ({payload.Length}B)");
    }
    public byte[] KeyPackage() {
        if(disposed || !Ready) return null;
        byte[] package = DaveNative.KeyPackage(session);
        MainCore.Log.Msg($"[Discord] dave key-package ({package?.Length ?? 0}B)");
        return package;
    }
    public byte[] ProcessProposals(byte[] proposals, IReadOnlyList<string> recognized) {
        if(disposed || !Ready || proposals == null) return null;
        byte[] commitWelcome = DaveNative.ProcessProposals(session, proposals, recognized);
        MainCore.Log.Msg(
            $"[Discord] dave proposals→commitWelcome ({proposals.Length}B → {commitWelcome?.Length ?? 0}B)");
        return commitWelcome == null || commitWelcome.Length == 0 ? null : commitWelcome;
    }
    public void ProcessCommit(byte[] commit) {
        if(disposed || !Ready || commit == null) return;
        if(!DaveNative.ProcessCommit(session, commit, out List<string> members)) {
            MainCore.Log.Msg("[Discord] dave commit failed or was ignored");
            return;
        }
        UpdateRoster(members);
        RefreshRatchets();
    }
    public void ProcessWelcome(byte[] welcome, IReadOnlyList<string> recognized) {
        if(disposed || !Ready || welcome == null) return;
        if(!DaveNative.ProcessWelcome(session, welcome, recognized, out List<string> members)) {
            MainCore.Log.Msg("[Discord] dave welcome produced no result");
            return;
        }
        UpdateRoster(members);
        RefreshRatchets();
    }
    private void UpdateRoster(List<string> members) {
        if(members == null || members.Count == 0) return;
        roster.Clear();
        roster.AddRange(members);
        MainCore.Log.Msg("[Discord] dave roster: " + string.Join(",", roster));
    }
    public void SetVersion(ushort version) {
        if(disposed || !Ready) return;
        Version = version;
        DaveNative.SessionSetVersion(session, version);
    }
    public void RefreshRatchets() {
        if(disposed || !Ready) return;
        IntPtr self = GetOrRenewRatchet(selfUserId);
        if(self != IntPtr.Zero && encryptor != IntPtr.Zero) {
            DaveNative.EncryptorSetRatchet(encryptor, self);
            DaveNative.EncryptorPassthrough(encryptor, false);
            KeysReady = true;
        }
        foreach(string userId in roster) {
            if(userId == selfUserId) continue;
            IntPtr ratchet = GetOrRenewRatchet(userId);
            if(ratchet == IntPtr.Zero) continue;
            if(!decryptors.TryGetValue(userId, out IntPtr decryptor)) {
                decryptor = DaveNative.DecryptorCreate();
                if(decryptor == IntPtr.Zero) continue;
                decryptors[userId] = decryptor;
            }
            DaveNative.DecryptorTransition(decryptor, ratchet);
            DaveNative.DecryptorPassthrough(decryptor, false);
        }
        MainCore.Log.Msg(
            $"[Discord] dave ratchets refreshed (self + {decryptors.Count} remotes, keysReady={KeysReady})");
    }
    private IntPtr GetOrRenewRatchet(string userId) {
        IntPtr ratchet = DaveNative.KeyRatchet(session, userId);
        if(ratchet == IntPtr.Zero) return IntPtr.Zero;
        if(ratchets.TryGetValue(userId, out IntPtr old) && old != IntPtr.Zero && old != ratchet)
            DaveNative.KeyRatchetDestroy(old);
        ratchets[userId] = ratchet;
        return ratchet;
    }
    public void MapSsrc(uint ssrc, string userId) {
        if(userId == null || ssrc == 0 || userId == selfUserId) return;
        ssrcToUser[ssrc] = userId;
        MainCore.Log.Msg($"[Discord] dave map ssrc {ssrc} → user {userId}");
    }
    public byte[] EncryptOpus(byte[] opus, int length) {
        if(disposed || encryptor == IntPtr.Zero) return Trim(opus, length);
        int capacity = Math.Max(length, DaveNative.EncryptorMaxSize(encryptor, DaveNative.MediaType.Audio, length));
        byte[] output = new byte[capacity];
        int written = DaveNative.Encrypt(encryptor, selfSsrc, opus, length, output);
        if(written <= 0) return Trim(opus, length);
        return Trim(output, written);
    }
    public byte[] DecryptOpus(uint ssrc, byte[] frame) {
        if(disposed) return frame;
        if(ssrcToUser.TryGetValue(ssrc, out string mapped)
            && decryptors.TryGetValue(mapped, out IntPtr known)) {
            byte[] direct = TryDecrypt(known, frame);
            if(direct != null) LogDecrypt(ssrc);
            return direct;
        }
        HashSet<string> assigned = new(ssrcToUser.Values);
        foreach(KeyValuePair<string, IntPtr> pair in decryptors) {
            if(assigned.Contains(pair.Key)) continue;
            byte[] trial = TryDecrypt(pair.Value, frame);
            if(trial == null) continue;
            ssrcToUser[ssrc] = pair.Key;
            MainCore.Log.Msg($"[Discord] dave decrypt: mapped ssrc {ssrc} → {pair.Key} by trial");
            LogDecrypt(ssrc);
            return trial;
        }
        return null;
    }
    private static byte[] TryDecrypt(IntPtr decryptor, byte[] frame) {
        int capacity = Math.Max(
            frame.Length, DaveNative.DecryptorMaxSize(decryptor, DaveNative.MediaType.Audio, frame.Length));
        byte[] output = new byte[capacity];
        int written = DaveNative.Decrypt(decryptor, frame, frame.Length, output);
        return written <= 0 ? null : Trim(output, written);
    }
    private static byte[] Trim(byte[] buffer, int length) {
        if(buffer == null) return null;
        if(buffer.Length == length) return buffer;
        byte[] result = new byte[length];
        Buffer.BlockCopy(buffer, 0, result, 0, length);
        return result;
    }
    private void LogDecrypt(uint ssrc) {
        decryptOk++;
        if(decryptOk <= 3 || decryptOk % 250 == 0)
            MainCore.Log.Msg($"[Discord] dave decrypt ok #{decryptOk} ssrc={ssrc}");
    }
    public void Dispose() {
        if(disposed) return;
        disposed = true;
        try {
            foreach(KeyValuePair<string, IntPtr> pair in decryptors)
                if(pair.Value != IntPtr.Zero) DaveNative.DecryptorDestroy(pair.Value);
            foreach(KeyValuePair<string, IntPtr> pair in ratchets)
                if(pair.Value != IntPtr.Zero) DaveNative.KeyRatchetDestroy(pair.Value);
            decryptors.Clear();
            ratchets.Clear();
            if(encryptor != IntPtr.Zero) DaveNative.EncryptorDestroy(encryptor);
            encryptor = IntPtr.Zero;
            if(session != IntPtr.Zero) DaveNative.SessionDestroy(session);
            session = IntPtr.Zero;
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
}
