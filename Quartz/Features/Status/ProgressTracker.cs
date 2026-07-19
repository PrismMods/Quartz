using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Status;
internal static class ProgressTracker {
    internal static float RunStartProgress;
    internal static float RunStartMapTimeRatio;
    internal static bool RunStartedFromFirstTile = true;
    internal static bool IsFirstTileRunStart(int seqID = 0) {
        if(seqID > 0) return false;
        try {
            if(scnGame.instance != null && scnGame.instance.checkpointsUsed > 0) return false;
        } catch { }
        try {
            if(scrController.checkpointsUsed > 0) return false;
        } catch { }
        try {
            scrController c = scrController.instance;
            if(c != null && c.currentSeqID > 0) return false;
        } catch { }
        return true;
    }
    internal static void CaptureRunStart(int seqID = 0) {
        try {
            scrController c = scrController.instance;
            RunStartedFromFirstTile = IsFirstTileRunStart(seqID);
            RunStartProgress = RunStartedFromFirstTile ? 0f : StartProgress(c, seqID);
            RunStartMapTimeRatio = RunStartedFromFirstTile ? 0f : StartMapTimeRatio(seqID);
        } catch {
            RunStartedFromFirstTile = true;
            RunStartProgress = 0f;
            RunStartMapTimeRatio = 0f;
        }
    }
    // Map-time-space twin of StartProgress: where the run began as a fraction of
    // the chart's total duration, so the smooth (map-time) bar's start offset
    // lines up with its fill instead of borrowing the tile-fraction value.
    private static float StartMapTimeRatio(int seqID) {
        try {
            scrLevelMaker lm = scrLevelMaker.instance;
            if(seqID > 0 && lm != null && lm.listFloors != null && lm.listFloors.Count > 0) {
                int count = lm.listFloors.Count;
                scrFloor last = lm.listFloors[count - 1];
                float total = last != null ? (float)last.entryTime : 0f;
                if(total <= 0f) return 0f;
                scrFloor start = lm.listFloors[Mathf.Clamp(seqID, 0, count - 1)];
                float t = start != null ? (float)start.entryTime : 0f;
                return Mathf.Clamp01(t / total);
            }
        } catch { }
        // Checkpoint reverts don't carry a seqID; fall back to the live map-time
        // ratio, mirroring StartProgress's percentComplete fallback.
        return GameStats.MapTimeRatio;
    }
    private static float StartProgress(scrController c, int seqID) {
        try {
            scrLevelMaker lm = scrLevelMaker.instance;
            int count = lm != null && lm.listFloors != null ? lm.listFloors.Count : 0;
            if(seqID > 0 && count > 0) return Mathf.Clamp01((seqID + 1f) / count);
        } catch { }
        float progress = c != null ? c.percentComplete : 0f;
        if(progress > 0f) return Mathf.Clamp01(progress);
        return 0f;
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class PlayPatch {
        private static void Postfix(int seqID) {
            if(!MainCore.IsModEnabled) return;
            CaptureRunStart(seqID);
        }
    }
    [HarmonyPatch(typeof(scrController), "RestartProgress")]
    private static class RestartProgressPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            CaptureRunStart();
        }
    }
    [HarmonyPatch(typeof(scrController), "Restart", typeof(bool))]
    private static class RestartPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            CaptureRunStart();
        }
    }
    [HarmonyPatch(typeof(scrMistakesManager), "RevertToLastCheckpoint")]
    private static class RevertCheckpointPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            CaptureRunStart();
        }
    }
}
