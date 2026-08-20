#nullable enable
using Quartz.Core;
namespace Quartz.Features.Minecraft;
public sealed class McInstallProgress(string stage, float fraction, long bytes, long total) {
    public string Stage { get; } = stage;
    public float Fraction { get; } = fraction;
    public long Bytes { get; } = bytes;
    public long Total { get; } = total;
}
public sealed class McInstaller : IDisposable {
    private const string StageFolder = ".quartz-mc-tmp";
    private readonly string dataRoot;
    private readonly HttpClient http;
    private CancellationTokenSource? active;
    public bool Busy { get; private set; }
    public McInstaller(string dataRoot) {
        this.dataRoot = dataRoot;
        http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Minecraft/1.0");
    }
    public void Cancel() {
        try { active?.Cancel(); } catch(Exception e) { Diag.Ignore(e); }
    }
    public async Task<bool> InstallAsync(Action<McInstallProgress>? onProgress, CancellationToken token) {
        if(Busy) return false;
        if(McPaths.PackageId() == null) {
            MainCore.Log.Wrn("[Minecraft] no CEF engine build exists for this platform and architecture.");
            return false;
        }
        Busy = true;
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        active = linked;
        string root = McPaths.InstallRoot(dataRoot);
        string stage = Path.Combine(Path.GetDirectoryName(root)!, StageFolder);
        string archive = Path.Combine(stage, "engine.tgz");
        try {
            if(Directory.Exists(stage)) Directory.Delete(stage, true);
            Directory.CreateDirectory(stage);
            onProgress?.Invoke(new McInstallProgress("download", 0f, 0, 0));
            long total = await DownloadAsync(McPaths.TarballUrl(), archive, onProgress, linked.Token).ConfigureAwait(false);
            if(total <= 0) return false;
            onProgress?.Invoke(new McInstallProgress("extract", 0f, total, total));
            string unpacked = Path.Combine(stage, "unpacked");
            McTar.ExtractGz(archive, unpacked);
            linked.Token.ThrowIfCancellationRequested();
            if(Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            Directory.Move(unpacked, root);
            File.WriteAllText(McPaths.VersionMarker(dataRoot), McPaths.PackageId() + "@" + McPaths.EngineVersion);
            McPaths.InvalidateCache();
            onProgress?.Invoke(new McInstallProgress("done", 1f, total, total));
            return McPaths.Locate(dataRoot) != null;
        } catch(OperationCanceledException) {
            MainCore.Log.Wrn("[Minecraft] engine install cancelled.");
            return false;
        } catch(Exception e) {
            MainCore.Log.Err("[Minecraft] engine install failed: " + e.Message);
            return false;
        } finally {
            try { if(Directory.Exists(stage)) Directory.Delete(stage, true); } catch(Exception e) { Diag.Ignore(e); }
            active = null;
            Busy = false;
        }
    }
    private async Task<long> DownloadAsync(string url, string destination, Action<McInstallProgress>? onProgress, CancellationToken token) {
        if(string.IsNullOrEmpty(url)) return 0;
        using HttpResponseMessage response = await http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if(!response.IsSuccessStatusCode) {
            MainCore.Log.Err($"[Minecraft] engine download returned HTTP {(int)response.StatusCode}.");
            return 0;
        }
        long declared = response.Content.Headers.ContentLength ?? 0;
        using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[81920];
        long written = 0;
        while(true) {
            int read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            if(read <= 0) break;
            await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
            written += read;
            if(declared > 0) onProgress?.Invoke(new McInstallProgress("download", (float)written / declared, written, declared));
        }
        return written;
    }
    public void Dispose() {
        Cancel();
        http.Dispose();
    }
}
