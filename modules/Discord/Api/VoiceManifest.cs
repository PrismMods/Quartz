using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using static Quartz.Features.Discord.Json;
namespace Quartz.Features.Discord;
public sealed class VoiceNativePackage {
    public string Name = "";
    public string Url = "";
    public string Sha256 = "";
    public string Entry = "";
    public string File = "";
}
public sealed class VoiceNativeEntry {
    public string Rid = "";
    public string Version = "";
    public readonly List<VoiceNativePackage> Packages = [];
    public bool Has(string name) {
        foreach(VoiceNativePackage package in Packages)
            if(package.Name == name) return true;
        return false;
    }
}
public static class VoiceManifest {
    public static VoiceNativeEntry Parse(string json, string rid) {
        if(string.IsNullOrEmpty(json) || string.IsNullOrEmpty(rid)) return null;
        JToken root = Json.Parse(json);
        JObject platforms = Obj(root, "platforms");
        JToken entry = platforms?[rid];
        JArray packages = Arr(entry, "packages");
        if(packages == null) return null;
        VoiceNativeEntry result = new() {
            Rid = rid,
            Version = Str(entry, "version") ?? Str(root, "version") ?? "",
        };
        foreach(JToken package in packages) {
            VoiceNativePackage parsed = new() {
                Name = Str(package, "name") ?? "",
                Url = Str(package, "url") ?? "",
                Sha256 = (Str(package, "sha256") ?? "").Trim().ToLowerInvariant(),
                Entry = Str(package, "entry") ?? "",
                File = Str(package, "file") ?? "",
            };
            if(parsed.Name.Length == 0 || parsed.Url.Length == 0
                || parsed.Entry.Length == 0 || parsed.File.Length == 0) continue;
            if(parsed.Sha256.Length != 64) continue;
            result.Packages.Add(parsed);
        }
        return result.Packages.Count == 0 ? null : result;
    }
    public static string ToHex(byte[] value) {
        if(value == null) return "";
        char[] hex = new char[value.Length * 2];
        const string digits = "0123456789abcdef";
        for(int i = 0; i < value.Length; i++) {
            hex[i * 2] = digits[value[i] >> 4];
            hex[(i * 2) + 1] = digits[value[i] & 0xF];
        }
        return new string(hex);
    }
    public static string HashOf(Stream stream) {
        using SHA256 sha = SHA256.Create();
        return ToHex(sha.ComputeHash(stream));
    }
}
