using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Modules;
public sealed partial class ModuleState {
    public const int Schema = 1;
    public sealed class Entry {
        public bool Enabled = true;
        public string Version = "";
        public string Source = "manual";
    }
    public readonly Dictionary<string, Entry> Modules = new(StringComparer.Ordinal);
    public readonly List<string> PendingInstall = [];
    public bool Migrated;
    public readonly Dictionary<string, bool> Categories = new(StringComparer.Ordinal);
    public static ModuleState Parse(string json) {
        ModuleState state = new();
        JObject root;
        try {
            root = JObject.Parse(json);
        } catch {
            return state;
        }
        if(root["modules"] is JObject modules) {
            foreach(JProperty prop in modules.Properties()) {
                if(prop.Value is not JObject entry) continue;
                if(!ModuleManifest.IsValidId(prop.Name)) continue;
                state.Modules[prop.Name] = new Entry {
                    Enabled = entry["enabled"] is not { Type: JTokenType.Boolean } flag || (bool)flag,
                    Version = entry["version"]?.ToString() ?? "",
                    Source = entry["source"]?.ToString() ?? "manual",
                };
            }
        }
        state.Migrated = root["migrated"] is { Type: JTokenType.Boolean } migrated && (bool)migrated;
        if(root["categories"] is JObject categories) {
            foreach(JProperty prop in categories.Properties()) {
                if(prop.Value is not { Type: JTokenType.Boolean } flag) continue;
                state.Categories[prop.Name] = (bool)flag;
            }
        }
        if(root["pendingInstall"] is JArray pending) {
            foreach(JToken item in pending) {
                string id = item?.ToString();
                if(ModuleManifest.IsValidId(id) && !state.PendingInstall.Contains(id)) state.PendingInstall.Add(id);
            }
        }
        return state;
    }
    public string ToJson() {
        JObject modules = new();
        foreach(var kvp in Modules) {
            modules[kvp.Key] = new JObject {
                ["enabled"] = kvp.Value.Enabled,
                ["version"] = kvp.Value.Version ?? "",
                ["source"] = kvp.Value.Source ?? "manual",
            };
        }
        JArray pending = new();
        foreach(string id in PendingInstall) pending.Add(id);
        JObject categories = new();
        foreach(var kvp in Categories) categories[kvp.Key] = kvp.Value;
        return new JObject {
            ["schema"] = Schema,
            ["migrated"] = Migrated,
            ["modules"] = modules,
            ["categories"] = categories,
            ["pendingInstall"] = pending,
        }.ToString(Formatting.Indented);
    }
    public Entry For(string id) {
        if(!Modules.TryGetValue(id, out Entry entry)) Modules[id] = entry = new Entry();
        return entry;
    }
}
