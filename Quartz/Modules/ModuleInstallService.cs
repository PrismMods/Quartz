using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Quartz.Async;
using Quartz.Core;
using Quartz.Net;
namespace Quartz.Modules;
public static class ModuleInstallService {
    private static readonly HttpClient http = CreateClient();
    private static readonly SemaphoreSlim oneAtATime = new(1, 1);
    public static string ActiveId { get; private set; }
    public static float Progress { get; private set; } = -1f;
    public static string Error { get; private set; }
    public static event Action OnChanged;
    public static bool Busy => ActiveId != null;
    private static HttpClient CreateClient() {
        try {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        } catch(Exception e) { Diag.Ignore(e); }
        HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Modules/1.0");
        return client;
    }
    public static List<string> PlanInstall(string id) {
        ModuleCatalog catalog = ModuleCatalogService.Catalog;
        if(catalog == null) return [];
        List<string> plan = [];
        foreach(string needed in catalog.ResolveWithDeps(id)) {
            if(needed != id && ModuleService.Find(needed) is { Loaded: true }) continue;
            plan.Add(needed);
        }
        return plan;
    }
    public static void Install(string id) => InstallAll([id]);
    public static async void InstallAll(IReadOnlyList<string> ids) {
        if(Busy || ids == null || ids.Count == 0) return;
        ModuleCatalog catalog = ModuleCatalogService.Catalog;
        if(catalog == null) return;
        List<string> plan = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach(string wanted in ids)
            foreach(string needed in PlanInstall(wanted))
                if(seen.Add(needed)) plan.Add(needed);
        if(plan.Count == 0) return;
        ActiveId = plan[0];
        Progress = 0f;
        Error = null;
        Raise();
        string failure = null;
        string bundle = null;
        await oneAtATime.WaitAsync().ConfigureAwait(false);
        try {
            if(catalog.HasBundle) {
                try {
                    bundle = await Task.Run(() => FetchBundle(catalog, fraction => Report(0, 2, fraction)))
                        .ConfigureAwait(false);
                } catch(Exception e) {
                    failure = Reason(e);
                }
            }
            for(int i = 0; failure == null && i < plan.Count; i++) {
                ModuleCatalogEntry entry = catalog.Find(plan[i]);
                if(entry == null) {
                    failure = $"'{plan[i]}' is not in the catalog";
                    break;
                }
                ActiveId = plan[i];
                string source = bundle;
                int steps = source == null ? plan.Count : plan.Count * 2;
                int step = source == null ? i : plan.Count + i;
                try {
                    await Task.Run(() => Fetch(entry, source, fraction => Report(step, steps, fraction)))
                        .ConfigureAwait(false);
                } catch(Exception e) {
                    failure = Reason(e);
                    break;
                }
            }
        } finally {
            oneAtATime.Release();
            Delete(bundle);
        }
        string reason = failure;
        List<string> installed = plan;
        MainThread.Enqueue(() => {
            ActiveId = null;
            Progress = -1f;
            Error = reason;
            if(reason == null) {
                foreach(string moduleId in installed) {
                    ModuleState.Entry entry = ModuleService.State.For(moduleId);
                    entry.Enabled = true;
                    entry.Source = "catalog";
                }
                ModuleService.State.Save();
                MainCore.Log.Msg($"[Modules] installed {string.Join(", ", installed)}");
                ModuleService.LoadInstalled(installed);
            } else {
                MainCore.Log.Err($"[Modules] install of '{string.Join(", ", ids)}' failed: {reason}");
            }
            Raise();
        });
    }
    private static void Report(int index, int count, float fraction) {
        float value = (index + Math.Max(0f, fraction)) / Math.Max(1, count);
        if(Math.Abs(value - Progress) < 0.01f) return;
        Progress = value;
        MainThread.Enqueue(Raise);
    }
    private static string Reason(Exception e) => NetworkPolicy.IsOfflineError(e)
        ? MainCore.Tr.Get("MODULES_INSTALL_OFFLINE", "Couldn't reach GitHub — check your connection.")
        : e.Message;
    private static string Stage() {
        string stage = Path.Combine(MainCore.Paths.TempPath, "Module");
        Directory.CreateDirectory(stage);
        return stage;
    }
    private static string FetchBundle(ModuleCatalog catalog, Action<float> progress) {
        string path = Path.Combine(Stage(), "QuartzModules.zip.part");
        Delete(path);
        Download(catalog.BundleUrl, path, catalog.BundleSize > 0 ? catalog.BundleSize : 0, progress);
        Verify(path, catalog.BundleSha256, "module bundle");
        return path;
    }
    private static void Extract(ZipArchive archive, string name, string path, long cap) {
        ZipArchiveEntry found = null;
        foreach(ZipArchiveEntry candidate in archive.Entries) {
            if(!string.Equals(candidate.FullName, name, StringComparison.Ordinal)) continue;
            found = candidate;
            break;
        }
        if(found == null) throw new InvalidDataException($"the module bundle has no '{name}'");
        long limit = cap > 0 ? cap : ModuleCatalog.MaxBundleBytes;
        if(found.Length > limit) throw new InvalidDataException($"'{name}' is larger than the catalog declared.");
        using Stream input = found.Open();
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536);
        byte[] buffer = new byte[65536];
        long total = 0;
        while(true) {
            int read = input.Read(buffer, 0, buffer.Length);
            if(read == 0) break;
            total += read;
            if(total > limit) throw new InvalidDataException($"'{name}' is larger than the catalog declared.");
            output.Write(buffer, 0, read);
        }
    }
    private static void Fetch(ModuleCatalogEntry entry, string bundle, Action<float> progress) {
        if(entry.CoreAbi != Info.ModuleAbi)
            throw new InvalidDataException($"'{entry.Id}' targets module ABI {entry.CoreAbi}, this Quartz uses {Info.ModuleAbi}.");
        string root = MainCore.Paths.ModulePath;
        Directory.CreateDirectory(root);
        string stage = Stage();
        string binaryPart = Path.Combine(stage, entry.Id + ".qmod.part");
        string manifestPart = Path.Combine(stage, entry.Id + ".qmod.json.part");
        try {
            Delete(binaryPart);
            Delete(manifestPart);
            if(bundle != null) {
                using ZipArchive archive = ZipFile.OpenRead(bundle);
                Extract(archive, entry.Id + ModuleService.ManifestExtension, manifestPart, ModuleManifest.MaxBytes);
                Extract(archive, entry.Id + ModuleService.ModuleExtension, binaryPart, entry.Size);
                progress?.Invoke(1f);
            } else if(string.IsNullOrWhiteSpace(entry.Url) || string.IsNullOrWhiteSpace(entry.ManifestUrl)) {
                throw new InvalidDataException($"the catalog offers no download for '{entry.Id}'.");
            } else {
                Download(entry.ManifestUrl, manifestPart, ModuleManifest.MaxBytes, null);
                Download(entry.Url, binaryPart, entry.Size > 0 ? entry.Size : 0, progress);
            }
            Verify(manifestPart, entry.ManifestSha256, entry.Id + " manifest");
            Verify(binaryPart, entry.Sha256, entry.Id);
            ModuleManifest manifest = ModuleManifest.Parse(File.ReadAllText(manifestPart), out string manifestError);
            if(manifest == null) throw new InvalidDataException($"downloaded manifest is unusable: {manifestError}");
            if(manifest.Id != entry.Id) throw new InvalidDataException("downloaded manifest is for a different module");
            Replace(binaryPart, Path.Combine(root, entry.Id + ModuleService.ModuleExtension));
            Replace(manifestPart, Path.Combine(root, entry.Id + ModuleService.ManifestExtension));
        } finally {
            Delete(binaryPart);
            Delete(manifestPart);
        }
    }
    private static void Download(string url, string path, long declared, Action<float> progress) {
        Uri current = new(url);
        for(int redirects = 0; redirects <= 5; redirects++) {
            NetworkPolicy.Github.EnsurePublicHostAsync(current, CancellationToken.None).GetAwaiter().GetResult();
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            using HttpResponseMessage response = http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if((int)response.StatusCode is >= 300 and < 400) {
                if(redirects == 5 || response.Headers.Location == null)
                    throw new HttpRequestException("Too many download redirects.");
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            long? length = response.Content.Headers.ContentLength;
            long cap = declared > 0 ? declared : 32L * 1024 * 1024;
            if(length is > 0 && length.Value > cap)
                throw new InvalidDataException("Download is larger than the catalog declared.");
            using Stream input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536);
            byte[] buffer = new byte[65536];
            long total = 0;
            while(true) {
                int read = input.Read(buffer, 0, buffer.Length);
                if(read == 0) break;
                total += read;
                if(total > cap) throw new InvalidDataException("Download sent more data than it declared.");
                output.Write(buffer, 0, read);
                if(length is > 0) progress?.Invoke(Math.Min(1f, (float)total / length.Value));
            }
            if(declared > 0 && total != declared)
                throw new InvalidDataException("Download size does not match the catalog.");
            return;
        }
        throw new HttpRequestException("Download redirect failed.");
    }
    private static void Verify(string path, string expected, string what) {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if(!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"checksum mismatch for {what}");
    }
    private static void Replace(string from, string to) {
        Delete(to);
        File.Move(from, to);
    }
    private static void Delete(string path) {
        try {
            if(File.Exists(path)) File.Delete(path);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void Raise() {
        try {
            OnChanged?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] install listener threw: {e}");
        }
    }
}
