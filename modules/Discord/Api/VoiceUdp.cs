using System.Net;
using System.Net.Sockets;
using System.Text;
using Quartz.Core;
namespace Quartz.Features.Discord;
public sealed class VoiceUdp : IDisposable {
    public const string PreferredMode = "aead_xchacha20_poly1305_rtpsize";
    private const int HeaderSize = 12;
    private const int NonceSuffix = 4;
    private readonly UdpClient udp = new();
    private IPEndPoint server;
    private readonly uint ssrc;
    private byte[] key = [];
    private ushort sequence;
    private uint timestamp;
    private uint nonceCounter;
    private long sentPackets;
    private CancellationTokenSource receiveCts;
    private bool disposed;
    public event Action<uint, byte[]> AudioReceived;
    public event Action<string> Error;
    public bool Ready => key.Length == SodiumNative.KeyBytes;
    public VoiceUdp(int ssrc) => this.ssrc = (uint)ssrc;
    public void SetKey(byte[] secret) => key = secret ?? [];
    public async Task<(string Ip, int Port)> DiscoverAsync(string serverIp, int serverPort) {
        server = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        udp.Connect(server);
        byte[] request = new byte[74];
        request[1] = 0x01;
        request[3] = 70;
        WriteUInt32(request, 4, ssrc);
        await udp.SendAsync(request, request.Length);
        Task<UdpReceiveResult> receive = udp.ReceiveAsync();
        if(await Task.WhenAny(receive, Task.Delay(5000)) != receive)
            throw new TimeoutException("the voice server did not answer IP discovery");
        byte[] data = receive.Result.Buffer;
        if(data.Length < 74) throw new InvalidOperationException($"short IP discovery reply ({data.Length} bytes)");
        int end = 8;
        while(end < 72 && data[end] != 0) end++;
        return (Encoding.ASCII.GetString(data, 8, end - 8), (data[72] << 8) | data[73]);
    }
    public static string PickMode(IReadOnlyList<string> offered) {
        if(offered == null || offered.Count == 0) return PreferredMode;
        foreach(string mode in offered)
            if(mode == PreferredMode) return mode;
        foreach(string mode in offered)
            if(mode.Contains("xchacha20")) return mode;
        return offered[0];
    }
    public void StartReceiving() {
        if(receiveCts != null) return;
        receiveCts = new CancellationTokenSource();
        CancellationToken ct = receiveCts.Token;
        _ = Task.Run(() => ReceiveLoopAsync(ct));
    }
    private async Task ReceiveLoopAsync(CancellationToken ct) {
        long total = 0;
        long failed = 0;
        long delivered = 0;
        long rtcp = 0;
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        _ = Task.Run(async () => {
            while(!ct.IsCancellationRequested) {
                await Task.Delay(5000, ct);
                MainCore.Log.Msg(
                    $"[Discord] voice udp: rx total={total} audio={delivered} rtcp={rtcp} failed={failed}"
                    + $", tx={sentPackets} in {clock.Elapsed.TotalSeconds:F0}s");
            }
        }, ct);
        while(!ct.IsCancellationRequested) {
            byte[] packet;
            try {
                UdpReceiveResult result = await udp.ReceiveAsync();
                packet = result.Buffer;
            } catch(ObjectDisposedException e) {
                Diag.Ignore(e);
                return;
            } catch(OperationCanceledException e) {
                Diag.Ignore(e);
                return;
            } catch(Exception e) {
                Error?.Invoke("receive: " + e.Message);
                return;
            }
            total++;
            if(!Ready) continue;
            if(packet.Length < HeaderSize + NonceSuffix + SodiumNative.TagBytes) continue;
            int payloadType = packet[1] & 0x7F;
            if(payloadType is >= 72 and <= 79) {
                rtcp++;
                continue;
            }
            uint sourceSsrc = ReadUInt32(packet, 8);
            int extension = (packet[0] & 0x10) != 0 ? 1 : 0;
            int aadLength = HeaderSize + ((packet[0] & 0x0F) * 4) + (extension * 4);
            if(aadLength > packet.Length - NonceSuffix - SodiumNative.TagBytes) continue;
            int extensionBody = extension == 1
                ? ((packet[aadLength - 2] << 8) | packet[aadLength - 1]) * 4
                : 0;
            byte[] nonce = new byte[SodiumNative.NonceBytes];
            Buffer.BlockCopy(packet, packet.Length - NonceSuffix, nonce, 0, NonceSuffix);
            byte[] aad = new byte[aadLength];
            Buffer.BlockCopy(packet, 0, aad, 0, aadLength);
            int cipherLength = packet.Length - aadLength - NonceSuffix;
            byte[] cipher = new byte[cipherLength];
            Buffer.BlockCopy(packet, aadLength, cipher, 0, cipherLength);
            byte[] plain = SodiumNative.Decrypt(cipher, cipherLength, aad, nonce, key);
            if(plain == null) {
                failed++;
                if(failed <= 5)
                    MainCore.Log.Msg(
                        $"[Discord] voice transport-decrypt failed #{failed} len={packet.Length} "
                        + $"b0=0x{packet[0]:x2} aad={aadLength} pt={payloadType} ssrc={sourceSsrc}");
                continue;
            }
            if(extensionBody >= plain.Length) continue;
            byte[] frame = plain;
            if(extensionBody > 0) {
                frame = new byte[plain.Length - extensionBody];
                Buffer.BlockCopy(plain, extensionBody, frame, 0, frame.Length);
            }
            delivered++;
            if(delivered <= 3 || delivered % 250 == 0)
                MainCore.Log.Msg(
                    $"[Discord] voice rx ok total={total} delivered={delivered} failed={failed} "
                    + $"ssrc={sourceSsrc} aad={aadLength} extBody={extensionBody} frame={frame.Length}B");
            AudioReceived?.Invoke(sourceSsrc, frame);
        }
    }
    public void SendAudio(byte[] opus, int length) {
        int opusLength = length;
        if(!Ready || opus == null || length <= 0) return;
        byte[] header = new byte[HeaderSize];
        header[0] = 0x80;
        header[1] = 0x78;
        header[2] = (byte)(sequence >> 8);
        header[3] = (byte)sequence;
        WriteUInt32(header, 4, timestamp);
        WriteUInt32(header, 8, ssrc);
        sequence++;
        timestamp += OpusNative.FrameSamples;
        uint counter = ++nonceCounter;
        byte[] nonce = new byte[SodiumNative.NonceBytes];
        WriteUInt32(nonce, 0, counter);
        byte[] cipher = SodiumNative.Encrypt(opus, length, header, nonce, key);
        if(cipher == null) {
            Error?.Invoke("encrypt returned nothing");
            return;
        }
        byte[] packet = new byte[HeaderSize + cipher.Length + NonceSuffix];
        Buffer.BlockCopy(header, 0, packet, 0, HeaderSize);
        Buffer.BlockCopy(cipher, 0, packet, HeaderSize, cipher.Length);
        WriteUInt32(packet, HeaderSize + cipher.Length, counter);
        try {
            udp.Send(packet, packet.Length);
            sentPackets++;
            if(sentPackets <= 3)
                MainCore.Log.Msg(
                    $"[Discord] voice tx #{sentPackets}: {packet.Length}B "
                    + $"(payload {opusLength}B, seq {sequence}, ssrc {ssrc})");
        } catch(Exception e) {
            Error?.Invoke("send: " + e.Message);
        }
    }
    private static void WriteUInt32(byte[] buffer, int offset, uint value) {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
    private static uint ReadUInt32(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16)
        | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
    public void Dispose() {
        if(disposed) return;
        disposed = true;
        try {
            receiveCts?.Cancel();
            receiveCts?.Dispose();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        try {
            udp.Close();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
}
