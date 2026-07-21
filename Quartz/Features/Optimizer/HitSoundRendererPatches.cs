using HarmonyLib;
namespace Quartz.Features.Optimizer;
public static class HitSoundRendererPatches {
    [HarmonyPatch(typeof(scrConductor), "PlayHitTimes")]
    private static class PlayHitTimesPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrConductor), "PlayHitTimes") != null;
        private static void Postfix(scrConductor __instance) {
            if(HitSoundRenderer.Active) HitSoundRenderer.Capture(__instance);
        }
    }
    [HarmonyPatch(typeof(AudioManager), "StopAllSounds")]
    private static class StopAllSoundsPatch {
        private static bool Prepare() => AccessTools.Method(typeof(AudioManager), "StopAllSounds") != null;
        private static void Postfix() => HitSoundRenderer.StopAll("sounds stopped");
    }
    [HarmonyPatch(typeof(scrConductor), "KillAllSounds")]
    private static class KillAllSoundsPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrConductor), "KillAllSounds") != null;
        private static void Prefix() => HitSoundRenderer.StopAll("sounds killed");
    }
    [HarmonyPatch(typeof(scrController), "Restart")]
    private static class RestartPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrController), "Restart") != null;
        private static void Prefix() => HitSoundRenderer.StopAll("restart");
    }
    [HarmonyPatch(typeof(scrController), "FailAction")]
    private static class FailActionPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrController), "FailAction") != null;
        private static void Postfix() => HitSoundRenderer.StopAll("game over");
    }
}
