using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.VisualTweaks;
internal static class LegacyTweaks {
    internal static bool Adopt(VisualTweaksSettings target) {
        if(target == null) return false;
        string path = System.IO.Path.Combine(MainCore.Paths.RootPath, "Tweaks.json");
        JObject legacy;
        try {
            if(!System.IO.File.Exists(path)) return false;
            legacy = JObject.Parse(System.IO.File.ReadAllText(path));
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
        bool took = false;
        void Flag(string name, Action<bool> set) {
            if(legacy[name] is not { Type: JTokenType.Boolean } value) return;
            set((bool)value);
            took = true;
        }
        Flag(nameof(VisualTweaksSettings.RemoveAllCheckpoints), v => target.RemoveAllCheckpoints = v);
        Flag(nameof(VisualTweaksSettings.RemoveBallCoreParticles), v => target.RemoveBallCoreParticles = v);
        Flag(nameof(VisualTweaksSettings.DisableTileHitGlow), v => target.DisableTileHitGlow = v);
        Flag(nameof(VisualTweaksSettings.RemovePlanetGlow), v => target.RemovePlanetGlow = v);
        if(took) MainCore.Log.Msg("[VisualTweaks] carried settings over from Tweaks.json");
        return took;
    }
}
