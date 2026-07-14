using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Quartz.Core.Service;
// Pulls community translations from the Quartz-i18n repo into the local Lang folder,
// so a translator's work reaches users without cutting a release.
//
// Cost is kept near zero by a manifest: one small file at the repo root lists the
// sha256 of every language file. We hash what is on disk and only download the ones
// that differ, so the usual "nothing changed" launch costs a single ~200 byte
// request. Everything goes through raw.githubusercontent.com rather than the GitHub
// API, which also sidesteps the API's 60-requests/hour unauthenticated limit.
//
// The language files shipped in the zip stay the offline baseline: if anything here
// fails — no network, GitHub blocked or throttled (common in mainland China, i.e.
// exactly where zh-CN users are), a malformed upstream file, a hash mismatch —
// nothing is written and the bundled translations are used unchanged. Failure is
// always silent and lossless; this never throws.
//
// Touches no Unity API (HTTP + file IO + JSON only), so it is safe to run off the
// main thread. The reload it feeds (Translator.Load) marshals OnLoadEnd through the
// dispatcher itself, which is what keeps the UI rebuild on the main thread.
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
    // Returns how many language files were written. 0 means "already current" or
    // "couldn't reach the repo" — both leave what's on disk alone, so callers can
    // skip the reload when this returns 0.
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
                // en-US is owned by the mod repo and only mirrored into Quartz-i18n; the
                // bundled copy always matches this build, so pulling it back could only
                // ever replace it with a staler one.
                if(name.Equals(EnglishFile, System.StringComparison.OrdinalIgnoreCase)) continue;
                string dest = Path.Combine(langPath, name);
                if(HashFileSha256(dest) == want) continue;   // already current — no download at all
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
    // The manifest is remote content, so its keys are untrusted: anything that isn't a
    // plain file name could escape the Lang folder once it reaches Path.Combine.
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
            // Write the exact bytes the manifest hashed — re-encoding the text could
            // shift a BOM or line endings and leave the on-disk hash permanently
            // mismatched, re-downloading this file on every single launch.
            string tmp = dest + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if(File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
            return true;
        } catch(System.Exception e) {
            return Reject(name, e.Message);
        }
    }
    // Never let a broken download replace a good bundled file. Translator silently
    // drops any block whose 0KTL sentinel is missing, so an upstream mistake would
    // otherwise wipe a whole language with no error anywhere.
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
    // null when the file is missing or unreadable, which simply reads as "differs from
    // the manifest" and re-downloads.
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
