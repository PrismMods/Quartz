using HarmonyLib;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
    [HarmonyPatch(typeof(scnSplash), "Start")]
    private static class HideAlphaWarningPatch {
        private static bool Prefix(scnSplash __instance) {
            if(ShouldDisableAlphaWarning) {
                Traverse.Create(__instance).Method("GoToMenu").GetValue();
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(NewsSign), "Awake")]
    private static class HideAnnounceSignPatch {
        private static void Postfix() {
            if(ShouldDisableAnnounceSign) ToggleSign(false);
        }
    }
    [HarmonyPatch(typeof(scnLevelSelect), "Start")]
    private static class LevelSelectStartPatch {
        private static void Postfix() {
            if(!Enabled) return;
            SetBackground();
            if(ShouldDisableAnnounceSign) ToggleSign(false);
            ApplyDeathSound();
            try { RDC.useOldAuto = ShouldWeakAuto; } catch { }
        }
    }
}
