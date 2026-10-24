using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features;
/// <summary>Which built-in features are switched on, plus the sidebar tab toggles. Features.json.</summary>
public sealed class FeatureState {
    public readonly Dictionary<string, bool> Features = new(StringComparer.Ordinal);
    public readonly Dictionary<string, bool> Categories = new(StringComparer.Ordinal);
    public static string Path => System.IO.Path.Combine(MainCore.Paths.RootPath, "Features.json");
    private static string LegacyFolder => System.IO.Path.Combine(MainCore.Paths.RootPath, "Module");
    // A feature added after this file was written gets its fresh-install default.
    public bool IsEnabled(string id) =>
        Features.TryGetValue(id, out bool enabled) ? enabled : FeatureDefaults.FreshDefault(id);
    public static FeatureState Parse(string json) {
        FeatureState state = new();
        JObject root = JObject.Parse(json);
        Read(root["features"] as JObject, state.Features);
        Read(root["categories"] as JObject, state.Categories);
        return state;
    }
    private static void Read(JObject source, Dictionary<string, bool> into) {
        if(source == null) return;
        foreach(JProperty prop in source.Properties())
            if(prop.Value is { Type: JTokenType.Boolean } flag) into[prop.Name] = (bool)flag;
    }
    public string ToJson() => new JObject {
        ["features"] = Write(Features),
        ["categories"] = Write(Categories),
    }.ToString(Formatting.Indented);
    private static JObject Write(Dictionary<string, bool> source) {
        JObject into = new();
        foreach(var kvp in source) into[kvp.Key] = kvp.Value;
        return into;
    }
    public static FeatureState Load() {
        try {
            if(File.Exists(Path)) return Parse(File.ReadAllText(Path));
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Features] couldn't read Features.json, using defaults: {e.Message}");
            return new FeatureState();
        }
        FeatureState state = FromLegacy() ?? FromSettings() ?? new FeatureState();
        state.Save();
        // Only once the new file is safely down: the old binaries are what the migration reads.
        if(File.Exists(Path)) RemoveLegacyBinaries();
        return state;
    }
    public void Save() {
        try {
            AtomicFile.WriteAllText(Path, ToJson());
        } catch(Exception e) {
            MainCore.Log.Err($"[Features] couldn't write Features.json: {e.Message}");
        }
    }
    // Module-era install: a feature stays on only if its module was installed and enabled.
    private static FeatureState FromLegacy() {
        string installed = System.IO.Path.Combine(LegacyFolder, "installed.json");
        if(!File.Exists(installed)) return null;
        FeatureState state = new();
        try {
            JObject root = JObject.Parse(File.ReadAllText(installed));
            Read(root["categories"] as JObject, state.Categories);
            HashSet<string> on = new(StringComparer.Ordinal);
            if(root["modules"] is JObject modules) {
                foreach(JProperty prop in modules.Properties()) {
                    bool enabled = prop.Value["enabled"] is not { Type: JTokenType.Boolean } flag || (bool)flag;
                    bool present = File.Exists(System.IO.Path.Combine(LegacyFolder, prop.Name + ".qmod"));
                    if(enabled && present) on.Add(prop.Name);
                }
            }
            foreach(FeatureService.Handle handle in FeatureService.Features) state.Features[handle.Id] = on.Contains(handle.Id);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Features] couldn't read the old module list, using defaults: {e.Message}");
            return null;
        }
        MainCore.Log.Msg("[Features] carried the installed modules over as switched-on features");
        return state;
    }
    // Pre-module (1.x) install: settings files say what was in use.
    private static FeatureState FromSettings() {
        FeatureDefaults.Plan plan = FeatureDefaults.Decide(ReadSettings);
        if(!plan.IsUpgrade) return null;
        FeatureState state = new();
        foreach(FeatureService.Handle handle in FeatureService.Features)
            state.Features[handle.Id] = plan.Enable.Contains(handle.Id);
        return state;
    }
    private static JObject ReadSettings(string fileName) {
        string path = System.IO.Path.Combine(MainCore.Paths.RootPath, fileName.Replace('/', System.IO.Path.DirectorySeparatorChar));
        try {
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    // The .qmod binaries are dead weight now; installed.json stays as the record of the old choice.
    private static void RemoveLegacyBinaries() {
        try {
            if(Directory.Exists(LegacyFolder)) {
                foreach(string file in Directory.GetFiles(LegacyFolder, "*.qmod")) File.Delete(file);
                foreach(string file in Directory.GetFiles(LegacyFolder, "*.qmod.json")) File.Delete(file);
            }
            string bundled = System.IO.Path.Combine(MainCore.Paths.RootPath, "Module.bundled");
            if(Directory.Exists(bundled)) Directory.Delete(bundled, true);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Features] couldn't remove old module files: {e.Message}");
        }
    }
}
