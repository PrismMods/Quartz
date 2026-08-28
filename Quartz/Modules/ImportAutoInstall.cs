using Quartz.Core;
using Quartz.Interop;
namespace Quartz.Modules;
public static class ImportAutoInstall {
    private static readonly Dictionary<string, string[]> ByKind = new(StringComparer.Ordinal) {
        [ImportSourceKind.KeyboardChatterBlocker] = ["keylimiter"],
        [ImportSourceKind.JipperKeyViewer] = ["keyviewer"],
        [ImportSourceKind.JipperResourcePack] = ["keyviewer", "combo", "judgement", "progressbar", "planetcolors", "ottoicon"],
        [ImportSourceKind.AdofaiTweaks] = ["keylimiter", "tweaks", "uihider", "restriction"],
        [ImportSourceKind.EnhancedEffectRemover] = ["effectremover", "visualtweaks"],
        [ImportSourceKind.KorenResourcePackV1] = [
            "keyviewer", "keylimiter", "combo", "judgement", "progressbar", "planetcolors", "ottoicon",
            "tweaks", "visualtweaks", "uihider", "restriction", "deathlimit", "effectremover",
        ],
    };
    // An IImportHandler only exists once its module is installed, so a settings import
    // into a fresh install silently drops everything the missing modules would have read.
    // Put the modules that handle this source in first; the caller re-runs the import
    // after the load pump so the handlers are registered by the time it delivers.
    public static bool EnsureForKind(string kind) =>
        kind != null && ByKind.TryGetValue(kind, out string[] ids) && Ensure(ids);
    public static bool Ensure(IReadOnlyList<string> ids) {
        if(ids == null) return false;
        List<string> missing = [];
        foreach(string id in ids) {
            if(string.IsNullOrEmpty(id) || missing.Contains(id)) continue;
            if(ModuleService.Find(id) is { Loaded: true }) continue;
            if(!ModuleBundle.Has(id)) continue;
            missing.Add(id);
        }
        if(missing.Count == 0) return false;
        MainCore.Log.Msg($"[Import] the imported settings need {string.Join(", ", missing)} — installing");
        ModuleBundle.Install(missing);
        return true;
    }
}
