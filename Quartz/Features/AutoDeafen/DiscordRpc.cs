using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using Quartz.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Features.AutoDeafen;
internal sealed class DiscordRpc {
    private const int PollIntervalMs = 120;
    private const int PipeConnectTimeoutMs = 500;
    private const int IoTimeoutMs = 3000;
    private const int GracefulStopTimeoutMs = 400;
    private const int AbortStopTimeoutMs = 100;
    private const int MaxFrameBytes = 1024 * 1024;
    private readonly string clientId;
    private readonly string accessToken;
    private readonly AutoResetEvent wake = new(false);
    private readonly CancellationTokenSource abort = new();
    private Thread thread;
    private volatile bool running;
    private volatile bool stopRequested;
    private volatile bool desiredDeaf;
    private volatile bool ready;
    private volatile string status = "idle";
    private Stream stream;
    private readonly object connectLock = new();
    private Socket connectingSocket;
    internal string Status => status;
    internal bool Ready => ready;
    internal DiscordRpc(string clientId, string accessToken) {
        this.clientId = clientId;
        this.accessToken = accessToken;
    }
    internal void Start() {
        if(running) return;
        stopRequested = false;
        running = true;
        thread = new Thread(Run) { IsBackground = true, Name = "Quartz-DiscordRpc" };
        thread.Start();
    }
    internal void SetDeaf(bool deaf) {
        desiredDeaf = deaf;
        wake.Set();
    }
    internal void Stop() {
        desiredDeaf = false;
        stopRequested = true;
        wake.Set();
        Thread worker = thread;
        if(worker == null) {
            AbortStream();
            running = false;
            ready = false;
            return;
        }
        if(worker != Thread.CurrentThread && !Join(worker, GracefulStopTimeoutMs)) {
            try { abort.Cancel(); } catch(Exception e) { Diag.Ignore(e); }
            AbortConnect();
            AbortStream();
            if(!Join(worker, AbortStopTimeoutMs))
                MainCore.Log.Wrn("[AutoDeafen] Discord RPC worker did not stop within the bounded shutdown window");
        }
        ready = false;
    }
    private void Run() {
        bool current = false;
        try {
            status = "connecting";
            Stream connected = Connect();
            if(connected == null) {
                status = "discord not found";
                return;
            }
            Volatile.Write(ref stream, connected);
            if(stopRequested) return;
            Handshake();
            if(!TryAuthenticate()) {
                status = "authenticate failed";
                return;
            }
            ready = true;
            status = "ready";
            while(!stopRequested) {
                bool target = desiredDeaf;
                if(target != current) {
                    ApplyDeaf(target);
                    current = target;
                }
                wake.WaitOne(PollIntervalMs);
            }
            ApplyDeaf(false);
            status = "stopped";
        } catch(Exception ex) {
            if(stopRequested) {
                status = "stopped";
                Diag.Ignore(ex);
            } else {
                status = "error: " + ex.Message;
                MainCore.Log.Wrn("[AutoDeafen] discord rpc error: " + ex);
            }
        } finally {
            AbortStream();
            ready = false;
            running = false;
        }
    }
    private Stream Connect() {
        bool unix = Environment.OSVersion.Platform == PlatformID.Unix
            || Environment.OSVersion.Platform == PlatformID.MacOSX
            || (int)Environment.OSVersion.Platform == 6;
        for(int i = 0; i < 10 && !stopRequested; i++) {
            try {
                if(unix) {
                    string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
                        ?? Environment.GetEnvironmentVariable("TMPDIR")
                        ?? Environment.GetEnvironmentVariable("TMP")
                        ?? "/tmp";
                    string path = System.IO.Path.Combine(dir.TrimEnd('/'), "discord-ipc-" + i);
                    if(!File.Exists(path)) continue;
                    Socket sock = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    try {
                        lock(connectLock) {
                            if(stopRequested) {
                                sock.Dispose();
                                return null;
                            }
                            connectingSocket = sock;
                        }
                        sock.Connect(new UnixDomainSocketEndPoint(path));
                        lock(connectLock) {
                            if(ReferenceEquals(connectingSocket, sock)) connectingSocket = null;
                            if(stopRequested) {
                                sock.Dispose();
                                return null;
                            }
                        }
                        return new NetworkStream(sock, true);
                    } catch(Exception e) {
                        Diag.Ignore(e);
                        sock.Dispose();
                        throw;
                    } finally {
                        lock(connectLock) {
                            if(ReferenceEquals(connectingSocket, sock)) connectingSocket = null;
                        }
                    }
                }
                NamedPipeClientStream pipe = new(".", "discord-ipc-" + i, PipeDirection.InOut);
                try {
                    pipe.Connect(PipeConnectTimeoutMs);
                    return pipe;
                } catch(Exception e) {
                    Diag.Ignore(e);
                    pipe.Dispose();
                    throw;
                }
            } catch(Exception e) { Diag.Ignore(e); }
        }
        return null;
    }
    private void WriteFrame(int op, string json, CancellationToken token) {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        if(payload.Length > MaxFrameBytes) throw new InvalidDataException("Discord IPC frame is too large.");
        byte[] header = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(op), 0, header, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, header, 4, 4);
        Stream target = CurrentStream();
        target.WriteAsync(header, 0, header.Length, token).GetAwaiter().GetResult();
        target.WriteAsync(payload, 0, payload.Length, token).GetAwaiter().GetResult();
        target.FlushAsync(token).GetAwaiter().GetResult();
    }
    private JObject ReadFrame(out int op, CancellationToken token) {
        byte[] header = ReadExact(8, token);
        op = BitConverter.ToInt32(header, 0);
        int len = BitConverter.ToInt32(header, 4);
        if(len < 0 || len > MaxFrameBytes) throw new InvalidDataException("Discord IPC frame length is invalid.");
        byte[] payload = len > 0 ? ReadExact(len, token) : [];
        string json = Encoding.UTF8.GetString(payload);
        return string.IsNullOrEmpty(json) ? [] : JObject.Parse(json);
    }
    private byte[] ReadExact(int n, CancellationToken token) {
        byte[] buf = new byte[n];
        int off = 0;
        Stream target = CurrentStream();
        while(off < n) {
            int r = target.ReadAsync(buf, off, n - off, token).GetAwaiter().GetResult();
            if(r <= 0) throw new IOException("ipc closed");
            off += r;
        }
        return buf;
    }
    private void Handshake() {
        using CancellationTokenSource timeout = NewIoTimeout();
        using CancellationTokenRegistration close = timeout.Token.Register(AbortStream);
        try {
            WriteFrame(0, JsonConvert.SerializeObject(new { v = 1, client_id = clientId }), timeout.Token);
            ReadFrame(out _, timeout.Token);
        } catch(Exception e) when(IsIoTimeout(timeout)) {
            throw new TimeoutException("Discord IPC handshake timed out.", e);
        }
    }
    private JObject Command(string cmd, object args) {
        string nonce = Guid.NewGuid().ToString();
        using CancellationTokenSource timeout = NewIoTimeout();
        using CancellationTokenRegistration close = timeout.Token.Register(AbortStream);
        try {
            WriteFrame(1, JsonConvert.SerializeObject(new { cmd, args, nonce }), timeout.Token);
            while(true) {
                JObject msg = ReadFrame(out int op, timeout.Token);
                if(op == 3) {
                    WriteFrame(4, msg.ToString(Formatting.None), timeout.Token);
                    continue;
                }
                if(msg.Value<string>("nonce") == nonce) return msg;
            }
        } catch(Exception e) when(IsIoTimeout(timeout)) {
            throw new TimeoutException($"Discord IPC command '{cmd}' timed out.", e);
        }
    }
    private bool TryAuthenticate() {
        if(string.IsNullOrEmpty(accessToken)) return false;
        try {
            JObject r = Command("AUTHENTICATE", new { access_token = accessToken });
            JToken data = r["data"];
            return data != null && data["user"] != null;
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    private void ApplyDeaf(bool deaf) => Command("SET_VOICE_SETTINGS", new { deaf });
    private CancellationTokenSource NewIoTimeout() {
        CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(abort.Token);
        timeout.CancelAfter(IoTimeoutMs);
        return timeout;
    }
    private bool IsIoTimeout(CancellationTokenSource timeout) =>
        timeout.IsCancellationRequested && !abort.IsCancellationRequested;
    private Stream CurrentStream() => Volatile.Read(ref stream) ?? throw new IOException("ipc closed");
    private void AbortConnect() {
        Socket pending;
        lock(connectLock) {
            pending = connectingSocket;
            connectingSocket = null;
        }
        try { pending?.Dispose(); } catch(Exception e) { Diag.Ignore(e); }
    }
    private void AbortStream() {
        Stream current = Interlocked.Exchange(ref stream, null);
        try { current?.Dispose(); } catch(Exception e) { Diag.Ignore(e); }
    }
    private static bool Join(Thread worker, int milliseconds) {
        try { return !worker.IsAlive || worker.Join(milliseconds); }
        catch(Exception e) { Diag.Ignore(e); return false; }
    }
}
