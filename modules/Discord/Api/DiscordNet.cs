using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Quartz.Async;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class DiscordNet {
    public const string ApiBase = "https://discord.com/api/v10";
    public const string GatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";
    private const int TimeoutSeconds = 12;
    public static bool Running { get; private set; }
    public static string Https { get; private set; } = "not run";
    public static string Gateway { get; private set; } = "not run";
    public static string Crypto { get; private set; } = "not run";
    public static string Native { get; private set; } = "not run";
    public static void SelfTest(Action onUpdate) {
        if(Running) return;
        Running = true;
        Https = "testing...";
        Gateway = "waiting";
        Crypto = "waiting";
        Native = "waiting";
        Publish(onUpdate);
        Thread worker = new(() => {
            Https = TestHttps();
            Gateway = "testing...";
            Publish(onUpdate);
            Gateway = TestGateway();
            Publish(onUpdate);
            Crypto = TestCrypto();
            Publish(onUpdate);
            Native = TestNative();
            Running = false;
            Publish(onUpdate);
        }) {
            IsBackground = true,
            Name = "QuartzDiscordSelfTest",
        };
        worker.Start();
    }
    private static void Publish(Action onUpdate) {
        MainCore.Log.Msg($"[Discord] https={Https} gateway={Gateway} crypto={Crypto} native={Native}");
        if(onUpdate != null) MainThread.Enqueue(onUpdate);
    }
    private static string TestHttps() {
        try {
            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
            using HttpResponseMessage response = http.GetAsync(ApiBase + "/gateway").GetAwaiter().GetResult();
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return $"ok — {(int)response.StatusCode} {response.StatusCode}, {body.Length} bytes";
        } catch(Exception e) {
            return "FAILED — " + Describe(e);
        }
    }
    private static string TestGateway() {
        ClientWebSocket socket = null;
        try {
            socket = new ClientWebSocket();
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(TimeoutSeconds));
            socket.ConnectAsync(new Uri(GatewayUrl), cts.Token).GetAwaiter().GetResult();
            byte[] buffer = new byte[8192];
            WebSocketReceiveResult result =
                socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token).GetAwaiter().GetResult();
            string hello = Encoding.UTF8.GetString(buffer, 0, result.Count);
            bool isHello = hello.Contains("\"op\":10");
            return isHello
                ? $"ok — HELLO received ({result.Count} bytes)"
                : $"connected but no HELLO ({result.Count} bytes)";
        } catch(Exception e) {
            return "FAILED — " + Describe(e);
        } finally {
            if(socket != null) {
                try {
                    if(socket.State == WebSocketState.Open)
                        socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "selftest", CancellationToken.None)
                            .GetAwaiter().GetResult();
                } catch(Exception e) {
                    Diag.Ignore(e);
                }
                socket.Dispose();
            }
        }
    }
    private static string TestCrypto() {
        try {
            using RSA rsa = RsaKeys.Create2048();
            int bits = RsaKeys.Bits(rsa);
            if(bits != RsaKeys.RequiredBits)
                return $"FAILED — got an RSA-{bits} key, Discord requires RSA-{RsaKeys.RequiredBits}";
            byte[] spki = Der.SubjectPublicKeyInfo(rsa.ExportParameters(false));
            if(spki.Length == 0 || spki[0] != 0x30) return "FAILED — the public key did not encode as DER";
            byte[] sample = Encoding.UTF8.GetBytes("quartz-remote-auth-probe");
            byte[] wrapped = RsaOaep.EncryptSha256(rsa, sample);
            byte[] opened = RsaOaep.DecryptSha256Managed(rsa, wrapped);
            if(Encoding.UTF8.GetString(opened) != "quartz-remote-auth-probe")
                return "FAILED — OAEP-SHA256 round trip did not match";
            byte[] keys = TokenBox.NewKeyMaterial();
            if(TokenBox.Unprotect(keys, TokenBox.Protect(keys, "probe")) != "probe")
                return "FAILED — the token store round trip did not match";
            return $"ok — RSA-{bits} OAEP-SHA256 and the token store both work ({spki.Length}-byte SPKI)";
        } catch(Exception e) {
            return "FAILED — " + Describe(e);
        }
    }
    private static string TestNative() {
        IntPtr handle = IntPtr.Zero;
        try {
            string rid = VoiceNatives.Rid();
            string library = NativeLib.SystemLibrary();
            handle = NativeLib.Load(library);
            if(handle == IntPtr.Zero) return $"FAILED — could not dlopen {library}";
            IntPtr symbol = NativeLib.Symbol(handle, NativeLib.SystemSymbol());
            if(symbol == IntPtr.Zero)
                return $"FAILED — opened {library} but could not resolve {NativeLib.SystemSymbol()}";
            return $"ok — native loading works, platform {rid ?? "unsupported"}";
        } catch(Exception e) {
            return "FAILED — " + Describe(e);
        } finally {
            NativeLib.Free(handle);
        }
    }
    private static string Describe(Exception e) {
        Exception inner = e;
        while(inner.InnerException != null) inner = inner.InnerException;
        return inner.GetType().Name + ": " + inner.Message;
    }
}
