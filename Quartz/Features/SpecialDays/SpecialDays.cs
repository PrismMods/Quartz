using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features.SpecialDays;
// Overrides the game's own date checks: Halloween week (Oct 24 - Nov 2) and April Fools (Apr 1 - 8).
// Forcing on skips the game's "world 1 cleared" requirement too.
public static class SpecialDays {
    public static SettingsFile<SpecialDaysSettings> ConfMgr { get; private set; }
    public static SpecialDaysSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<SpecialDaysSettings>.Loaded("SpecialDays.json");
    public static void Save() => ConfMgr?.RequestSave();
    // false skips the original and returns the forced answer.
    private static bool Run(SpecialDayMode mode, ref bool result) {
        if(!MainCore.IsModEnabled || mode == SpecialDayMode.Auto) return true;
        result = mode == SpecialDayMode.On;
        return false;
    }
    [HarmonyPatch(typeof(ADOBase), nameof(ADOBase.IsHalloweenWeek))]
    private static class HalloweenPatch {
        private static bool Prefix(ref bool __result) {
            EnsureConf();
            return Run(Conf.Halloween, ref __result);
        }
    }
    [HarmonyPatch(typeof(ADOBase), nameof(ADOBase.IsAprilFools))]
    private static class AprilFoolsPatch {
        private static bool Prefix(ref bool __result) {
            EnsureConf();
            return Run(Conf.AprilFools, ref __result);
        }
    }
}
