using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.DeathLimit;
internal static class LegacyRestriction {
    internal static bool Adopt(DeathLimitSettings target) {
        if(target == null) return false;
        string path = System.IO.Path.Combine(MainCore.Paths.RootPath, "Restriction.json");
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
        void Number(string name, Action<int> set) {
            if(legacy[name] is not { Type: JTokenType.Integer } value) return;
            set((int)value);
            took = true;
        }
        Flag(nameof(DeathLimitSettings.DeathLimitEnabled), v => target.DeathLimitEnabled = v);
        Flag(nameof(DeathLimitSettings.MaxDeathsOn), v => target.MaxDeathsOn = v);
        Number(nameof(DeathLimitSettings.MaxDeaths), v => target.MaxDeaths = v);
        Flag(nameof(DeathLimitSettings.MaxMissesOn), v => target.MaxMissesOn = v);
        Number(nameof(DeathLimitSettings.MaxMisses), v => target.MaxMisses = v);
        Flag(nameof(DeathLimitSettings.MaxOverloadsOn), v => target.MaxOverloadsOn = v);
        Number(nameof(DeathLimitSettings.MaxOverloads), v => target.MaxOverloads = v);
        if(legacy[nameof(DeathLimitSettings.DeathLimitMessage)] is { Type: JTokenType.String } message) {
            target.DeathLimitMessage = (string)message;
            took = true;
        }
        if(took) MainCore.Log.Msg("[DeathLimit] carried settings over from Restriction.json");
        return took;
    }
}
