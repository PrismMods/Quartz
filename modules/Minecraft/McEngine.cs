#nullable enable
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Quartz.Core;
using VoltRpc.Communication.Syncing;
using VoltRpc.Communication.TCP;
using VoltstroStudios.UnityWebBrowser.Shared;
using VoltstroStudios.UnityWebBrowser.Shared.Core;
using VoltstroStudios.UnityWebBrowser.Shared.Popups;
using VoltstroStudios.UnityWebBrowser.Shared.Events;
namespace Quartz.Features.Minecraft;
public sealed class McEngine : IDisposable {
    private const int ConnectTimeoutMs = 30_000;
    private const int ConnectAttemptMs = 1_000;
    private const int RetryDelayMs = 250;
    private readonly string dataRoot;
    private readonly object lifecycle = new();
    private Process? process;
    private TCPClient? ipcClient;
    private IEngineControls? engine;
    private McPixelsReader? pixels;
    private Thread? worker;
    private volatile bool running;
    private volatile bool starting;
    private volatile bool stopRequested;
    private volatile bool stopping;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool Running => running;
    public bool Starting => starting;
    public bool Stopping => stopping;
    public McEngine(string dataRoot) => this.dataRoot = dataRoot;
    // Returns immediately. Launching CEF and waiting for its IPC port costs one to
    // three seconds on a good day and the full retry budget on a bad one, and this is
    // called from OnEnable on Unity's main thread — doing it inline froze the game for
    // the whole duration every time the tab was opened. All of it happens on the
    // worker thread instead; Ready goes true once pixels can actually flow.
    public bool Start(string url, int width, int height, int frameRate) {
        lock(lifecycle) {
            if(running || starting) return true;
            // A teardown is still unwinding CEF on its own thread; the view retries.
            if(stopping) return false;
            McEngineLocation? location = McPaths.Locate(dataRoot);
            if(location == null) {
                MainCore.Log.Wrn("[Minecraft] engine is not installed.");
                return false;
            }
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            stopRequested = false;
            starting = true;
            worker = new Thread(() => RunLoop(url, frameRate, location.Value)) {
                IsBackground = true,
                Name = "Quartz-Minecraft-Engine"
            };
            worker.Start();
            return true;
        }
    }
    private void RunLoop(string url, int frameRate, McEngineLocation location) {
        TCPClient? client = null;
        try {
            int inPort = FreePort();
            int outPort = FreePort();
            if(!Launch(location, url, frameRate, inPort, outPort)) return;
            McPixelsReader reader = new();
            client = ConnectWithRetry(inPort, reader);
            if(client == null || stopRequested) return;
            lock(lifecycle) {
                if(stopRequested) return;
                ipcClient = client;
                pixels = reader;
                engine = new McEngineProxy(client);
                running = true;
            }
            client = null;
            // GetPixels is a poll, and at 720p each call drags ~3.7MB back over the
            // socket while holding the RPC lock. Spinning it flat out burned a core,
            // saturated loopback and starved input calls of the lock. Pace it to the
            // frame rate the engine is actually rendering at.
            int interval = Math.Max(1, 1000 / Math.Clamp(frameRate, 1, 60));
            Stopwatch clock = new();
            while(!stopRequested) {
                clock.Restart();
                try {
                    engine?.GetPixels();
                } catch(Exception e) {
                    Diag.Ignore(e);
                    if(!stopRequested) Thread.Sleep(100);
                    continue;
                }
                int spent = (int)clock.ElapsedMilliseconds;
                if(spent < interval && !stopRequested) Thread.Sleep(interval - spent);
            }
        } catch(Exception e) {
            MainCore.Log.Err("[Minecraft] engine worker failed: " + e.Message);
        } finally {
            if(client != null) try { client.Dispose(); } catch(Exception e) { Diag.Ignore(e); }
            starting = false;
        }
    }
    // The engine needs about a second of CEF startup before it binds its IPC port, and
    // a connection to a port nobody is listening on is REFUSED immediately rather than
    // waiting out the connect timeout — so a single attempt always loses the race.
    private TCPClient? ConnectWithRetry(int inPort, McPixelsReader reader) {
        int waited = 0;
        while(waited < ConnectTimeoutMs && !stopRequested) {
            if(process != null && process.HasExited) {
                MainCore.Log.Err("[Minecraft] engine exited before its IPC port opened.");
                return null;
            }
            TCPClient candidate = new(new IPEndPoint(IPAddress.Loopback, inPort), ConnectAttemptMs);
            try {
                if(!McReadWriters.AddBaseTypeReadWriters(candidate.TypeReaderWriterManager)) {
                    candidate.Dispose();
                    return null;
                }
                candidate.TypeReaderWriterManager.AddType(reader);
                // Services must be registered before Connect: that is when VoltRpc syncs
                // the contract. The engine hosts BOTH of these on its in-port, and
                // registering only one fails the sync on service count.
                candidate.AddService<IEngineControls>();
                candidate.AddService<IPopupClientControls>();
                candidate.Connect();
                return candidate;
            } catch(SyncServiceMissMatchException e) {
                // Deterministic: the re-declared contract disagrees with the engine's.
                // Retrying cannot help, and would burn the whole connect budget.
                try { candidate.Dispose(); } catch(Exception d) { Diag.Ignore(d); }
                MainCore.Log.Err("[Minecraft] engine IPC contract mismatch: " + e.Message);
                return null;
            } catch(Exception e) {
                Diag.Ignore(e);
                try { candidate.Dispose(); } catch(Exception d) { Diag.Ignore(d); }
                Thread.Sleep(RetryDelayMs);
                waited += RetryDelayMs;
            }
        }
        if(!stopRequested) MainCore.Log.Err("[Minecraft] engine IPC did not accept a connection in time.");
        return null;
    }
    private bool Launch(McEngineLocation location, string url, int frameRate, int inPort, int outPort) {
        string cache = McPaths.CachePath(dataRoot);
        try { Directory.CreateDirectory(cache); } catch(Exception e) { Diag.Ignore(e); }
        string args = string.Format(CultureInfo.InvariantCulture, "-initial-url \"{0}\" -width {1} -height {2} -javascript -local-storage -windowless-frame-rate {3} -comms-layer-name TCP -in-location {4} -out-location {5} -popup-action Ignore -cache-path \"{6}\"", url, Width, Height, Math.Clamp(frameRate, 1, 60), inPort, outPort, cache);
        ProcessStartInfo info = new(location.Executable, args) {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // CefGlue resolves the CEF framework relative to the process working
            // directory, not the executable, so this must be the binary's own folder.
            WorkingDirectory = location.WorkingDirectory
        };
        process = Process.Start(info);
        if(process == null) {
            MainCore.Log.Err("[Minecraft] engine process failed to start.");
            return false;
        }
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, e) => {
            if(!string.IsNullOrEmpty(e.Data)) MainCore.Log.Wrn("[Minecraft] engine: " + e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return true;
    }
    public int FrameLength => pixels?.Length ?? 0;
    public bool TryApplyFrame(Action<byte[]> upload) => pixels?.TryApply(upload) ?? false;
    public void LoadUrl(string url) {
        if(!running) return;
        try { engine?.LoadUrl(url); } catch(Exception e) { Diag.Ignore(e); }
    }
    public void Resize(int width, int height) {
        if(width <= 0 || height <= 0) return;
        Width = width;
        Height = height;
        if(!running) return;
        try { engine?.Resize(new Resolution((uint)width, (uint)height)); }
        catch(Exception e) { Diag.Ignore(e); }
    }
    public void SendMouseMove(int x, int y) {
        if(!running) return;
        try { engine?.SendMouseMoveEvent(new MouseMoveEvent { MouseX = x, MouseY = y }); }
        catch(Exception e) { Diag.Ignore(e); }
    }
    public void SendMouseClick(int x, int y, int count, MouseClickType type, MouseEventType eventType) {
        if(!running) return;
        try {
            engine?.SendMouseClickEvent(new MouseClickEvent {
                MouseX = x,
                MouseY = y,
                MouseClickCount = count,
                MouseClickType = type,
                MouseEventType = eventType
            });
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public void SendMouseScroll(int x, int y, int delta) {
        if(!running) return;
        try { engine?.SendMouseScrollEvent(new MouseScrollEvent { MouseX = x, MouseY = y, MouseScroll = delta }); }
        catch(Exception e) { Diag.Ignore(e); }
    }
    public void SendKeyboard(WindowsKey[] down, WindowsKey[] up, string chars) {
        if(!running) return;
        try {
            engine?.SendKeyboardEvent(new KeyboardEvent {
                KeysDown = down,
                KeysUp = up,
                Chars = chars.ToCharArray()
            });
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public void AudioMute(bool mute) {
        if(!running) return;
        try { engine?.AudioMute(mute); } catch(Exception e) { Diag.Ignore(e); }
    }
    public void ExecuteJs(string js) {
        if(!running) return;
        try { engine?.ExecuteJs(js); } catch(Exception e) { Diag.Ignore(e); }
    }
    // Teardown costs about two seconds — joining the worker, the Shutdown round trip
    // and reaping CEF's process tree. OnDisable runs on Unity's main thread, so doing
    // that inline froze the game every time the menu closed. Hand it to a thread and
    // return; pass wait: true only on quit, where the process really must be gone
    // before the runtime tears down under us.
    public void Stop() => Stop(false);
    public void Stop(bool wait) {
        if(wait) {
            lock(lifecycle) StopLocked();
            return;
        }
        stopRequested = true;
        running = false;
        if(stopping) return;
        stopping = true;
        new Thread(() => {
            try { lock(lifecycle) StopLocked(); } catch(Exception e) { Diag.Ignore(e); } finally { stopping = false; }
        }) { IsBackground = true, Name = "Quartz-Minecraft-Teardown" }.Start();
    }
    private void StopLocked() {
        stopRequested = true;
        running = false;
        // Join the worker BEFORE any further RPC: it may be blocked in GetPixels, and a
        // second concurrent call on the same socket corrupts the stream.
        try { worker?.Join(4000); } catch(Exception e) { Diag.Ignore(e); }
        worker = null;
        // Then ask CEF to shut down: it owns six or more child processes, and a bare
        // parent kill orphans them, which is exactly the gameplay cost this avoids.
        try { engine?.Shutdown(); } catch(Exception e) { Diag.Ignore(e); }
        try { ipcClient?.Dispose(); } catch(Exception e) { Diag.Ignore(e); }
        ipcClient = null;
        engine = null;
        pixels = null;
        starting = false;
        KillProcess();
    }
    private void KillProcess() {
        Process? current = process;
        process = null;
        if(current == null) return;
        try {
            if(!current.WaitForExit(2000)) {
                KillTree(current.Id);
                current.Kill();
            }
        } catch(Exception e) { Diag.Ignore(e); }
        try { current.Dispose(); } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void KillTree(int pid) {
        if(Environment.OSVersion.Platform == PlatformID.Win32NT) {
            RunSilent("taskkill", $"/PID {pid} /T /F");
            return;
        }
        RunSilent("/bin/sh", $"-c \"pkill -TERM -P {pid}\"");
    }
    private static void RunSilent(string file, string args) {
        try {
            using Process? killer = Process.Start(new ProcessStartInfo(file, args) {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            killer?.WaitForExit(2000);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static int FreePort() {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
    public void Dispose() => Stop(true);
}
