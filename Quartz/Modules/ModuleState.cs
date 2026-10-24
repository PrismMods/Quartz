using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Modules;
public sealed partial class ModuleState {
    public const int Schema = 1;
    public sealed class Entry {
        public bool Enabled = true;
        public string Source = "manual";
    }
    public readonly Dictionary<string, Entry> Modules = new(StringComparer.Ordinal);
    public bool Migrated;
    public bool Corrupt;
    public readonly Dictionary<string, bool> Categories = new(StringComparer.Ordinal);
    public static ModuleState Parse(string json) {
        ModuleState state = new();
        JObject root;
        try {
            root = JObject.Parse(json);
        } catch(Exception e) {
            Diag.Ignore(e);
            state.Corrupt = true;
            return state;
        }
        if(root["modules"] is JObject modules) {
            foreach(JProperty prop in modules.Properties()) {
                if(prop.Value is not JObject entry) continue;
                if(!ModuleManifest.IsValidId(prop.Name)) continue;
                state.Modules[prop.Name] = new Entry {
                    Enabled = entry["enabled"] is not { Type: JTokenType.Boolean } flag || (bool)flag,
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
        return state;
    }
    public string ToJson() {
        JObject modules = new();
        foreach(var kvp in Modules) {
            modules[kvp.Key] = new JObject {
                ["enabled"] = kvp.Value.Enabled,
                ["source"] = kvp.Value.Source ?? "manual",
            };
        }
        JObject categories = new();
        foreach(var kvp in Categories) categories[kvp.Key] = kvp.Value;
        return new JObject {
            ["schema"] = Schema,
            ["migrated"] = Migrated,
            ["modules"] = modules,
            ["categories"] = categories,
        }.ToString(Formatting.Indented);
    }
    public Entry For(string id) {
        if(!Modules.TryGetValue(id, out Entry entry)) Modules[id] = entry = new Entry();
        return entry;
    }
}
