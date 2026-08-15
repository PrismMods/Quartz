using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.UpdateEngine;
internal sealed class UpdateManager {
    // The whole resolve — API call, download, extract — runs before the payload
    // loads, so it must give up fast enough that a dead network never holds the
    // game hostage. The API call gets the short budget; only once a newer
    // release is confirmed real is the longer download budget worth spending.
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(90);
    internal const long MaximumPackageBytes = 256L * 1024 * 1024;
    private readonly string currentVersion;
    private readonly string failedVersion;
    private readonly string runtimeRoot;
    private readonly string versionsRoot;
    public UpdateManager(UpdateRequest request) {
        currentVersion = request.CurrentVersion;
        failedVersion = request.FailedVersion;
        runtimeRoot = Path.GetFullPath(request.RuntimeRoot ?? throw new ArgumentException("RuntimeRoot is missing."));
        versionsRoot = Path.Combine(runtimeRoot, "versions");
    }
    public UpdateResult Resolve() {
        PackageInstaller.CleanupTemporaryArtifacts(runtimeRoot);
        UpdatePrefs prefs = UpdatePrefs.Load(runtimeRoot);
        if(!prefs.Enabled) return new UpdateResult { Outcome = UpdateOutcomes.None, Message = "auto-update is disabled in update.json" };
        using CancellationTokenSource timeout = new(TotalTimeout);
        Task<UpdateResult> operation = Task.Run(() => ResolveAsync(prefs, timeout.Token));
        if(Task.WhenAny(operation, Task.Delay(TotalTimeout)).GetAwaiter().GetResult() != operation) {
            timeout.Cancel();
            _ = operation.ContinueWith(
                completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            throw new TimeoutException($"the update check timed out after {TotalTimeout.TotalSeconds:0}s");
        }
        UpdateResult result = operation.GetAwaiter().GetResult();
        if(prefs.Message != null) result.Message = result.Message == null ? prefs.Message : prefs.Message + "; " + result.Message;
        return result;
    }
    private async Task<UpdateResult> ResolveAsync(UpdatePrefs prefs, CancellationToken cancellation) {
        if(!SemVer.TryParse(currentVersion, out SemVer current))
            throw new InvalidDataException("the current runtime version is unparseable: " + currentVersion);
        using HttpClient client = CreateClient();
        ReleaseAsset best = await PickReleaseAsync(client, current, prefs, cancellation).ConfigureAwait(false);
        if(best == null) return new UpdateResult { Outcome = UpdateOutcomes.None };
        string target = Path.Combine(versionsRoot, best.Version.ToString());
        if(PackageInstaller.IsValidRuntime(target, best.Version.ToString()))
            return Candidate(best, target);
        Directory.CreateDirectory(runtimeRoot);
        string packagePath = Path.Combine(runtimeRoot, "download-" + Guid.NewGuid().ToString("N") + ".zip");
        try {
            await DownloadFileAsync(client, best.Url, packagePath, cancellation).ConfigureAwait(false);
            VerifyChecksum(packagePath, best.Sha256);
            string runtimePath = PackageInstaller.Install(packagePath, runtimeRoot, best.Version.ToString());
            return Candidate(best, runtimePath);
        } finally {
            PackageInstaller.TryDeleteFile(packagePath);
        }
    }
    private static UpdateResult Candidate(ReleaseAsset best, string runtimePath) => new() {
        Outcome = UpdateOutcomes.Candidate,
        Version = best.Version.ToString(),
        RuntimePath = runtimePath,
        Message = best.Sha256 == null ? "the release asset had no digest — integrity was not verified" : null,
    };
    private sealed class ReleaseAsset {
        public SemVer Version;
        public string Url;
        public string Sha256;
    }
    private async Task<ReleaseAsset> PickReleaseAsync(
        HttpClient client, SemVer current, UpdatePrefs prefs, CancellationToken cancellation) {
        string url = $"https://api.github.com/repos/{EngineInfo.RepoOwner}/{EngineInfo.RepoName}/releases?per_page=30";
        string json = await DownloadTextAsync(client, url, 8 * 1024 * 1024, cancellation).ConfigureAwait(false);
        ReleaseAsset best = null;
        foreach(JToken release in JArray.Parse(json)) {
            if((bool?)release["draft"] == true) continue;
            string tag = (string)release["tag_name"];
            if(string.IsNullOrEmpty(tag) || tag == prefs.Skipped) continue;
            if(!SemVer.TryParse(tag, out SemVer version)) continue;
            if(version.Channel < prefs.Channel) continue;
            if(SemVer.CompareForChannel(version, current, prefs.Channel) <= 0) continue;
            if(failedVersion != null && version.ToString() == failedVersion) continue;
            if(release["assets"] is not JArray assets) continue;
            foreach(JToken asset in assets) {
                if((string)asset["name"] != EngineInfo.AssetName) continue;
                string assetUrl = (string)asset["browser_download_url"];
                if(!IsTrustedReleaseUrl(assetUrl)) continue;
                if(best == null || SemVer.CompareForChannel(version, best.Version, prefs.Channel) > 0) {
                    best = new ReleaseAsset {
                        Version = version,
                        Url = assetUrl,
                        Sha256 = ParseSha256Digest((string)asset["digest"]),
                    };
                }
            }
        }
        return best;
    }
    private static string ParseSha256Digest(string digest) {
        const string prefix = "sha256:";
        if(string.IsNullOrEmpty(digest) || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        string hex = digest.Substring(prefix.Length);
        return hex.Length == 64 ? hex.ToLowerInvariant() : null;
    }
    private static bool IsTrustedReleaseUrl(string value) {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
    }
    private static HttpClient CreateClient() {
        HttpClient client = new() { Timeout = ApiTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Bootstrap/" + EngineInfo.Version);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
    private static async Task<string> DownloadTextAsync(
        HttpClient client, string url, int maximumBytes, CancellationToken cancellation) {
        using HttpResponseMessage response = await client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using MemoryStream buffer = new();
        await CopyWithLimitAsync(stream, buffer, maximumBytes, cancellation).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
    private static async Task DownloadFileAsync(
        HttpClient client, string url, string destinationPath, CancellationToken cancellation) {
        // The client's short Timeout only bounds time-to-headers here (the body
        // streams under ResponseHeadersRead); the body is bounded by the caller's
        // total-timeout token and the byte cap.
        using HttpResponseMessage response = await client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await CopyWithLimitAsync(source, destination, MaximumPackageBytes, cancellation).ConfigureAwait(false);
    }
    private static async Task CopyWithLimitAsync(
        Stream source, Stream destination, long maximumBytes, CancellationToken cancellation) {
        byte[] buffer = new byte[81920];
        long total = 0;
        while(true) {
            int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false);
            if(read == 0) return;
            total += read;
            if(total > maximumBytes) throw new InvalidDataException("the downloaded update asset is too large");
            await destination.WriteAsync(buffer, 0, read, cancellation).ConfigureAwait(false);
        }
    }
    private static void VerifyChecksum(string path, string expected) {
        if(expected == null) return;
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        string actual = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if(!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"checksum mismatch: expected {expected}, got {actual}");
    }
}
