#nullable enable
namespace Quartz.Modules;
public static class ModuleOrder {
    public sealed class Result {
        public List<ModuleManifest> Ordered = [];
        public Dictionary<string, string> Rejected = new(StringComparer.Ordinal);
    }
    public static Result Sort(IReadOnlyList<ModuleManifest> manifests) {
        Result result = new();
        Dictionary<string, ModuleManifest> byId = new(StringComparer.Ordinal);
        foreach(ModuleManifest manifest in manifests) byId[manifest.Id] = manifest;
        HashSet<string> pending = new(byId.Keys, StringComparer.Ordinal);
        foreach(ModuleManifest manifest in manifests) {
            foreach(string dep in manifest.Deps) {
                if(byId.ContainsKey(dep)) continue;
                Reject(result, byId, pending, manifest.Id, $"requires '{dep}', which is not installed");
                break;
            }
        }
        List<ModuleManifest> ready = [];
        while(true) {
            ready.Clear();
            foreach(string id in pending) {
                ModuleManifest manifest = byId[id];
                bool blocked = false;
                foreach(string dep in manifest.Deps)
                    if(pending.Contains(dep)) { blocked = true; break; }
                if(!blocked) ready.Add(manifest);
            }
            if(ready.Count == 0) break;
            ready.Sort(Compare);
            foreach(ModuleManifest manifest in ready) {
                result.Ordered.Add(manifest);
                pending.Remove(manifest.Id);
            }
        }
        foreach(string id in pending)
            result.Rejected[id] = "part of a dependency cycle";
        return result;
    }
    private static void Reject(
        Result result, Dictionary<string, ModuleManifest> byId, HashSet<string> pending, string id, string reason
    ) {
        if(!pending.Remove(id)) return;
        result.Rejected[id] = reason;
        foreach(ModuleManifest other in byId.Values) {
            if(!pending.Contains(other.Id)) continue;
            if(Array.IndexOf(other.Deps, id) >= 0)
                Reject(result, byId, pending, other.Id, $"requires '{id}', which could not be loaded");
        }
    }
    private static int Compare(ModuleManifest a, ModuleManifest b) {
        int byOrder = a.Order.CompareTo(b.Order);
        return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
    }
}
