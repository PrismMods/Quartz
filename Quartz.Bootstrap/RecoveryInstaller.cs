using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Quartz.Bootstrap;
// Last-resort repair for an install whose runtime store is gone or gutted —
// third-party installers have been seen extracting the mod folder without its
// Runtime/ subtree. The bootstrap knows exactly which release it shipped in,
// so it re-downloads that release's own install zip from its deterministic
// URL and restores just the versioned runtime out of it. Deliberately not the
// update engine: no release scan, no channel logic, never a different version
// than the one this DLL was packaged with.
public static class RecoveryInstaller {
    private const long MaximumPackageBytes = 256L * 1024 * 1024;
    private const long MaximumExtractedBytes = 512L * 1024 * 1024;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(120);
    public static bool TryRestore(string runtimeRoot, Action<string> msg, Action<string> warn) {
        string url = $"https://github.com/{BootstrapInfo.RepoOwner}/{BootstrapInfo.RepoName}"
            + $"/releases/download/v{BootstrapInfo.Version}/{BootstrapInfo.AssetName}";
        string packagePath = Path.Combine(runtimeRoot, "recovery-" + Guid.NewGuid().ToString("N") + ".zip");
        try {
            msg($"restoring the {BootstrapInfo.Version} runtime from {url}");
            Directory.CreateDirectory(runtimeRoot);
            Download(url, packagePath);
            InstallArchive(packagePath, runtimeRoot);
            msg("the runtime was restored");
            return true;
        } catch(Exception e) {
            warn("the runtime could not be restored: " + e.Message);
            return false;
        } finally {
            try {
                if(File.Exists(packagePath)) File.Delete(packagePath);
            } catch(Exception cleanup) {
                warn("could not remove the recovery download: " + cleanup.Message);
            }
        }
    }
    public static string InstallArchive(string packagePath, string runtimeRoot) {
        string versionsRoot = Path.Combine(runtimeRoot, "versions");
        string staging = Path.Combine(runtimeRoot, "recovery-extract-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(versionsRoot, BootstrapInfo.Version);
        string prefix = BootstrapInfo.ZipRuntimeRel + "/versions/" + BootstrapInfo.Version + "/";
        try {
            Directory.CreateDirectory(staging);
            string rootPrefix = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            using(FileStream package = File.OpenRead(packagePath))
            using(ZipArchive archive = new(package, ZipArchiveMode.Read)) {
                long extracted = 0;
                foreach(ZipArchiveEntry entry in archive.Entries) {
                    string name = entry.FullName.Replace('\\', '/');
                    if(!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    string relative = name.Substring(prefix.Length);
                    if(relative.Length == 0 || string.IsNullOrEmpty(entry.Name)) continue;
                    extracted = checked(extracted + entry.Length);
                    if(extracted > MaximumExtractedBytes)
                        throw new InvalidDataException("the recovery package is too large");
                    string destination = Path.GetFullPath(Path.Combine(
                        staging, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if(!destination.StartsWith(rootPrefix, StringComparison.Ordinal))
                        throw new InvalidDataException("the recovery package contains an unsafe path");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? staging);
                    using Stream source = entry.Open();
                    using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    source.CopyTo(output);
                }
            }
            if(!File.Exists(Path.Combine(staging, BootstrapInfo.PayloadFileName))
                || !File.Exists(Path.Combine(staging, BootstrapInfo.EngineFileName))
                || !File.Exists(Path.Combine(staging, "runtime.json")))
                throw new InvalidDataException($"the recovery package has no complete runtime under {prefix}");
            Directory.CreateDirectory(versionsRoot);
            if(Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(staging, target);
            return target;
        } finally {
            try {
                if(Directory.Exists(staging)) Directory.Delete(staging, true);
            } catch(Exception e) {
                _ = e.Message;
            }
        }
    }
    private static void Download(string url, string destinationPath) {
        using CancellationTokenSource timeout = new(DownloadTimeout);
        Task operation = Task.Run(() => DownloadAsync(url, destinationPath, timeout.Token));
        if(Task.WhenAny(operation, Task.Delay(DownloadTimeout)).GetAwaiter().GetResult() != operation) {
            timeout.Cancel();
            _ = operation.ContinueWith(
                completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            throw new TimeoutException($"the recovery download timed out after {DownloadTimeout.TotalSeconds:0}s");
        }
        operation.GetAwaiter().GetResult();
    }
    private static async Task DownloadAsync(string url, string destinationPath, CancellationToken cancellation) {
        using HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Bootstrap/" + BootstrapInfo.Version);
        using HttpResponseMessage response = await client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        byte[] buffer = new byte[81920];
        long total = 0;
        while(true) {
            int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false);
            if(read == 0) return;
            total += read;
            if(total > MaximumPackageBytes) throw new InvalidDataException("the recovery download is too large");
            await destination.WriteAsync(buffer, 0, read, cancellation).ConfigureAwait(false);
        }
    }
}
