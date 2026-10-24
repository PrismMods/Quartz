using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.MainMenu;
internal static class LegacyTweaks {
    internal static bool Adopt(MenuTweaksSettings target) {
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
        if(legacy[nameof(MenuTweaksSettings.DisableMenuMusic)] is { Type: JTokenType.Boolean } music) {
            target.DisableMenuMusic = (bool)music;
            took = true;
        }
        if(legacy[nameof(MenuTweaksSettings.MenuBpmEnabled)] is { Type: JTokenType.Boolean } bpm) {
            target.MenuBpmEnabled = (bool)bpm;
            took = true;
        }
        if(Number(legacy[nameof(MenuTweaksSettings.MenuSlowBpm)], out float slow)) {
            target.MenuSlowBpm = slow;
            took = true;
        }
        if(Number(legacy[nameof(MenuTweaksSettings.MenuHighBpm)], out float high)) {
            target.MenuHighBpm = high;
            took = true;
        }
        if(took) MainCore.Log.Msg("[MainMenu] carried settings over from Tweaks.json");
        return took;
    }
    private static bool Number(JToken token, out float value) {
        value = 0f;
        if(token is not { Type: JTokenType.Float or JTokenType.Integer }) return false;
        value = (float)token;
        return true;
    }
}
