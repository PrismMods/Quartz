using System;
using System.Collections.Generic;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal static class CountdownWorld {
    internal static int CurrentFrame => Time.frameCount;
    internal static int AutomaticTileSafetyLimit => Math.Max(1, (ADOBase.lm?.listFloors?.Count ?? 0) + 1);
    internal static bool IsAsyncInputActive => AsyncInputManager.isActive;
    internal static int ResolveStartFloor(int requestedFloor) =>
        requestedFloor >= 0 ? requestedFloor : GCS.checkpointNum;
    internal static bool CanArm(scrController controller, int startFloor) =>
        ADOBase.isLevelEditor && startFloor > 0 && controller != null && controller == ADOBase.controller;
    internal static bool CanPrepareInitialScrub(scrController controller, int floorNumber) =>
        controller != null && ADOBase.isLevelEditor && floorNumber > 0 && floorNumber == GCS.checkpointNum;
    internal static bool CanHandleMusicScheduled(scrController controller) =>
        controller != null && controller == ADOBase.controller && ADOBase.isLevelEditor && GCS.checkpointNum > 0;
    internal static bool IsRuntimeValid(scrController controller) =>
        ADOBase.isLevelEditor
        && GCS.checkpointNum > 0
        && controller != null
        && controller == ADOBase.controller
        && ADOBase.conductor != null;
    internal static bool IsPauseRequest(scrController controller) => controller != null && !controller.paused;
    internal static void EnterPlayerControl(scrController controller) =>
        controller.ChangeState(States.PlayerControl);
    internal static scrPlayer GetPrimaryPlayer(scrController controller) => controller?.playerOne;
    internal static bool HasChosenPlanet(scrPlayer player) => player?.planetarySystem?.chosenPlanet != null;
    internal static bool IsNextTileAutomatic(scrPlayer player) {
        scrFloor next = player?.currFloor?.nextfloor;
        return next != null && next.auto;
    }
    internal static void AdvanceAutomaticTiles() {
        bool previousAuto = RDC.auto;
        RDC.auto = true;
        try {
            foreach(scrPlayer player in ADOBase.playerManager) {
                if(player?.currFloor?.nextfloor == null || !player.currFloor.nextfloor.auto) continue;
                player.keyTimes.Clear();
                if(!player.Hit(isAuto: true))
                    throw new InvalidOperationException(
                        $"Could not advance automatic tile {player.currFloor.nextfloor.seqID}.");
            }
        } finally {
            RDC.auto = previousAuto;
        }
    }
    internal static bool HasFollowingTile(scrPlayer player) => player?.currFloor?.nextfloor != null;
    internal static int GetCurrentFloorId(scrPlayer player) => player.currFloor.seqID;
    internal static PerfectTimingInput GetPerfectTimingInput(scrPlayer player, double crotchet) {
        scrPlanet planet = player?.planetarySystem?.chosenPlanet;
        if(planet == null || planet.player == null || planet.planetarySystem == null)
            throw new InvalidOperationException("A valid chosen planet is required to calculate PP time.");
        return new PerfectTimingInput(
            planet.player.lastHit,
            planet.targetExitAngle,
            planet.snappedLastAngle,
            planet.planetarySystem.isCW,
            crotchet,
            planet.planetarySystem.speed
        );
    }
    internal static void SeekLoadedWorld(double logicalSongPosition, double audioSongPosition) {
        scrConductor conductor = ADOBase.conductor;
        if(conductor == null || ADOBase.playerManager == null)
            throw new InvalidOperationException("The conductor or player manager is unavailable.");
        Dictionary<scrPlayer, double> lastHits = [];
        foreach(scrPlayer player in ADOBase.playerManager) lastHits[player] = player.lastHit;
        conductor.dspTime = AudioSettings.dspTime;
        conductor.ScrubMusicToTime(audioSongPosition);
        conductor.songposition_minusi = logicalSongPosition;
        foreach(KeyValuePair<scrPlayer, double> pair in lastHits) {
            pair.Key.lastHit = pair.Value;
            pair.Key.planetarySystem?.chosenPlanet?.Update_RefreshAngles();
        }
    }
    internal static bool UnlockInputIfNeeded(scrPlayer player) {
        if(player.responsive || player.lockInput <= 0f) return false;
        player.UnlockInput();
        return true;
    }
    internal static bool ValidInputWasTriggered(scrPlayer player) => player.ValidInputWasTriggered();
    internal static string DescribeInput(scrPlayer player) {
        scrPlanet planet = player.planetarySystem?.chosenPlanet;
        return planet == null
            ? "accepting frozen input"
            : $"accepting frozen input at angle {planet.angle:F6}, target {planet.targetExitAngle:F6}, "
                + $"delta {planet.angle - planet.targetExitAngle:F6}, responsive {player.responsive}";
    }
    internal static bool CanRetryHit(scrPlayer player) =>
        player.alive && player.currFloor != null && player.currFloor.nextfloor != null;
    internal static bool Hit(scrPlayer player) => player.Hit(isAuto: false);
    internal static void UpdateInput(scrController controller) => controller.UpdateInput();
    internal static void Log(string message) => MainCore.Log.Msg("[Countdown] " + message);
    internal static void Warn(Exception e, string context) => Diag.Warn(e, "Countdown/" + context);
}
