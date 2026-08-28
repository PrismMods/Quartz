using HarmonyLib;
using System.Reflection;
using Quartz.Compat.Game;
using Quartz.Core;
namespace Quartz.Features.MainMenu;
public static partial class MenuTweaks {
    [HarmonyPatch(typeof(scrConductor), "Update")]
    private static class DisableMenuMusicPatch {
        private static void Postfix(scrConductor __instance) => ApplyMenuMusicMute(__instance);
    }
    [HarmonyPatch(typeof(ffxMenuPlanetSpeedChange), "Start")]
    private static class MenuBpmInitPatch {
        private static void Postfix() {
            try { ApplyInitialMenuBpm(); }
            catch(Exception e) { Diag.Ignore(e); }
        }
    }
    [HarmonyPatch]
    private static class MenuBpmTogglePatch {
        private static MethodBase TargetMethod() => GameApi.MenuSpeedStartEffectTarget;
        private static bool Prefix(ffxMenuPlanetSpeedChange __instance) {
            try {
                return !HandleMenuBpmToggle(__instance.floor);
            } catch(Exception e) {
                Diag.Ignore(e);
                return true;
            }
        }
    }
}
