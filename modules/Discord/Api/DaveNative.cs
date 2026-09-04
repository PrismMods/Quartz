using System.Runtime.InteropServices;
using System.Text;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class DaveNative {
    public enum MediaType { Audio = 0, Video = 1 }
    public enum Codec { Unknown = 0, Opus = 1, Vp8 = 2, Vp9 = 3, H264 = 4, H265 = 5, Av1 = 6 }
    public enum EncryptResult { Success = 0, EncryptionFailure = 1, MissingKeyRatchet = 2, MissingCryptor = 3, TooManyAttempts = 4 }
    public enum DecryptResult { Success = 0, DecryptionFailure = 1, MissingKeyRatchet = 2, InvalidNonce = 3, MissingCryptor = 4 }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MlsFailure(IntPtr source, IntPtr reason, IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ushort MaxVersionFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeFn(IntPtr ptr);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr SessionCreateFn(IntPtr context, IntPtr authSessionId, MlsFailure callback, IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SessionDestroyFn(IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SessionInitFn(IntPtr session, ushort version, ulong groupId, IntPtr selfUserId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SessionResetFn(IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SessionSetVersionFn(IntPtr session, ushort version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ushort SessionGetVersionFn(IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SessionSetExternalSenderFn(IntPtr session, IntPtr bytes, UIntPtr length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SessionGetKeyPackageFn(IntPtr session, out IntPtr package, out UIntPtr length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SessionProcessProposalsFn(IntPtr session, IntPtr proposals, UIntPtr length, IntPtr[] recognized, UIntPtr recognizedLength, out IntPtr commitWelcome, out UIntPtr commitWelcomeLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr SessionProcessCommitFn(IntPtr session, IntPtr commit, UIntPtr length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr SessionProcessWelcomeFn(IntPtr session, IntPtr welcome, UIntPtr length, IntPtr[] recognized, UIntPtr recognizedLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr SessionGetKeyRatchetFn(IntPtr session, IntPtr userId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void KeyRatchetDestroyFn(IntPtr ratchet);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte ResultFlagFn(IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ResultDestroyFn(IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RosterFn(IntPtr handle, out IntPtr ids, out UIntPtr length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CryptorCreateFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void CryptorDestroyFn(IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void EncryptorSetRatchetFn(IntPtr encryptor, IntPtr ratchet);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PassthroughFn(IntPtr handle, byte enabled);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte HasRatchetFn(IntPtr encryptor);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void EncryptorAssignCodecFn(IntPtr encryptor, uint ssrc, Codec codec);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate UIntPtr EncryptorMaxSizeFn(IntPtr encryptor, MediaType type, UIntPtr frameSize);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate EncryptResult EncryptFn(IntPtr encryptor, MediaType type, uint ssrc, IntPtr frame, UIntPtr frameLength, IntPtr output, UIntPtr capacity, out UIntPtr written);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DecryptorTransitionFn(IntPtr decryptor, IntPtr ratchet);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate UIntPtr DecryptorMaxSizeFn(IntPtr decryptor, MediaType type, UIntPtr encryptedSize);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate DecryptResult DecryptFn(IntPtr decryptor, MediaType type, IntPtr encrypted, UIntPtr encryptedLength, IntPtr output, UIntPtr capacity, out UIntPtr written);
    private static IntPtr handle;
    private static MaxVersionFn maxVersion;
    private static FreeFn free;
    private static SessionCreateFn sessionCreate;
    private static SessionDestroyFn sessionDestroy;
    private static SessionInitFn sessionInit;
    private static SessionResetFn sessionReset;
    private static SessionSetVersionFn sessionSetVersion;
    private static SessionGetVersionFn sessionGetVersion;
    private static SessionSetExternalSenderFn sessionSetExternalSender;
    private static SessionGetKeyPackageFn sessionGetKeyPackage;
    private static SessionProcessProposalsFn sessionProcessProposals;
    private static SessionProcessCommitFn sessionProcessCommit;
    private static SessionProcessWelcomeFn sessionProcessWelcome;
    private static SessionGetKeyRatchetFn sessionGetKeyRatchet;
    private static KeyRatchetDestroyFn keyRatchetDestroy;
    private static ResultFlagFn commitFailed;
    private static ResultFlagFn commitIgnored;
    private static ResultDestroyFn commitDestroy;
    private static ResultDestroyFn welcomeDestroy;
    private static RosterFn commitRoster;
    private static RosterFn welcomeRoster;
    private static CryptorCreateFn encryptorCreate;
    private static CryptorDestroyFn encryptorDestroy;
    private static EncryptorSetRatchetFn encryptorSetRatchet;
    private static PassthroughFn encryptorPassthrough;
    private static PassthroughFn decryptorPassthrough;
    private static HasRatchetFn encryptorHasRatchet;
    private static EncryptorAssignCodecFn encryptorAssignCodec;
    private static EncryptorMaxSizeFn encryptorMaxSize;
    private static EncryptFn encrypt;
    private static CryptorCreateFn decryptorCreate;
    private static CryptorDestroyFn decryptorDestroy;
    private static DecryptorTransitionFn decryptorTransition;
    private static DecryptorMaxSizeFn decryptorMaxSize;
    private static DecryptFn decrypt;
    public static bool Available { get; private set; }
    public static string LoadError { get; private set; } = "not loaded";
    public static ushort MaxProtocolVersion { get; private set; }
    public static bool Load() {
        if(Available) return true;
        string path = VoiceNatives.Locate("dave");
        if(path == null) {
            LoadError = "libdave is not installed";
            return false;
        }
        IntPtr library = NativeLib.Load(path);
        if(library == IntPtr.Zero) {
            LoadError = "the operating system refused to load " + Path.GetFileName(path);
            return false;
        }
        handle = library;
        maxVersion = NativeLib.Bind<MaxVersionFn>(library, "daveMaxSupportedProtocolVersion");
        free = NativeLib.Bind<FreeFn>(library, "daveFree");
        sessionCreate = NativeLib.Bind<SessionCreateFn>(library, "daveSessionCreate");
        sessionDestroy = NativeLib.Bind<SessionDestroyFn>(library, "daveSessionDestroy");
        sessionInit = NativeLib.Bind<SessionInitFn>(library, "daveSessionInit");
        sessionReset = NativeLib.Bind<SessionResetFn>(library, "daveSessionReset");
        sessionSetVersion = NativeLib.Bind<SessionSetVersionFn>(library, "daveSessionSetProtocolVersion");
        sessionGetVersion = NativeLib.Bind<SessionGetVersionFn>(library, "daveSessionGetProtocolVersion");
        sessionSetExternalSender = NativeLib.Bind<SessionSetExternalSenderFn>(library, "daveSessionSetExternalSender");
        sessionGetKeyPackage = NativeLib.Bind<SessionGetKeyPackageFn>(library, "daveSessionGetMarshalledKeyPackage");
        sessionProcessProposals = NativeLib.Bind<SessionProcessProposalsFn>(library, "daveSessionProcessProposals");
        sessionProcessCommit = NativeLib.Bind<SessionProcessCommitFn>(library, "daveSessionProcessCommit");
        sessionProcessWelcome = NativeLib.Bind<SessionProcessWelcomeFn>(library, "daveSessionProcessWelcome");
        sessionGetKeyRatchet = NativeLib.Bind<SessionGetKeyRatchetFn>(library, "daveSessionGetKeyRatchet");
        keyRatchetDestroy = NativeLib.Bind<KeyRatchetDestroyFn>(library, "daveKeyRatchetDestroy");
        commitFailed = NativeLib.Bind<ResultFlagFn>(library, "daveCommitResultIsFailed");
        commitIgnored = NativeLib.Bind<ResultFlagFn>(library, "daveCommitResultIsIgnored");
        commitDestroy = NativeLib.Bind<ResultDestroyFn>(library, "daveCommitResultDestroy");
        commitRoster = NativeLib.Bind<RosterFn>(library, "daveCommitResultGetRosterMemberIds");
        welcomeRoster = NativeLib.Bind<RosterFn>(library, "daveWelcomeResultGetRosterMemberIds");
        welcomeDestroy = NativeLib.Bind<ResultDestroyFn>(library, "daveWelcomeResultDestroy");
        encryptorCreate = NativeLib.Bind<CryptorCreateFn>(library, "daveEncryptorCreate");
        encryptorDestroy = NativeLib.Bind<CryptorDestroyFn>(library, "daveEncryptorDestroy");
        encryptorSetRatchet = NativeLib.Bind<EncryptorSetRatchetFn>(library, "daveEncryptorSetKeyRatchet");
        encryptorPassthrough = NativeLib.Bind<PassthroughFn>(library, "daveEncryptorSetPassthroughMode");
        decryptorPassthrough = NativeLib.Bind<PassthroughFn>(library, "daveDecryptorTransitionToPassthroughMode");
        encryptorHasRatchet = NativeLib.Bind<HasRatchetFn>(library, "daveEncryptorHasKeyRatchet");
        encryptorAssignCodec = NativeLib.Bind<EncryptorAssignCodecFn>(library, "daveEncryptorAssignSsrcToCodec");
        encryptorMaxSize = NativeLib.Bind<EncryptorMaxSizeFn>(library, "daveEncryptorGetMaxCiphertextByteSize");
        encrypt = NativeLib.Bind<EncryptFn>(library, "daveEncryptorEncrypt");
        decryptorCreate = NativeLib.Bind<CryptorCreateFn>(library, "daveDecryptorCreate");
        decryptorDestroy = NativeLib.Bind<CryptorDestroyFn>(library, "daveDecryptorDestroy");
        decryptorTransition = NativeLib.Bind<DecryptorTransitionFn>(library, "daveDecryptorTransitionToKeyRatchet");
        decryptorMaxSize = NativeLib.Bind<DecryptorMaxSizeFn>(library, "daveDecryptorGetMaxPlaintextByteSize");
        decrypt = NativeLib.Bind<DecryptFn>(library, "daveDecryptorDecrypt");
        string missing = FirstMissing();
        if(missing != null) {
            LoadError = "libdave is missing the export " + missing;
            NativeLib.Free(library);
            handle = IntPtr.Zero;
            return false;
        }
        try {
            MaxProtocolVersion = maxVersion();
        } catch(Exception e) {
            LoadError = "calling into libdave failed: " + e.Message;
            return false;
        }
        Available = true;
        LoadError = "";
        MainCore.Log.Msg($"[Discord] libdave loaded, max DAVE protocol version {MaxProtocolVersion}");
        return true;
    }
    private static string FirstMissing() {
        if(maxVersion == null) return "daveMaxSupportedProtocolVersion";
        if(free == null) return "daveFree";
        if(sessionCreate == null) return "daveSessionCreate";
        if(sessionInit == null) return "daveSessionInit";
        if(sessionGetKeyPackage == null) return "daveSessionGetMarshalledKeyPackage";
        if(sessionProcessProposals == null) return "daveSessionProcessProposals";
        if(sessionProcessCommit == null) return "daveSessionProcessCommit";
        if(sessionProcessWelcome == null) return "daveSessionProcessWelcome";
        if(sessionSetExternalSender == null) return "daveSessionSetExternalSender";
        if(sessionGetKeyRatchet == null) return "daveSessionGetKeyRatchet";
        if(encrypt == null) return "daveEncryptorEncrypt";
        if(decrypt == null) return "daveDecryptorDecrypt";
        return null;
    }
    public static IntPtr Utf8(string value) {
        if(value == null) return IntPtr.Zero;
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        IntPtr pointer = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        Marshal.WriteByte(pointer, bytes.Length, 0);
        return pointer;
    }
    public static byte[] Take(IntPtr pointer, UIntPtr length) {
        ulong size = length.ToUInt64();
        if(pointer == IntPtr.Zero || size == 0) return [];
        byte[] result = new byte[size];
        Marshal.Copy(pointer, result, 0, result.Length);
        free?.Invoke(pointer);
        return result;
    }
    public static IntPtr SessionCreate(string authSessionId, MlsFailure callback) {
        IntPtr id = Utf8(authSessionId);
        try {
            return sessionCreate(IntPtr.Zero, id, callback, IntPtr.Zero);
        } finally {
            if(id != IntPtr.Zero) Marshal.FreeHGlobal(id);
        }
    }
    public static void SessionDestroy(IntPtr session) => sessionDestroy?.Invoke(session);
    public static void SessionInit(IntPtr session, ushort version, ulong groupId, string selfUserId) {
        IntPtr id = Utf8(selfUserId);
        try {
            sessionInit(session, version, groupId, id);
        } finally {
            if(id != IntPtr.Zero) Marshal.FreeHGlobal(id);
        }
    }
    public static void SessionReset(IntPtr session) => sessionReset?.Invoke(session);
    public static void SessionSetVersion(IntPtr session, ushort version) => sessionSetVersion?.Invoke(session, version);
    public static ushort SessionVersion(IntPtr session) => sessionGetVersion?.Invoke(session) ?? 0;
    public static void SessionSetExternalSender(IntPtr session, byte[] payload) {
        if(payload == null || payload.Length == 0) return;
        IntPtr buffer = Marshal.AllocHGlobal(payload.Length);
        try {
            Marshal.Copy(payload, 0, buffer, payload.Length);
            sessionSetExternalSender(session, buffer, (UIntPtr)payload.Length);
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }
    public static byte[] KeyPackage(IntPtr session) {
        sessionGetKeyPackage(session, out IntPtr package, out UIntPtr length);
        return Take(package, length);
    }
    public static byte[] ProcessProposals(IntPtr session, byte[] proposals, IReadOnlyList<string> recognized) {
        IntPtr buffer = Marshal.AllocHGlobal(Math.Max(1, proposals.Length));
        IntPtr[] ids = Ids(recognized, out List<IntPtr> allocated);
        try {
            Marshal.Copy(proposals, 0, buffer, proposals.Length);
            sessionProcessProposals(
                session, buffer, (UIntPtr)proposals.Length, ids, (UIntPtr)(ids?.Length ?? 0),
                out IntPtr output, out UIntPtr outputLength);
            return Take(output, outputLength);
        } finally {
            Marshal.FreeHGlobal(buffer);
            foreach(IntPtr pointer in allocated) Marshal.FreeHGlobal(pointer);
        }
    }
    private static List<string> Roster(RosterFn getter, IntPtr result) {
        List<string> ids = [];
        if(getter == null || result == IntPtr.Zero) return ids;
        getter(result, out IntPtr pointer, out UIntPtr length);
        int count = (int)length.ToUInt64();
        if(pointer == IntPtr.Zero || count <= 0) return ids;
        long[] raw = new long[count];
        Marshal.Copy(pointer, raw, 0, count);
        free?.Invoke(pointer);
        foreach(long id in raw) ids.Add(((ulong)id).ToString());
        return ids;
    }
    public static bool ProcessCommit(IntPtr session, byte[] commit, out List<string> roster) {
        roster = [];
        IntPtr buffer = Marshal.AllocHGlobal(Math.Max(1, commit.Length));
        try {
            Marshal.Copy(commit, 0, buffer, commit.Length);
            IntPtr result = sessionProcessCommit(session, buffer, (UIntPtr)commit.Length);
            if(result == IntPtr.Zero) return false;
            try {
                if((commitFailed?.Invoke(result) ?? 0) != 0) return false;
                if((commitIgnored?.Invoke(result) ?? 0) != 0) return false;
                roster = Roster(commitRoster, result);
                return true;
            } finally {
                commitDestroy?.Invoke(result);
            }
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }
    public static bool ProcessWelcome(IntPtr session, byte[] welcome, IReadOnlyList<string> recognized, out List<string> roster) {
        roster = [];
        IntPtr buffer = Marshal.AllocHGlobal(Math.Max(1, welcome.Length));
        IntPtr[] ids = Ids(recognized, out List<IntPtr> allocated);
        try {
            Marshal.Copy(welcome, 0, buffer, welcome.Length);
            IntPtr result = sessionProcessWelcome(
                session, buffer, (UIntPtr)welcome.Length, ids, (UIntPtr)(ids?.Length ?? 0));
            if(result == IntPtr.Zero) return false;
            try {
                roster = Roster(welcomeRoster, result);
                return true;
            } finally {
                welcomeDestroy?.Invoke(result);
            }
        } finally {
            Marshal.FreeHGlobal(buffer);
            foreach(IntPtr pointer in allocated) Marshal.FreeHGlobal(pointer);
        }
    }
    public static IntPtr KeyRatchet(IntPtr session, string userId) {
        IntPtr id = Utf8(userId);
        try {
            return sessionGetKeyRatchet(session, id);
        } finally {
            if(id != IntPtr.Zero) Marshal.FreeHGlobal(id);
        }
    }
    public static void KeyRatchetDestroy(IntPtr ratchet) => keyRatchetDestroy?.Invoke(ratchet);
    public static IntPtr EncryptorCreate() => encryptorCreate?.Invoke() ?? IntPtr.Zero;
    public static void EncryptorDestroy(IntPtr encryptor) => encryptorDestroy?.Invoke(encryptor);
    public static void EncryptorSetRatchet(IntPtr encryptor, IntPtr ratchet) =>
        encryptorSetRatchet?.Invoke(encryptor, ratchet);
    public static void EncryptorAssignCodec(IntPtr encryptor, uint ssrc, Codec codec) =>
        encryptorAssignCodec?.Invoke(encryptor, ssrc, codec);
    public static int EncryptorMaxSize(IntPtr encryptor, MediaType type, int frameSize) =>
        (int)(encryptorMaxSize?.Invoke(encryptor, type, (UIntPtr)frameSize) ?? (UIntPtr)frameSize).ToUInt64();
    public static void EncryptorPassthrough(IntPtr encryptor, bool enabled) =>
        encryptorPassthrough?.Invoke(encryptor, (byte)(enabled ? 1 : 0));
    public static void DecryptorPassthrough(IntPtr decryptor, bool enabled) =>
        decryptorPassthrough?.Invoke(decryptor, (byte)(enabled ? 1 : 0));
    public static bool EncryptorHasRatchet(IntPtr encryptor) => (encryptorHasRatchet?.Invoke(encryptor) ?? 0) != 0;
    public static unsafe int Encrypt(IntPtr encryptor, uint ssrc, byte[] frame, int length, byte[] output) {
        if(encryptor == IntPtr.Zero || length < 0 || length > (frame?.Length ?? 0)
            || output == null || output.Length == 0) return -1;
        fixed(byte* input = frame)
        fixed(byte* result = output) {
            EncryptResult code = encrypt(
                encryptor, MediaType.Audio, ssrc,
                length == 0 ? IntPtr.Zero : (IntPtr)input, (UIntPtr)length,
                (IntPtr)result, (UIntPtr)output.Length, out UIntPtr written);
            if(code != EncryptResult.Success) return -(int)code;
            ulong size = written.ToUInt64();
            return size <= (ulong)output.Length ? (int)size : -1;
        }
    }
    public static unsafe int Decrypt(IntPtr decryptor, byte[] frame, int length, byte[] output) {
        if(decryptor == IntPtr.Zero || length < 0 || length > (frame?.Length ?? 0)
            || output == null || output.Length == 0) return -1;
        fixed(byte* input = frame)
        fixed(byte* result = output) {
            DecryptResult code = decrypt(
                decryptor, MediaType.Audio,
                length == 0 ? IntPtr.Zero : (IntPtr)input, (UIntPtr)length,
                (IntPtr)result, (UIntPtr)output.Length, out UIntPtr written);
            if(code != DecryptResult.Success) return -(int)code;
            ulong size = written.ToUInt64();
            return size <= (ulong)output.Length ? (int)size : -1;
        }
    }
    public static IntPtr DecryptorCreate() => decryptorCreate?.Invoke() ?? IntPtr.Zero;
    public static void DecryptorDestroy(IntPtr decryptor) => decryptorDestroy?.Invoke(decryptor);
    public static void DecryptorTransition(IntPtr decryptor, IntPtr ratchet) =>
        decryptorTransition?.Invoke(decryptor, ratchet);
    public static int DecryptorMaxSize(IntPtr decryptor, MediaType type, int encryptedSize) =>
        (int)(decryptorMaxSize?.Invoke(decryptor, type, (UIntPtr)encryptedSize) ?? (UIntPtr)encryptedSize).ToUInt64();
    private static IntPtr[] Ids(IReadOnlyList<string> recognized, out List<IntPtr> allocated) {
        allocated = [];
        if(recognized == null || recognized.Count == 0) return null;
        IntPtr[] result = new IntPtr[recognized.Count];
        for(int i = 0; i < recognized.Count; i++) {
            result[i] = Utf8(recognized[i]);
            allocated.Add(result[i]);
        }
        return result;
    }
}
