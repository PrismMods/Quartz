using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using static Quartz.Features.Discord.Json;
namespace Quartz.Features.Discord;
public sealed class RemoteAuth : IDisposable {
    private const string GatewayUrl = "wss://remote-auth-gateway.discord.gg/?v=2";
    private const string LoginEndpoint = "https://discord.com/api/v9/users/@me/remote-auth/login";
    private const string UserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
        + "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private readonly RSA rsa = RsaKeys.Create2048();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private ClientWebSocket socket;
    private CancellationTokenSource cts;
    private bool disposed;
    public event Action<string> QrUrl;
    public event Action<string> UserPreview;
    public event Action<string> Token;
    public event Action<string> Status;
    public event Action<string> Failed;
    public async Task StartAsync() {
        cts = new CancellationTokenSource();
        CancellationToken ct = cts.Token;
        socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "https://discord.com");
        socket.Options.SetRequestHeader("User-Agent", UserAgent);
        Raise(Status, "connecting...");
        await socket.ConnectAsync(new Uri(GatewayUrl), ct);
        _ = Task.Run(() => ReceiveLoopAsync(ct), ct);
    }
    private async Task ReceiveLoopAsync(CancellationToken ct) {
        byte[] buffer = new byte[16 * 1024];
        using MemoryStream frame = new();
        try {
            while(socket != null && socket.State == WebSocketState.Open && !ct.IsCancellationRequested) {
                frame.SetLength(0);
                WebSocketReceiveResult result;
                do {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if(result.MessageType == WebSocketMessageType.Close) {
                        Raise(Failed, "connection closed");
                        return;
                    }
                    frame.Write(buffer, 0, result.Count);
                } while(!result.EndOfMessage);
                string text = Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length);
                await HandleAsync(Parse(text), ct);
            }
        } catch(OperationCanceledException e) {
            Diag.Ignore(e);
        } catch(Exception e) {
            Raise(Failed, e.Message);
        }
    }
    private async Task HandleAsync(JToken payload, CancellationToken ct) {
        switch(Str(payload, "op")) {
            case "hello":
                int interval = Int(payload, "heartbeat_interval") ?? 40000;
                _ = Task.Run(() => HeartbeatLoopAsync(interval, ct), ct);
                await SendAsync(
                    new { op = "init", encoded_public_key = Convert.ToBase64String(PublicKey()) }, ct);
                Raise(Status, "waiting for a QR code...");
                break;
            case "nonce_proof":
                byte[] nonce = Decrypt(Str(payload, "encrypted_nonce"));
                byte[] digest;
                using(SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(nonce);
                await SendAsync(new { op = "nonce_proof", proof = Der.Base64Url(digest) }, ct);
                break;
            case "pending_remote_init":
                Raise(QrUrl, "https://discord.com/ra/" + Str(payload, "fingerprint"));
                Raise(Status, "scan the code with the Discord app");
                break;
            case "pending_ticket":
                string preview = Encoding.UTF8.GetString(Decrypt(Str(payload, "encrypted_user_payload")));
                string[] parts = preview.Split(':');
                Raise(UserPreview, parts.Length >= 4 ? parts[3] : preview);
                Raise(Status, "approve the login on your phone");
                break;
            case "pending_login":
                await ExchangeTicketAsync(Str(payload, "ticket"));
                break;
            case "cancel":
                Raise(Failed, "login cancelled on the phone");
                break;
        }
    }
    private async Task ExchangeTicketAsync(string ticket) {
        Raise(Status, "logging in...");
        try {
            using HttpClient http = new();
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            using HttpResponseMessage response = await http.PostAsync(LoginEndpoint, Body(new { ticket }));
            response.EnsureSuccessStatusCode();
            JToken root = Parse(await response.Content.ReadAsStringAsync());
            string encrypted = Str(root, "encrypted_token");
            if(encrypted == null) {
                Raise(Failed, "token exchange returned no token");
                return;
            }
            Raise(Token, Encoding.UTF8.GetString(Decrypt(encrypted)));
        } catch(Exception e) {
            Raise(Failed, "token exchange failed: " + e.Message);
        }
    }
    private async Task HeartbeatLoopAsync(int intervalMs, CancellationToken ct) {
        try {
            while(!ct.IsCancellationRequested && socket != null && socket.State == WebSocketState.Open) {
                await Task.Delay(intervalMs, ct);
                await SendAsync(new { op = "heartbeat" }, ct);
            }
        } catch(OperationCanceledException e) {
            Diag.Ignore(e);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
    private byte[] PublicKey() => Der.SubjectPublicKeyInfo(rsa.ExportParameters(false));
    private byte[] Decrypt(string base64) =>
        RsaOaep.DecryptSha256(rsa, Convert.FromBase64String(base64));
    private async Task SendAsync(object payload, CancellationToken ct) {
        if(socket == null || socket.State != WebSocketState.Open) return;
        byte[] bytes = Encoding.UTF8.GetBytes(Serialize(payload));
        await sendLock.WaitAsync(ct);
        try {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        } finally {
            sendLock.Release();
        }
    }
    private static void Raise(Action<string> handler, string value) {
        if(handler == null) return;
        Quartz.Async.MainThread.Enqueue(() => handler(value));
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
        rsa.Dispose();
    }
}
