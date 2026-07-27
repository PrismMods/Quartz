using System.Net;
using Quartz.Async;
using Quartz.Core;
using Quartz.IO;
using Quartz.Net;
namespace Quartz.Modules;
public enum CatalogSource {
    None,
    Network,
    Cache,
    Embedded,
}
public static class ModuleCatalogService {
    private const string CacheFileName = "catalog.json";
    private const string EmbeddedResource = "Quartz.Modules.Catalog.Default.json";
    private static readonly HttpClient http = CreateClient();
    public static ModuleCatalog Catalog { get; private set; }
    public static CatalogSource Source { get; private set; } = CatalogSource.None;
    public static string Error { get; private set; }
    public static bool Busy { get; private set; }
    public static event Action OnChanged;
    private static HttpClient CreateClient() {
        try {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        } catch(Exception e) { Diag.Ignore(e); }
        HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Modules/1.0");
        return client;
    }
    private static string CachePath => Path.Combine(MainCore.Paths.ModulePath, CacheFileName);
    private static string PointerUrl =>
        $"https://github.com/{Info.RepoOwner}/{Info.RepoName}/releases/download/latest-{Info.Channel}/modules.json";
    private static string PinnedUrl =>
        $"https://github.com/{Info.RepoOwner}/{Info.RepoName}/releases/download/v{Info.DisplayVersion}/modules.json";
    public static void EnsureLoaded() {
        if(Catalog != null) return;
        LoadOffline();
    }
    private static void LoadOffline() {
        if(TryAdopt(ReadCache(), CatalogSource.Cache)) return;
        TryAdopt(ReadEmbedded(), CatalogSource.Embedded);
    }
    public static async void Refresh() {
        if(Busy) return;
        Busy = true;
        Error = null;
        Raise();
        string body = null;
        string failure = null;
        try {
            body = await Task.Run(() => Fetch(out failure)).ConfigureAwait(false);
        } catch(Exception e) {
            failure = e.Message;
        }
        string json = body;
        string reason = failure;
        MainThread.Enqueue(() => {
            Busy = false;
            if(json != null && TryAdopt(json, CatalogSource.Network)) {
                WriteCache(json);
                Error = null;
            } else {
                Error = reason ?? MainCore.Tr.Get("MODULES_CATALOG_UNREADABLE", "The module catalog could not be read.");
                if(Catalog == null) LoadOffline();
            }
            Raise();
        });
    }
    private static string Fetch(out string failure) {
        failure = null;
        foreach(string url in new[] { PointerUrl, PinnedUrl }) {
            try {
                return Download(url);
            } catch(Exception e) {
                failure = NetworkPolicy.IsOfflineError(e)
                    ? MainCore.Tr.Get("MODULES_OFFLINE", "Offline — showing the last catalog Quartz saw.")
                    : e.Message;
            }
        }
        return null;
    }
    private static string Download(string url) {
        Uri current = new(url);
        for(int redirects = 0; redirects <= 5; redirects++) {
            NetworkPolicy.Github.EnsurePublicHostAsync(current, CancellationToken.None).GetAwaiter().GetResult();
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            using HttpResponseMessage response = http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if((int)response.StatusCode is >= 300 and < 400) {
                if(redirects == 5 || response.Headers.Location == null)
                    throw new HttpRequestException("Too many catalog redirects.");
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            long? length = response.Content.Headers.ContentLength;
            if(length is > ModuleCatalog.MaxBytes) throw new InvalidDataException("Catalog is too large.");
            byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            if(bytes.Length > ModuleCatalog.MaxBytes) throw new InvalidDataException("Catalog is too large.");
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        throw new HttpRequestException("Catalog redirect failed.");
    }
    private static bool TryAdopt(string json, CatalogSource source) {
        if(string.IsNullOrWhiteSpace(json)) return false;
        ModuleCatalog parsed = ModuleCatalog.Parse(json, out string error);
        if(parsed == null) {
            MainCore.Log.Wrn($"[Modules] {source} catalog rejected: {error}");
            return false;
        }
        if(parsed.CoreAbi != Info.ModuleAbi) {
            MainCore.Log.Wrn($"[Modules] {source} catalog targets module ABI {parsed.CoreAbi}, this build uses {Info.ModuleAbi}");
            return false;
        }
        Catalog = parsed;
        Source = source;
        return true;
    }
    private static string ReadCache() {
        try {
            return File.Exists(CachePath) ? File.ReadAllText(CachePath) : null;
        } catch {
            return null;
        }
    }
    private static void WriteCache(string json) {
        try {
            Directory.CreateDirectory(MainCore.Paths.ModulePath);
            AtomicFile.WriteAllText(CachePath, json);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Modules] couldn't cache the catalog: {e.Message}");
        }
    }
    private static string ReadEmbedded() {
        try {
            using Stream stream = typeof(ModuleCatalogService).Assembly.GetManifestResourceStream(EmbeddedResource);
            if(stream == null) return null;
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        } catch {
            return null;
        }
    }
    private static void Raise() {
        try {
            OnChanged?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] catalog listener threw: {e}");
        }
    }
}
