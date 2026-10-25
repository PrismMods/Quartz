using Quartz.Core;
using Quartz.Interop;
namespace Quartz.Features;
public static class FeatureImport {
    private static readonly Dictionary<string, string[]> ByKind = new(StringComparer.Ordinal) {
        [ImportSourceKind.KeyboardChatterBlocker] = ["keylimiter", "antichatter"],
        [ImportSourceKind.JipperKeyViewer] = ["keyviewer"],
        [ImportSourceKind.JipperResourcePack] = ["keyviewer", "combo", "judgement", "progressbar", "planetcolors", "ottoicon"],
        [ImportSourceKind.AdofaiTweaks] = ["keylimiter", "tweaks", "uihider", "restriction"],
        [ImportSourceKind.EnhancedEffectRemover] = ["effectremover", "visualtweaks"],
        [ImportSourceKind.KorenResourcePackV1] = [
            "keyviewer", "keylimiter", "antichatter", "combo", "judgement", "progressbar", "planetcolors", "ottoicon",
            "tweaks", "visualtweaks", "uihider", "restriction", "deathlimit", "effectremover",
        ],
    };
    // Handlers exist only while their feature is loaded: switch those on first; the caller re-runs the import after the load pump.
    public static bool EnsureForKind(string kind) =>
        kind != null && ByKind.TryGetValue(kind, out string[] ids) && Ensure(ids);
    public static bool Ensure(IReadOnlyList<string> ids) {
        if(ids == null) return false;
        List<string> off = [];
        foreach(string id in ids) {
            if(off.Contains(id)) continue;
            if(FeatureService.Find(id) is { Enabled: false }) off.Add(id);
        }
        if(off.Count == 0) return false;
        MainCore.Log.Msg($"[Import] the imported settings need {string.Join(", ", off)} — switching them on");
        foreach(string id in off) FeatureService.SetEnabled(id, true);
        return true;
    }
}
