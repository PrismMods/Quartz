using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Status;
internal static class ProgressTracker {
    internal static float RunStartProgress;
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
        } catch {
            RunStartedFromFirstTile = true;
            RunStartProgress = 0f;
        }
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
