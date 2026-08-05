using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using Quartz.Compat.Game;
namespace Quartz.Features.Countdown;
internal static class CountdownPatches {
    [HarmonyPatch]
    internal static class FrozenPlayerUpdatePatch {
        private static MethodBase TargetMethod() => GameApi.PlayerControlUpdateTarget;
        private static bool Prefix(scrPlayer __instance, ref ulong? targetTick) =>
            CountdownFeature.Coordinator?.PreparePlayerUpdate(__instance, ref targetTick) ?? true;
        private static void Postfix(scrPlayer __instance) =>
            CountdownFeature.Coordinator?.CompletePlayerUpdate(__instance);
    }
    [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Hit))]
    internal static class FirstManualHitPatch {
        private static void Prefix(scrPlayer __instance, bool isAuto) =>
            CountdownFeature.Coordinator?.OnManualHitStarting(__instance, isAuto);
        private static void Postfix(scrPlayer __instance, bool isAuto, bool __result) =>
            CountdownFeature.Coordinator?.OnManualHitCompleted(__instance, isAuto, __result);
    }
    [HarmonyPatch(typeof(scrController), nameof(scrController.Start_Rewind))]
    internal static class StartRewindPatch {
        private static void Prefix(scrController __instance, int _currentSeqID) {
            CountdownScreenFx.ResetVolatileState();
            CountdownFeature.Coordinator?.OnStartRewind(__instance, _currentSeqID);
        }
        private static void Postfix() {
            CountdownHaywire.PendingCheckpoint = -1;
            if(GCS.checkpointNum == 0) return;
            CountdownHaywire.AttemptPending = CountdownFeature.HaywireActive;
            CountdownHaywire.ApplyStretch(GCS.checkpointNum);
        }
    }
    [HarmonyPatch(typeof(scrController), nameof(scrController.Scrub))]
    internal static class InitialScrubPatch {
        private static void Prefix(int floorNum, ref bool forceDontStartMusicFourTilesBefore) {
            CountdownScreenFx.ResetVolatileState();
            if(CountdownFeature.Coordinator?.PrepareInitialScrub(floorNum) != true) return;
            forceDontStartMusicFourTilesBefore = true;
            CountdownScrubWindow.Arm(floorNum);
        }
        private static void Postfix() {
            CountdownScrubWindow.Disarm();
            CountdownHaywire.OnScrubCompleted();
        }
    }
    [HarmonyPatch(typeof(scrVfxPlus), nameof(scrVfxPlus.ScrubToTime))]
    internal static class VfxScrubWindowPatch {
        private static void Prefix(ref float t) => t = CountdownScrubWindow.Restore(t);
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
    [HarmonyPatch(typeof(scrController), "OnMusicScheduled")]
    internal static class MusicScheduledPatch {
        private static void Postfix(scrController __instance) =>
            CountdownFeature.Coordinator?.OnMusicScheduled(__instance);
    }
    [HarmonyPatch(typeof(scrController), nameof(scrController.TogglePauseGame))]
    internal static class PauseFrozenStartPatch {
        private static void Prefix(scrController __instance) =>
            CountdownFeature.Coordinator?.OnPauseRequested(__instance);
    }
    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.SwitchToEditMode))]
    internal static class EditorPlayModeExitPatch {
        private static void Prefix() => CountdownFeature.Coordinator?.OnEditorPlayModeExited();
    }
    [HarmonyPatch(typeof(scrController), "WaitForStartCo")]
    internal static class HaywireWaitForStartPatch {
        private static void Prefix() {
            if(GCS.checkpointNum == 0) return;
            CountdownHaywire.AttemptPending = CountdownFeature.HaywireActive;
            CountdownHaywire.ApplyStretch();
        }
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    internal static class HaywirePlayPatch {
        private static void Prefix(int seqID) {
            CountdownHaywire.PendingCheckpoint = seqID;
            CountdownHaywire.AttemptPending = CountdownFeature.HaywireActive && seqID != 0;
        }
    }
    [HarmonyPatch(typeof(scnGame), "ApplyEventsToFloors",
        new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) })]
    internal static class HaywireApplyEventsPatch {
        private static void Postfix() {
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
