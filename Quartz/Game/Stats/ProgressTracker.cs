using HarmonyLib;
using MonsterLove.StateMachine;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Game.Stats;
public static class ProgressTracker {
    public static float RunStartProgress;
    public static float RunStartMapTimeRatio;
    public static bool RunStartedFromFirstTile = true;
    public static bool RunCleared;
    public static bool RunTimeFrozen;
    public static float FrozenMapTimeSeconds;
    public static float FrozenMusicTimeSeconds;
    public static void FreezeRunTime() {
        if(RunTimeFrozen) return;
        FrozenMapTimeSeconds = GameStats.RawMapTimeSeconds();
        FrozenMusicTimeSeconds = GameStats.RawMusicTimeSeconds();
        RunTimeFrozen = true;
    }
    public static void ThawRunTime() {
        RunTimeFrozen = false;
        FrozenMapTimeSeconds = 0f;
        FrozenMusicTimeSeconds = 0f;
    }
    public static int ResolveStartSeqID(int seqID) {
        if(seqID > 0) return seqID;
        try {
            return Mathf.Max(0, GCS.checkpointNum);
        } catch(Exception e) { Diag.Ignore(e); }
        return 0;
    }
    public static bool IsFirstTileRunStart(int seqID = 0) {
        if(ResolveStartSeqID(seqID) > 0) return false;
        try {
            if(scnGame.instance != null && scnGame.instance.checkpointsUsed > 0) return false;
        } catch(Exception e) { Diag.Ignore(e); }
        try {
            if(scrController.checkpointsUsed > 0) return false;
        } catch(Exception e) { Diag.Ignore(e); }
        return true;
    }
    public static void CaptureRunStart(int seqID = 0) {
        RunCleared = false;
        ThawRunTime();
        try {
            int startSeqID = ResolveStartSeqID(seqID);
            RunStartedFromFirstTile = IsFirstTileRunStart(seqID);
            RunStartProgress = RunStartedFromFirstTile ? 0f : StartProgress(startSeqID);
            RunStartMapTimeRatio = RunStartedFromFirstTile ? 0f : StartMapTimeRatio(startSeqID);
        } catch(Exception e) {
            Diag.Ignore(e);
            RunStartedFromFirstTile = true;
            RunStartProgress = 0f;
            RunStartMapTimeRatio = 0f;
        }
    }
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
        } catch(Exception e) { Diag.Ignore(e); }
        return 0f;
    }
    private static float StartProgress(int seqID) {
        try {
            scrLevelMaker lm = scrLevelMaker.instance;
            int count = lm != null && lm.listFloors != null ? lm.listFloors.Count : 0;
            if(seqID > 0 && count > 0) return Mathf.Clamp01((seqID + 1f) / count);
        } catch(Exception e) { Diag.Ignore(e); }
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
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class RunStatePatch {
        private static void Postfix(Enum newState) {
            if(!MainCore.IsModEnabled) return;
            if(newState is not States state) return;
            if(state == States.Won) {
                RunCleared = true;
                FreezeRunTime();
            } else if(state == States.Fail) {
                FreezeRunTime();
            } else if(state != States.Fail2) {
                ThawRunTime();
            }
        }
    }
}
