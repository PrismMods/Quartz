using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Modules;
public sealed class ModuleCatalogEntry {
    public string Id;
    public string Name;
    public string NameKey;
    public string Desc;
    public string DescKey;
    public string Group;
    public int Order;
    public string Version;
    public int CoreAbi;
    public string MinCoreVersion;
    public string[] Deps = [];
    public string Url;
    public string ManifestUrl;
    public long Size;
    public string Sha256;
    public string ManifestSha256;
}
public sealed class ModuleGroup {
    public string Key;
    public string Title;
    public string TitleKey;
    public int Order;
}
public sealed class ModuleCatalog {
    public const int Schema = 1;
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxBundleBytes = 64 * 1024 * 1024;
    public string CoreVersion = "";
    public string CoreTag = "";
    public int CoreAbi = -1;
    public string BundleUrl = "";
    public string BundleSha256 = "";
    public long BundleSize;
    public bool HasBundle => !string.IsNullOrWhiteSpace(BundleUrl) && IsSha256(BundleSha256);
    public readonly List<ModuleGroup> Groups = [];
    public readonly List<ModuleCatalogEntry> Modules = [];
    public ModuleCatalogEntry Find(string id) {
        if(id == null) return null;
        foreach(ModuleCatalogEntry entry in Modules)
            if(entry.Id == id) return entry;
        return null;
    }
    public static string TrimV(string version) =>
        version != null && version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version[1..] : version;
    public static bool IsNewer(string candidate, string current) {
        if(string.IsNullOrWhiteSpace(candidate)) return false;
        if(string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase)) return false;
        if(string.IsNullOrWhiteSpace(current)) return true;
        return SemVer.TryParse(candidate, out SemVer remote)
            && SemVer.TryParse(current, out SemVer local)
            && remote.CompareTo(local) > 0;
    }
    public static bool IsSha256(string value) {
        if(value == null || value.Length != 64) return false;
        foreach(char c in value)
            if(!char.IsDigit(c) && c is not (>= 'a' and <= 'f')) return false;
        return true;
    }
    public static ModuleCatalog Parse(string json, out string error) {
        error = null;
        JObject root;
        try {
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 32 };
            root = JObject.Load(reader);
        } catch(Exception e) {
            error = "not valid JSON: " + e.Message;
            return null;
        }
        if(root["schema"] is not { Type: JTokenType.Integer } schema || (int)schema != Schema) {
            error = "unsupported catalog schema";
            return null;
        }
        ModuleCatalog catalog = new();
        if(root["core"] is JObject core) {
            catalog.CoreVersion = core["version"]?.ToString() ?? "";
            catalog.CoreTag = core["tag"]?.ToString() ?? "";
            catalog.CoreAbi = core["abi"] is { Type: JTokenType.Integer } abi ? (int)abi : -1;
        }
        if(root["bundle"] is JObject bundle) {
            catalog.BundleUrl = bundle["url"]?.ToString() ?? "";
            catalog.BundleSha256 = bundle["sha256"]?.ToString() ?? "";
            catalog.BundleSize = bundle["size"] is { Type: JTokenType.Integer } bundleSize ? (long)bundleSize : 0;
        }
        if(root["groups"] is JArray groups) {
            foreach(JToken item in groups) {
                if(item is not JObject group) continue;
                string key = group["key"]?.ToString();
                if(string.IsNullOrWhiteSpace(key)) continue;
                catalog.Groups.Add(new ModuleGroup {
                    Key = key,
                    Title = group["title"]?.ToString() ?? key,
                    TitleKey = group["titleKey"]?.ToString(),
                    Order = group["order"] is { Type: JTokenType.Integer } o ? (int)o : 0,
                });
            }
        }
        if(root["modules"] is JArray modules) {
            foreach(JToken item in modules) {
                if(item is not JObject module) continue;
                ModuleCatalogEntry entry = ParseEntry(module);
                if(entry != null && catalog.Find(entry.Id) == null) catalog.Modules.Add(entry);
            }
        }
        catalog.Groups.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Key, b.Key));
        catalog.Modules.Sort(static (a, b) => {
            int byGroup = string.CompareOrdinal(a.Group ?? "", b.Group ?? "");
            if(byGroup != 0) return byGroup;
            return a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id);
        });
        return catalog;
    }
    private static ModuleCatalogEntry ParseEntry(JObject module) {
        string id = module["id"]?.ToString();
        if(!ModuleManifest.IsValidId(id)) return null;
        string sha = module["sha256"]?.ToString();
        string manifestSha = module["manifestSha256"]?.ToString();
        if(!IsSha256(sha) || !IsSha256(manifestSha)) return null;
        string url = module["url"]?.ToString() ?? "";
        string manifestUrl = module["manifestUrl"]?.ToString() ?? "";
        return new ModuleCatalogEntry {
            Id = id,
            Name = module["name"]?.ToString() ?? id,
            NameKey = module["nameKey"]?.ToString(),
            Desc = module["desc"]?.ToString() ?? "",
            DescKey = module["descKey"]?.ToString(),
            Group = module["group"]?.ToString() ?? "",
            Order = module["order"] is { Type: JTokenType.Integer } order ? (int)order : 0,
            Version = module["version"]?.ToString() ?? "",
            CoreAbi = module["coreAbi"] is { Type: JTokenType.Integer } abi ? (int)abi : -1,
            MinCoreVersion = module["minCoreVersion"]?.ToString() ?? "",
            Deps = ReadDeps(module["deps"] as JArray),
            Url = url,
            ManifestUrl = manifestUrl,
            Size = module["size"] is { Type: JTokenType.Integer } size ? (long)size : 0,
            Sha256 = sha,
            ManifestSha256 = manifestSha,
        };
    }
    private static string[] ReadDeps(JArray array) {
        if(array == null) return [];
        List<string> deps = [];
        foreach(JToken item in array) {
            string id = item?.ToString();
            if(ModuleManifest.IsValidId(id) && !deps.Contains(id)) deps.Add(id);
        }
        return [.. deps];
    }
    public List<string> ResolveWithDeps(string id) {
        List<string> resolved = [];
        Walk(id, resolved, new HashSet<string>(StringComparer.Ordinal));
        return resolved;
    }
    private void Walk(string id, List<string> resolved, HashSet<string> seen) {
        if(id == null || !seen.Add(id)) return;
        ModuleCatalogEntry entry = Find(id);
        if(entry == null) return;
        foreach(string dep in entry.Deps) Walk(dep, resolved, seen);
        if(!resolved.Contains(id)) resolved.Add(id);
    }
}
