using HarmonyLib;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
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
