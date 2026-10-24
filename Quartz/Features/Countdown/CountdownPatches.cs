using System.Collections.Generic;
using ADOFAI;
using HarmonyLib;
namespace Quartz.Features.Countdown;
internal static class CountdownPatches {
    [HarmonyPatch(typeof(scrController), nameof(scrController.Start_Rewind))]
    internal static class StartRewindPatch {
        private static void Prefix() => CountdownScreenFx.ResetVolatileState();
        private static void Postfix() {
            CountdownHaywire.PendingCheckpoint = -1;
            if(GCS.checkpointNum == 0) return;
            CountdownHaywire.AttemptPending = CountdownFeature.Active;
            CountdownHaywire.ApplyStretch(GCS.checkpointNum);
        }
    }
    [HarmonyPatch(typeof(scrController), nameof(scrController.Scrub))]
    internal static class InitialScrubPatch {
        private static void Prefix() => CountdownScreenFx.ResetVolatileState();
        private static void Postfix() => CountdownHaywire.OnScrubCompleted();
    }
    [HarmonyPatch(typeof(scrConductor), nameof(scrConductor.ScrubMusicToTime))]
    internal static class HaywireScrubMusicPatch {
        private static void Prefix(scrConductor __instance, ref double newTime, out double __state) {
            __state = CountdownHaywire.ClaimAudioShift(__instance, newTime);
            newTime -= __state;
        }
        private static void Postfix(scrConductor __instance, double __state) {
            if(__state != 0.0) CountdownHaywire.RestoreLogicalClock(__instance, __state);
        }
    }
    [HarmonyPatch(typeof(scrController), "WaitForStartCo")]
    internal static class HaywireWaitForStartPatch {
        private static void Prefix() {
            if(GCS.checkpointNum == 0) return;
            CountdownHaywire.AttemptPending = CountdownFeature.Active;
            CountdownHaywire.ApplyStretch();
        }
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    internal static class HaywirePlayPatch {
        private static void Prefix(int seqID) {
            CountdownHaywire.PendingCheckpoint = seqID;
            CountdownHaywire.AttemptPending = CountdownFeature.Active && seqID != 0;
        }
    }
    [HarmonyPatch(typeof(scnGame), "ApplyEventsToFloors",
        new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) })]
    internal static class HaywireApplyEventsPatch {
        private static void Postfix() {
            CountdownHaywire.RestoreSpeeds();
            CountdownHaywire.ClearBookkeeping();
            if(CountdownHaywire.AttemptPending) CountdownHaywire.ApplyStretch();
        }
    }
    [HarmonyPatch(typeof(scrConductor), "SetupConductorWithLevelData")]
    internal static class HaywireSetupConductorPatch {
        private static void Postfix() {
            CountdownHaywire.RestoreSpeeds();
            CountdownHaywire.AttemptPending = false;
            CountdownHaywire.PendingCheckpoint = -1;
        }
    }
}
