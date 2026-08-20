using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using static Quartz.Features.Discord.Json;
namespace Quartz.Features.Discord;
public sealed class VoiceGateway : IDisposable {
    private readonly string endpoint;
    private readonly string serverId;
    private readonly string userId;
    private readonly string sessionId;
    private readonly string token;
    private readonly int daveVersion;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private ClientWebSocket socket;
    private CancellationTokenSource cts;
    private long heartbeatNonce;
    private int lastSeq = -1;
    private long received;
    private long beats;
    private bool disposed;
    public int Ssrc { get; private set; }
    public event Action<int, string, int, IReadOnlyList<string>> Ready;
    public event Action<byte[], string> SessionDescription;
    public event Action<string> Status;
    public event Action<string> Error;
    public event Action<int, string> Closed;
    public event Action<string, uint> Speaking;
    public event Action<int, JToken> DaveControl;
    public event Action<int, byte[]> DaveBinary;
    public VoiceGateway(
        string endpoint, string serverId, string userId, string sessionId, string token, int daveVersion = 0
    ) {
        this.endpoint = endpoint;
        this.serverId = serverId;
        this.userId = userId;
        this.sessionId = sessionId;
        this.token = token;
        this.daveVersion = daveVersion;
    }
    public async Task StartAsync() {
        cts = new CancellationTokenSource();
        CancellationToken ct = cts.Token;
        socket = new ClientWebSocket();
        string url = $"wss://{endpoint}/?v=8";
        Raise(Status, "voice: connecting to " + endpoint);
        await socket.ConnectAsync(new Uri(url), ct);
        await SendJsonAsync(
            new {
                op = 0,
                d = new {
                    server_id = serverId,
                    user_id = userId,
                    session_id = sessionId,
                    token,
                    max_dave_protocol_version = daveVersion,
                },
            },
            ct);
        _ = Task.Run(() => ReceiveLoopAsync(ct), ct);
    }
    public Task SendSelectProtocolAsync(string ip, int port, string mode) => SendJsonAsync(
        new { op = 1, d = new { protocol = "udp", data = new { address = ip, port, mode } } },
        cts?.Token ?? CancellationToken.None);
    public Task SendSpeakingAsync(bool speaking, int ssrc) {
        MainCore.Log.Msg($"[Discord] vgw speaking={speaking} ssrc={ssrc}");
        return SendSpeakingInternalAsync(speaking, ssrc);
    }
    private Task SendSpeakingInternalAsync(bool speaking, int ssrc) => SendJsonAsync(
        new { op = 5, d = new { speaking = speaking ? 1 : 0, delay = 0, ssrc } },
        cts?.Token ?? CancellationToken.None);
    private async Task ReceiveLoopAsync(CancellationToken ct) {
        byte[] buffer = new byte[64 * 1024];
        using MemoryStream frame = new();
        try {
            while(socket != null && socket.State == WebSocketState.Open && !ct.IsCancellationRequested) {
                frame.SetLength(0);
                WebSocketReceiveResult result;
                do {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if(result.MessageType == WebSocketMessageType.Close) {
                        int code = (int?)result.CloseStatus ?? 0;
                        RaiseClosed(code, result.CloseStatusDescription);
                        return;
                    }
                    frame.Write(buffer, 0, result.Count);
                } while(!result.EndOfMessage);
                if(result.MessageType == WebSocketMessageType.Binary) {
                    HandleBinary(frame.GetBuffer(), (int)frame.Length);
                    continue;
                }
                string text = Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length);
                await HandleAsync(Parse(text), ct);
            }
        } catch(OperationCanceledException e) {
            Diag.Ignore(e);
        } catch(Exception e) {
            Raise(Error, e.Message);
            RaiseClosed(0, e.Message);
        }
    }
    private void HandleBinary(byte[] bytes, int length) {
        if(length < 3) return;
        lastSeq = (bytes[0] << 8) | bytes[1];
        int op = bytes[2];
        byte[] payload = new byte[length - 3];
        Buffer.BlockCopy(bytes, 3, payload, 0, payload.Length);
        Action<int, byte[]> handler = DaveBinary;
        if(handler != null) MainThread.Enqueue(() => handler(op, payload));
    }
    public Task SendDaveBinaryAsync(int opcode, byte[] payload) {
        byte[] frame = new byte[1 + payload.Length];
        frame[0] = (byte)opcode;
        Buffer.BlockCopy(payload, 0, frame, 1, payload.Length);
        return SendRawAsync(frame, WebSocketMessageType.Binary, cts?.Token ?? CancellationToken.None);
    }
    public Task SendDaveTransitionReadyAsync(int transitionId) => SendJsonAsync(
        new { op = 23, d = new { transition_id = transitionId } }, cts?.Token ?? CancellationToken.None);
    public Task SendDaveInvalidCommitAsync(int transitionId) => SendJsonAsync(
        new { op = 31, d = new { transition_id = transitionId } }, cts?.Token ?? CancellationToken.None);
    private async Task HandleAsync(JToken payload, CancellationToken ct) {
        int op = Int(payload, "op") ?? -1;
        int? sequence = Int(payload, "seq");
        if(sequence.HasValue) lastSeq = sequence.Value;
        JToken d = Prop(payload, "d");
        received++;
        if(received <= 20) MainCore.Log.Msg($"[Discord] vgw recv op {op}");
        switch(op) {
            case 8:
                int interval = (int)(Prop(d, "heartbeat_interval")?.Value<double>() ?? 41250d);
                MainCore.Log.Msg($"[Discord] vgw HELLO, heartbeat every {interval}ms");
                _ = Task.Run(() => HeartbeatLoopAsync(interval, ct), ct);
                break;
            case 2:
                Ssrc = Int(d, "ssrc") ?? 0;
                string ip = Str(d, "ip");
                int port = Int(d, "port") ?? 0;
                List<string> modes = [];
                JArray raw = Arr(d, "modes");
                if(raw != null)
                    foreach(JToken mode in raw) modes.Add(mode.Value<string>() ?? "");
                if(Ready != null) {
                    Action<int, string, int, IReadOnlyList<string>> handler = Ready;
                    int ssrc = Ssrc;
                    MainThread.Enqueue(() => handler(ssrc, ip, port, modes));
                }
                break;
            case 4:
                string chosen = Str(d, "mode") ?? "";
                JArray keyArray = Arr(d, "secret_key");
                List<byte> key = [];
                if(keyArray != null)
                    foreach(JToken piece in keyArray) key.Add((byte)(piece.Value<int?>() ?? 0));
                if(SessionDescription != null) {
                    Action<byte[], string> handler = SessionDescription;
                    byte[] secret = [.. key];
                    MainThread.Enqueue(() => handler(secret, chosen));
                }
                break;
            case 5:
                string speakerId = Str(d, "user_id");
                uint speakerSsrc = (uint)(Prop(d, "ssrc")?.Value<long>() ?? 0L);
                if(speakerId != null && speakerSsrc != 0 && Speaking != null) {
                    Action<string, uint> speakingHandler = Speaking;
                    MainThread.Enqueue(() => speakingHandler(speakerId, speakerSsrc));
                }
                break;
            case 21:
            case 22:
            case 24:
                Action<int, JToken> daveHandler = DaveControl;
                if(daveHandler != null) {
                    int control = op;
                    JToken data = d;
                    MainThread.Enqueue(() => daveHandler(control, data));
                }
                break;
        }
        await Task.CompletedTask;
    }
    private async Task HeartbeatLoopAsync(int intervalMs, CancellationToken ct) {
        try {
            while(!ct.IsCancellationRequested && socket != null && socket.State == WebSocketState.Open) {
                await SendJsonAsync(new { op = 3, d = new { t = ++heartbeatNonce, seq_ack = lastSeq } }, ct);
                beats++;
                if(beats <= 3 || beats % 10 == 0)
                    MainCore.Log.Msg($"[Discord] vgw heartbeat #{beats} (seq_ack={lastSeq})");
                await Task.Delay(intervalMs, ct);
            }
        } catch(OperationCanceledException e) {
            Diag.Ignore(e);
        } catch(Exception e) {
            Raise(Error, "voice heartbeat: " + e.Message);
        }
    }
    private Task SendJsonAsync(object payload, CancellationToken ct) =>
        SendRawAsync(Encoding.UTF8.GetBytes(Serialize(payload)), WebSocketMessageType.Text, ct);
    private async Task SendRawAsync(byte[] bytes, WebSocketMessageType type, CancellationToken ct) {
        if(socket == null || socket.State != WebSocketState.Open) return;
        await sendLock.WaitAsync(ct);
        try {
            await socket.SendAsync(new ArraySegment<byte>(bytes), type, true, ct);
        } finally {
            sendLock.Release();
        }
    }
    private static void Raise(Action<string> handler, string value) {
        if(handler != null) MainThread.Enqueue(() => handler(value));
    }
    private void RaiseClosed(int code, string description) {
        Action<int, string> handler = Closed;
        if(handler != null) MainThread.Enqueue(() => handler(code, description));
    }
    public void Dispose() {
        if(disposed) return;
        disposed = true;
        try {
            cts?.Cancel();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        try {
            if(socket != null && socket.State == WebSocketState.Open)
                socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                    .GetAwaiter().GetResult();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        socket?.Dispose();
        cts?.Dispose();
        sendLock.Dispose();
    }
}
