using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Modules;
public sealed class ModuleManifest {
    public const int Schema = 1;
    public const int MaxBytes = 64 * 1024;
    public string Id;
    public string Entry;
    public string Name;
    public string NameKey;
    public string Group;
    public int Order;
    public string Version;
    public int CoreAbi;
    public string MinCoreVersion;
    public string[] Deps = [];
    public string[] SettingsFiles = [];
    public string[] LangPrefixes = [];
    public static bool IsValidId(string id) {
        if(string.IsNullOrEmpty(id) || id.Length > 64) return false;
        foreach(char c in id)
            if(!char.IsLower(c) && !char.IsDigit(c) && c != '-') return false;
        return char.IsLower(id[0]) || char.IsDigit(id[0]);
    }
    public static ModuleManifest Parse(string json, out string error) {
        error = null;
        JObject root;
        try {
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16 };
            root = JObject.Load(reader);
        } catch(Exception e) {
            error = "not valid JSON: " + e.Message;
            return null;
        }
        int schema = Read(root, "schema", 0);
        if(schema != Schema) {
            error = $"unsupported manifest schema {schema} (this build reads {Schema})";
            return null;
        }
        ModuleManifest manifest = new() {
            Id = Str(root, "id"),
            Entry = Str(root, "entry"),
            Name = Str(root, "name"),
            NameKey = Str(root, "nameKey"),
            Group = Str(root, "group"),
            Order = Read(root, "order", 0),
            Version = Str(root, "version"),
            CoreAbi = Read(root, "coreAbi", -1),
            MinCoreVersion = Str(root, "minCoreVersion"),
            Deps = StrArray(root, "deps"),
            SettingsFiles = StrArray(root, "settingsFiles"),
            LangPrefixes = StrArray(root, "langPrefixes"),
        };
        if(!IsValidId(manifest.Id)) {
            error = $"'{manifest.Id}' is not a valid module id (lowercase letters, digits and '-' only)";
            return null;
        }
        if(manifest.CoreAbi < 0) {
            error = "manifest is missing coreAbi";
            return null;
        }
        if(string.IsNullOrEmpty(manifest.Version)) {
            error = "manifest is missing version";
            return null;
        }
        foreach(string dep in manifest.Deps) {
            if(IsValidId(dep)) continue;
            error = $"'{dep}' is not a valid dependency id";
            return null;
        }
        if(Array.IndexOf(manifest.Deps, manifest.Id) >= 0) {
            error = "a module cannot depend on itself";
            return null;
        }
        if(string.IsNullOrEmpty(manifest.Name)) manifest.Name = manifest.Id;
        return manifest;
    }
    private static string Str(JObject root, string key) =>
        root[key] is { Type: JTokenType.String } token ? token.ToString().Trim() : null;
    private static int Read(JObject root, string key, int fallback) =>
        root[key] is { Type: JTokenType.Integer } token ? (int)token : fallback;
    private static string[] StrArray(JObject root, string key) {
        if(root[key] is not JArray array) return [];
        List<string> values = [];
        foreach(JToken item in array)
            if(item.Type == JTokenType.String && item.ToString().Trim().Length > 0)
                values.Add(item.ToString().Trim());
        return [.. values];
    }
}
