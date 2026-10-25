// Ported from AntiChatter by copecone (https://git.yunizun.org/copecone/AntiChatter).
// MIT License, Copyright (c) 2026 copecone. The full notice is in THIRD-PARTY-NOTICES.md.
using System.Diagnostics;
using Quartz.Core;
using Quartz.IO;
using SkyHook;
using UnityEngine;
namespace Quartz.Features.AntiChatter;
// Drops a key's second press inside the threshold (switch bounce), on both the async hook and Unity input,
// and bridges a hold that bounces open for a moment instead of ending it.
public static partial class AntiChatter {
    public const long TicksPerMillisecond = 10_000L;
    public static SettingsFile<AntiChatterSettings> ConfMgr { get; private set; }
    public static AntiChatterSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = SettingsFile<AntiChatterSettings>.Loaded("AntiChatter.json");
        if(!System.IO.File.Exists(ConfMgr.Path)) ImportLegacy();
    }
    // Keyboard Chatter Blocker saved the same Enabled/ThresholdMs fields; carry them over once.
    private static void ImportLegacy() {
        string legacy = System.IO.Path.Combine(MainCore.Paths.RootPath, "ChatterBlocker.json");
        if(!System.IO.File.Exists(legacy)) return;
        try {
            ConfMgr.Data.Deserialize(Newtonsoft.Json.Linq.JToken.Parse(System.IO.File.ReadAllText(legacy)));
            ConfMgr.RequestSave();
        } catch(Exception e) {
            MainCore.Log.Wrn($"[AntiChatter] couldn't read ChatterBlocker.json: {e.Message}");
        }
    }
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Enabled => MainCore.IsModEnabled && Conf is { Enabled: true };
    private static bool HoldBounceProtection => Enabled && Conf.HoldBounceProtection;
    private readonly struct EarlyReleaseInfo(int floorSeqId, float holdCompletionAtRelease, long releaseTick) {
        public readonly int FloorSeqId = floorSeqId;
        public readonly float HoldCompletionAtRelease = holdCompletionAtRelease;
        public readonly long ReleaseTick = releaseTick;
    }
    private static readonly Dictionary<ushort, AsyncKeyCode> AsyncPhysicallyDown = [];
    private static readonly HashSet<ushort> AsyncLogicallyDown = [];
    private static readonly Dictionary<ushort, long> AsyncLastValidPressTick = [];
    private static readonly Dictionary<ushort, long> AsyncLastReleaseTick = [];
    private static readonly List<AsyncKeyCode> ReconnectedHoldKeysThisFrame = [];
    private static readonly Dictionary<(scrPlayer Player, AnyKeyCode Key), EarlyReleaseInfo> PendingEarlyHoldReleases = [];
    private static readonly Dictionary<scrPlayer, EarlyReleaseInfo> ConfirmedEarlyHoldReleases = [];
    private static readonly List<(scrPlayer Player, AnyKeyCode Key)> RemoveScratch = [];
    public static long CurrentAsyncSimulationTick { get; set; }
    private static readonly Dictionary<KeyCode, long> SyncLastValidPressTick = [];
    private static readonly Dictionary<KeyCode, long> SyncLastReleaseTick = [];
    private static readonly HashSet<KeyCode> SyncLogicallyDown = [];
    private static readonly Dictionary<(KeyCode Key, ButtonState State), (int Frame, bool Result)> SyncFrameCache = [];
    public static int BlockedCount { get; private set; }
    public static string LastBlockedKey { get; private set; } = "-";
    public static double LastBlockedDeltaMs { get; private set; }
    public static void ResetStats() {
        BlockedCount = 0;
        LastBlockedKey = "-";
        LastBlockedDeltaMs = 0.0;
    }
    private static void RecordBlocked(string keyName, long deltaTicks) {
        BlockedCount++;
        LastBlockedKey = keyName;
        LastBlockedDeltaMs = deltaTicks / (double)TicksPerMillisecond;
    }
    public static long GetThresholdTicks() {
        float ms = Conf?.ThresholdMs ?? 0f;
        return ms <= 0f ? 0L : (long)Math.Round(ms * (double)TicksPerMillisecond);
    }
    public static long GetStopwatchTicks() =>
        (long)(Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
    public static void ClearAsyncState() {
        AsyncPhysicallyDown.Clear();
        AsyncLogicallyDown.Clear();
        ReconnectedHoldKeysThisFrame.Clear();
        PendingEarlyHoldReleases.Clear();
        ConfirmedEarlyHoldReleases.Clear();
        CurrentAsyncSimulationTick = 0L;
    }
    // True while the player is in the middle of a hold, before its release window; very short holds and holds
    // already in the release margin return false so a genuine release is never delayed.
    public static bool IsPlayerInProtectedHoldBody(scrPlayer player) {
        scrController controller = ADOBase.controller;
        if(player == null || !player.alive || controller == null || ADOBase.conductor == null) return false;
        if(controller.state != States.PlayerControl) return false;
        if(controller.gameworld && !controller.requireHolding) return false;
        scrFloor floor = player.currFloor;
        if(floor == null || floor.holdLength <= -1) return false;
        if(controller.gameworld && GCS.checkpointNum == floor.seqID) return false;
        double bpm = ADOBase.conductor.bpm * controller.playerOne.planetarySystem.speed;
        float pitch = ADOBase.conductor.song != null ? ADOBase.conductor.song.pitch : 1f;
        double minAngleMargin = scrMisc.GetAdjustedAngleBoundaryInDeg(GCS.difficulty, (float)bpm, pitch).Counted
            * 0.01745329238474369;
        if(!controller.gameworld) {
            double totalAngle = Math.PI * (floor.holdLength * 2 + 1);
            if(totalAngle <= 0.0) return false;
            return floor.holdCompletion <= 1.0 - minAngleMargin / totalAngle;
        }
        if(floor.angleLength <= 0.0) return false;
        float marginScale = floor.nextfloor != null ? (float)floor.nextfloor.marginScale : 1f;
        return floor.holdCompletion < 1.0 - minAngleMargin * marginScale / floor.angleLength;
    }
    public static bool HasPendingEarlyHoldRelease(scrPlayer player) =>
        player != null && player.alive && HasPendingFor(player, static _ => true);
    private static bool HasPendingFor(scrPlayer player, Func<object, bool> keyMatches) {
        if(player.currFloor == null || PendingEarlyHoldReleases.Count == 0) return false;
        int seqId = player.currFloor.seqID;
        foreach(KeyValuePair<(scrPlayer Player, AnyKeyCode Key), EarlyReleaseInfo> kvp in PendingEarlyHoldReleases)
            if(ReferenceEquals(kvp.Key.Player, player) && kvp.Value.FloorSeqId == seqId && keyMatches(kvp.Key.Key.value))
                return true;
        return false;
    }
    public static bool TryGetEarliestExpiredPendingReleaseTick(long upToTick, out long expireTick) {
        expireTick = 0L;
        if(PendingEarlyHoldReleases.Count == 0 || !HoldBounceProtection) return false;
        long thresholdTicks = GetThresholdTicks();
        if(thresholdTicks <= 0L) return false;
        long earliest = long.MaxValue;
        foreach(KeyValuePair<(scrPlayer Player, AnyKeyCode Key), EarlyReleaseInfo> kvp in PendingEarlyHoldReleases) {
            scrPlayer player = kvp.Key.Player;
            if(player == null || !player.alive || player.currFloor == null || player.currFloor.seqID != kvp.Value.FloorSeqId)
                continue;
            if(kvp.Key.Key.value is not AsyncKeyCode) continue;
            long candidate = kvp.Value.ReleaseTick + thresholdTicks;
            if(candidate <= upToTick && candidate < earliest) earliest = candidate;
        }
        if(earliest == long.MaxValue) return false;
        expireTick = earliest;
        return true;
    }
    private static void ClearPendingWhere(Func<(scrPlayer Player, AnyKeyCode Key), EarlyReleaseInfo, bool> match) {
        if(PendingEarlyHoldReleases.Count == 0) return;
        RemoveScratch.Clear();
        foreach(KeyValuePair<(scrPlayer Player, AnyKeyCode Key), EarlyReleaseInfo> kvp in PendingEarlyHoldReleases)
            if(match(kvp.Key, kvp.Value)) RemoveScratch.Add(kvp.Key);
        foreach((scrPlayer Player, AnyKeyCode Key) key in RemoveScratch) PendingEarlyHoldReleases.Remove(key);
        RemoveScratch.Clear();
    }
    private static void ClearPendingEarlyReleaseForAsyncKey(ushort rawKey) =>
        ClearPendingWhere((k, _) => k.Key.value is AsyncKeyCode ak && ak.key == rawKey);
    private static void ClearPendingEarlyReleaseForSyncKey(KeyCode key) =>
        ClearPendingWhere((k, _) => k.Key.value is KeyCode kc && kc == key);
    public static void CleanupStaleEarlyReleases(scrPlayer player) {
        if(player == null) return;
        int currentSeqId = player.alive && player.currFloor != null ? player.currFloor.seqID : -1;
        if(ConfirmedEarlyHoldReleases.TryGetValue(player, out EarlyReleaseInfo confirmed) && confirmed.FloorSeqId != currentSeqId)
            ConfirmedEarlyHoldReleases.Remove(player);
        ClearPendingWhere((k, info) => ReferenceEquals(k.Player, player) && (info.FloorSeqId != currentSeqId
            || (k.Key.value is AsyncKeyCode ak && AsyncLogicallyDown.Contains(ak.key))
            || (k.Key.value is KeyCode kc && SyncLogicallyDown.Contains(kc))));
    }
    public static bool TryConsumeConfirmedEarlyRelease(scrPlayer player, out float holdCompletionAtRelease) {
        holdCompletionAtRelease = 0f;
        if(player == null || !ConfirmedEarlyHoldReleases.TryGetValue(player, out EarlyReleaseInfo info)) return false;
        ConfirmedEarlyHoldReleases.Remove(player);
        if(!player.alive || player.currFloor == null || player.currFloor.seqID != info.FloorSeqId) return false;
        holdCompletionAtRelease = info.HoldCompletionAtRelease;
        return true;
    }
    private static bool IsKeyInProtectedHold(Func<object, bool> keyMatches) {
        scrPlayer[] players = ADOBase.playerManager?.players;
        if(players == null) return false;
        foreach(scrPlayer player in players) {
            if(player == null || !player.alive || player.holdKeys == null) continue;
            if(!IsPlayerInProtectedHoldBody(player) && !HasPendingFor(player, keyMatches)) continue;
            foreach(AnyKeyCode held in player.holdKeys)
                if(keyMatches(held.value)) return true;
        }
        return false;
    }
    private static bool IsAsyncKeyInProtectedHold(ushort rawKey) =>
        IsKeyInProtectedHold(v => v is AsyncKeyCode ak && ak.key == rawKey);
    private static bool IsSyncKeyInProtectedHold(KeyCode key) => IsKeyInProtectedHold(v => v is KeyCode kc && kc == key);
    // Returns true when the hold should survive this release (bounce inside the threshold, or already re-pressed).
    public static bool HandleHoldKeyReleaseCheck(scrPlayer player, AnyKeyCode holdKey, bool inProtectedBody, long nowTicks) {
        if(!HoldBounceProtection || player.currFloor == null) return false;
        long thresholdTicks = GetThresholdTicks();
        if(thresholdTicks <= 0L) return false;
        int seqId = player.currFloor.seqID;
        float completion = player.currFloor.holdCompletion;
        bool hasPending = PendingEarlyHoldReleases.TryGetValue((player, holdKey), out EarlyReleaseInfo pending)
            && pending.FloorSeqId == seqId;
        if(holdKey.value is AsyncKeyCode asyncKey) {
            if(AsyncLogicallyDown.Contains(asyncKey.key)) {
                if(!ReconnectedHoldKeysThisFrame.Contains(asyncKey)) ReconnectedHoldKeysThisFrame.Add(asyncKey);
                AsyncInputManager.keyMask.Add(asyncKey);
                AsyncInputManager.frameDependentKeyMask.Add(asyncKey);
                PendingEarlyHoldReleases.Remove((player, holdKey));
                return true;
            }
            if(!inProtectedBody && !hasPending) return false;
            long evalTick = CurrentAsyncSimulationTick > 0L ? CurrentAsyncSimulationTick : AsyncInputManager.currFrameTick;
            if(AsyncLastReleaseTick.TryGetValue(asyncKey.key, out long releasedAt)
                && Math.Max(0L, evalTick - releasedAt) < thresholdTicks) {
                if(!hasPending) PendingEarlyHoldReleases[(player, holdKey)] = new EarlyReleaseInfo(seqId, completion, releasedAt);
                return true;
            }
            PendingEarlyHoldReleases.Remove((player, holdKey));
            ConfirmedEarlyHoldReleases[player] = new EarlyReleaseInfo(seqId,
                hasPending ? pending.HoldCompletionAtRelease : completion, hasPending ? pending.ReleaseTick : evalTick);
            return false;
        }
        if(holdKey.value is KeyCode syncKey) {
            if(SyncLogicallyDown.Contains(syncKey)) {
                SyncLastReleaseTick.Remove(syncKey);
                PendingEarlyHoldReleases.Remove((player, holdKey));
                return true;
            }
            if(!inProtectedBody && !hasPending) return false;
            if(SyncLastReleaseTick.TryGetValue(syncKey, out long releasedAt)) {
                long elapsed = nowTicks - releasedAt;
                if(elapsed >= 0L && elapsed < thresholdTicks) {
                    if(!hasPending) PendingEarlyHoldReleases[(player, holdKey)] = new EarlyReleaseInfo(seqId, completion, releasedAt);
                    return true;
                }
            }
            PendingEarlyHoldReleases.Remove((player, holdKey));
            ConfirmedEarlyHoldReleases[player] = new EarlyReleaseInfo(seqId,
                hasPending ? pending.HoldCompletionAtRelease : completion, nowTicks);
            return false;
        }
        return false;
    }
    public static void RestoreReconnectedAsyncHoldKeys() {
        foreach(AsyncKeyCode key in ReconnectedHoldKeysThisFrame) {
            AsyncInputManager.keyMask.Add(key);
            AsyncInputManager.frameDependentKeyMask.Add(key);
        }
    }
}
