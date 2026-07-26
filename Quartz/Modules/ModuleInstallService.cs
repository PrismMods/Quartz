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
        } catch {
        }
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
    public static async void Install(string id) {
        if(Busy) return;
        ModuleCatalog catalog = ModuleCatalogService.Catalog;
        if(catalog == null) return;
        List<string> plan = PlanInstall(id);
        if(plan.Count == 0) return;
        ActiveId = id;
        Progress = 0f;
        Error = null;
        Raise();
        string failure = null;
        await oneAtATime.WaitAsync().ConfigureAwait(false);
        try {
            for(int i = 0; i < plan.Count; i++) {
                ModuleCatalogEntry entry = catalog.Find(plan[i]);
                if(entry == null) {
                    failure = $"'{plan[i]}' is not in the catalog";
                    break;
                }
                int index = i;
                try {
                    await Task.Run(() => Fetch(entry, fraction => Report(index, plan.Count, fraction)))
                        .ConfigureAwait(false);
                } catch(Exception e) {
                    failure = NetworkPolicy.IsOfflineError(e)
                        ? MainCore.Tr.Get("MODULES_INSTALL_OFFLINE", "Couldn't reach GitHub — check your connection.")
                        : e.Message;
                    break;
                }
            }
        } finally {
            oneAtATime.Release();
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
                MainCore.Log.Err($"[Modules] install of '{id}' failed: {reason}");
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
    private static void Fetch(ModuleCatalogEntry entry, Action<float> progress) {
        if(entry.CoreAbi != Info.ModuleAbi)
            throw new InvalidDataException($"'{entry.Id}' targets module ABI {entry.CoreAbi}, this Quartz uses {Info.ModuleAbi}.");
        string root = MainCore.Paths.ModulePath;
        Directory.CreateDirectory(root);
        string stage = Path.Combine(MainCore.Paths.TempPath, "Module");
        Directory.CreateDirectory(stage);
        string binaryPart = Path.Combine(stage, entry.Id + ".qmod.part");
        string manifestPart = Path.Combine(stage, entry.Id + ".qmod.json.part");
        try {
            Delete(binaryPart);
            Delete(manifestPart);
            Download(entry.ManifestUrl, manifestPart, ModuleManifest.MaxBytes, null);
            Verify(manifestPart, entry.ManifestSha256, entry.Id + " manifest");
            Download(entry.Url, binaryPart, entry.Size > 0 ? entry.Size : 0, progress);
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
        } catch {
        }
    }
    private static void Raise() {
        try {
            OnChanged?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] install listener threw: {e}");
        }
    }
}
