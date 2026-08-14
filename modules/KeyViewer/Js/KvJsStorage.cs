using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features.KeyViewer.Js;
internal static class KvJsStorage {
    private static Dictionary<string, string> data;
    private static bool dirty;
    private static float nextSave;
    private const float SaveDebounceSeconds = 2f;
    private static string FilePath => Path.Combine(MainCore.Paths.RootPath, "KeyViewerJs.json");
    private static void EnsureLoaded() {
        if(data != null) return;
        data = new Dictionary<string, string>(StringComparer.Ordinal);
        try {
            if(!File.Exists(FilePath)) return;
            JObject obj = JObject.Parse(File.ReadAllText(FilePath));
            foreach(var prop in obj.Properties()) data[prop.Name] = prop.Value.Type == JTokenType.String ? prop.Value.ToString() : prop.Value.ToString(Newtonsoft.Json.Formatting.None);
        } catch(Exception e) { Diag.Warn(e, "KeyViewerJs/StorageLoad"); }
    }
    public static string Get(string key) {
        EnsureLoaded();
        return data.TryGetValue(key, out string value) ? value : null;
    }
    public static void Set(string key, string json, float now) {
        EnsureLoaded();
        data[key] = json ?? "null";
        MarkDirty(now);
    }
    public static void Remove(string key, float now) {
        EnsureLoaded();
        if(data.Remove(key)) MarkDirty(now);
    }
    public static int ClearByPrefix(string prefix, float now) {
        EnsureLoaded();
        List<string> victims = [];
        foreach(string key in data.Keys)
            if(key.StartsWith(prefix, StringComparison.Ordinal)) victims.Add(key);
        foreach(string key in victims) data.Remove(key);
        if(victims.Count > 0) MarkDirty(now);
        return victims.Count;
    }
    public static List<string> Keys(string prefix) {
        EnsureLoaded();
        List<string> result = [];
        foreach(string key in data.Keys)
            if(key.StartsWith(prefix, StringComparison.Ordinal)) result.Add(key.Substring(prefix.Length));
        return result;
    }
    public static bool HasPrefix(string prefix) {
        EnsureLoaded();
        foreach(string key in data.Keys)
            if(key.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }
    private static void MarkDirty(float now) {
        dirty = true;
        nextSave = now + SaveDebounceSeconds;
    }
    public static void Tick(float now) {
        if(dirty && now >= nextSave) Flush();
    }
    public static void Flush() {
        if(!dirty || data == null) return;
        try {
            JObject obj = [];
            foreach((string key, string value) in data) obj[key] = value;
            AtomicFile.WriteAllText(FilePath, obj.ToString(Newtonsoft.Json.Formatting.None));
            dirty = false;
        } catch(Exception e) {
            nextSave = KvClock.Now + SaveDebounceSeconds;
            Diag.Warn(e, "KeyViewerJs/StorageSave");
        }
    }
}
