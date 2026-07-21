using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Quartz.Core.Service;
public static class LangUpdateService {
    private const int MaxFileBytes = 2 * 1024 * 1024;
    private const string EnglishFile = "en-US.json";
    private const string KtlKey = "0KTL";
    private const string KtlValue = "DO_NOT_TRANSLATE_THIS_KEY!";
    private static readonly string RawRoot =
        $"https://raw.githubusercontent.com/{Info.I18nRepoOwner}/{Info.I18nRepoName}/{Info.I18nBranch}";
    private static readonly string ManifestUrl = $"{RawRoot}/manifest.json";
    private static readonly HttpClient Http = CreateClient();
    private static HttpClient CreateClient() {
        try {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        } catch {
        }
        HttpClient client = new() { Timeout = System.TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Translations");
        return client;
    }
    public static async Task<int> FetchAsync(string langPath) {
        if(string.IsNullOrEmpty(langPath)) return 0;
        try {
            JObject files = await FetchManifest();
            if(files == null) return 0;
            Directory.CreateDirectory(langPath);
            int written = 0;
            foreach(JProperty entry in files.Properties()) {
                string name = entry.Name;
                string want = ((string)entry.Value)?.ToLowerInvariant();
                if(!IsAcceptableName(name) || string.IsNullOrEmpty(want) || want.Length != 64) continue;
                if(name.Equals(EnglishFile, System.StringComparison.OrdinalIgnoreCase)) continue;
                string dest = Path.Combine(langPath, name);
                if(HashFileSha256(dest) == want) continue;
                if(await FetchOne(name, want, dest)) written++;
            }
            if(written > 0) MainCore.Log.Msg($"[LangUpdate] updated {written} translation file(s)");
            return written;
        } catch(System.Exception e) {
            MainCore.Log.Wrn($"[LangUpdate] fetch failed: {e.GetType().Name}: {e.Message}");
            return 0;
        }
    }
    private static async Task<JObject> FetchManifest() {
        string json;
        using(HttpResponseMessage resp = await Http.GetAsync(ManifestUrl)) {
            if(!resp.IsSuccessStatusCode) {
                MainCore.Log.Wrn($"[LangUpdate] manifest fetch failed: HTTP {(int)resp.StatusCode} — keeping the bundled translations");
                return null;
            }
            json = await resp.Content.ReadAsStringAsync();
        }
        if(JObject.Parse(json)["files"] is JObject files) return files;
        MainCore.Log.Wrn("[LangUpdate] manifest has no 'files' map — keeping the bundled translations");
        return null;
    }
    private static bool IsAcceptableName(string name) =>
        !string.IsNullOrEmpty(name)
        && name.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)
        && name == Path.GetFileName(name)
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    private static async Task<bool> FetchOne(string name, string wantSha, string dest) {
        try {
            byte[] bytes;
            using(HttpResponseMessage resp = await Http.GetAsync($"{RawRoot}/Lang/{name}")) {
                if(!resp.IsSuccessStatusCode) return Reject(name, $"HTTP {(int)resp.StatusCode}");
                bytes = await resp.Content.ReadAsByteArrayAsync();
            }
            if(bytes.Length > MaxFileBytes) return Reject(name, $"bigger than {MaxFileBytes} bytes");
            if(HashBytesSha256(bytes) != wantSha) return Reject(name, "sha256 doesn't match the manifest");
            if(!IsUsable(bytes, name)) return false;
            string tmp = dest + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if(File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
            return true;
        } catch(System.Exception e) {
            return Reject(name, e.Message);
        }
    }
    private static bool IsUsable(byte[] bytes, string name) {
        try {
            JObject root = JObject.Parse(new UTF8Encoding(false).GetString(bytes).TrimStart('\uFEFF'));
            if(root.Count != 1) return Reject(name, "expected exactly one language block");
            if(root.Properties().First().Value is not JObject block) return Reject(name, "language block is not an object");
            if((string)block[KtlKey] != KtlValue) return Reject(name, $"missing/wrong {KtlKey} sentinel");
            return true;
        } catch(System.Exception e) {
            return Reject(name, e.Message);
        }
    }
    private static bool Reject(string name, string why) {
        MainCore.Log.Wrn($"[LangUpdate] {name} rejected: {why} — keeping the copy on disk");
        return false;
    }
    private static string HashFileSha256(string path) {
        try {
            if(!File.Exists(path)) return null;
            using SHA256 sha = SHA256.Create();
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ToHex(sha.ComputeHash(stream));
        } catch {
            return null;
        }
    }
    private static string HashBytesSha256(byte[] bytes) {
        using SHA256 sha = SHA256.Create();
        return ToHex(sha.ComputeHash(bytes));
    }
    private static string ToHex(byte[] hash) =>
        System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
}
