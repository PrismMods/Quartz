using System.IO.Compression;
using System.Runtime.InteropServices;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class VoiceNatives {
    private const string ManifestResource = "Quartz.Features.Discord.voice-natives.json";
    private const string StageFolder = ".quartz-voice-tmp";
    public static string Rid() {
        bool arm = RuntimeInformation.OSArchitecture == Architecture.Arm64;
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return arm ? "win-arm64" : "win-x64";
        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return arm ? "osx-arm64" : "osx-x64";
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return arm ? "linux-arm64" : "linux-x64";
        return null;
    }
    public static string InstallRoot => Path.Combine(MainCore.Paths.RootPath, "Discord", "Voice");
    private static string MarkerPath => Path.Combine(InstallRoot, ".voice-version");
    public static string InstalledVersion {
        get {
            try {
                return File.Exists(MarkerPath) ? File.ReadAllText(MarkerPath).Trim() : null;
            } catch(Exception e) {
                Diag.Ignore(e);
                return null;
            }
        }
    }
    public static bool IsInstalled => InstalledVersion != null;
    public static string ManifestJson() {
        try {
            using Stream stream = typeof(VoiceNatives).Assembly.GetManifestResourceStream(ManifestResource);
            if(stream == null) return null;
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public static VoiceNativeEntry Entry() {
        string rid = Rid();
        string json = ManifestJson();
        return rid == null || json == null ? null : VoiceManifest.Parse(json, rid);
    }
    public static string Locate(string library) {
        VoiceNativeEntry entry = Entry();
        if(entry == null) return null;
        foreach(VoiceNativePackage package in entry.Packages) {
            if(package.Name != library) continue;
            string path = Path.Combine(InstallRoot, package.File);
            return File.Exists(path) ? path : null;
        }
        return null;
    }
    public static bool DaveAvailable => Entry()?.Has("dave") ?? false;
    public static async Task<bool> InstallAsync(Action<string, float> progress, CancellationToken ct) {
        VoiceNativeEntry entry = Entry();
        if(entry == null) {
            MainCore.Log.Wrn($"[Discord] no voice runtime is published for {Rid() ?? "this platform"}.");
            return false;
        }
        string stage = Path.Combine(Path.GetDirectoryName(InstallRoot) ?? InstallRoot, StageFolder);
        try {
            if(Directory.Exists(stage)) Directory.Delete(stage, true);
            Directory.CreateDirectory(stage);
            Directory.CreateDirectory(InstallRoot);
            using HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Discord/1.0");
            for(int i = 0; i < entry.Packages.Count; i++) {
                VoiceNativePackage package = entry.Packages[i];
                float slice = 1f / entry.Packages.Count;
                float start = i * slice;
                string archive = Path.Combine(stage, package.Name + ".nupkg");
                progress?.Invoke($"downloading {package.Name}", start);
                if(!await DownloadAsync(http, package.Url, archive, package.Name, start, slice, progress, ct))
                    return false;
                progress?.Invoke($"verifying {package.Name}", start + (slice * 0.85f));
                string actual;
                using(FileStream stream = File.OpenRead(archive)) actual = VoiceManifest.HashOf(stream);
                if(actual != package.Sha256) {
                    MainCore.Log.Err(
                        $"[Discord] {package.Name} did not match its pinned checksum "
                        + $"(expected {package.Sha256}, got {actual}) — refusing to install it.");
                    return false;
                }
                progress?.Invoke($"extracting {package.Name}", start + (slice * 0.95f));
                if(!Extract(archive, package, InstallRoot)) return false;
                File.Delete(archive);
                ct.ThrowIfCancellationRequested();
            }
            File.WriteAllText(MarkerPath, entry.Rid + "@" + entry.Version);
            progress?.Invoke("done", 1f);
            return true;
        } catch(OperationCanceledException e) {
            Diag.Ignore(e);
            return false;
        } catch(Exception e) {
            MainCore.Log.Err("[Discord] the voice runtime install failed: " + e.Message);
            return false;
        } finally {
            try {
                if(Directory.Exists(stage)) Directory.Delete(stage, true);
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        }
    }
    private static bool Extract(string archive, VoiceNativePackage package, string destination) {
        using ZipArchive zip = ZipFile.OpenRead(archive);
        ZipArchiveEntry found = null;
        foreach(ZipArchiveEntry candidate in zip.Entries)
            if(string.Equals(candidate.FullName, package.Entry, StringComparison.OrdinalIgnoreCase)) {
                found = candidate;
                break;
            }
        if(found == null) {
            MainCore.Log.Err($"[Discord] {package.Name} has no '{package.Entry}' inside its package.");
            return false;
        }
        string target = Path.Combine(destination, package.File);
        using Stream input = found.Open();
        using FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        return true;
    }
    private static async Task<bool> DownloadAsync(
        HttpClient http, string url, string destination, string name,
        float start, float slice, Action<string, float> progress, CancellationToken ct
    ) {
        using HttpResponseMessage response =
            await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if(!response.IsSuccessStatusCode) {
            MainCore.Log.Err($"[Discord] downloading {name} returned HTTP {(int)response.StatusCode}.");
            return false;
        }
        long declared = response.Content.Headers.ContentLength ?? 0;
        using Stream input = await response.Content.ReadAsStreamAsync();
        using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[81920];
        long written = 0;
        while(true) {
            int read = await input.ReadAsync(buffer, 0, buffer.Length, ct);
            if(read <= 0) break;
            await output.WriteAsync(buffer, 0, read, ct);
            written += read;
            if(declared > 0)
                progress?.Invoke($"downloading {name}", start + (slice * 0.85f * written / declared));
        }
        return written > 0;
    }
    public static void Uninstall() {
        try {
            if(Directory.Exists(InstallRoot)) Directory.Delete(InstallRoot, true);
        } catch(Exception e) {
            MainCore.Log.Wrn("[Discord] could not remove the voice runtime: " + e.Message);
        }
    }
}
