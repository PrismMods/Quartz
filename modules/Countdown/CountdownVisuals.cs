using System;
using System.Collections.Generic;
using DG.Tweening;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal sealed class CountdownVisuals {
    private const float FrozenVisualLeadRadians = 0.017453292f;
    private readonly Dictionary<scrPlayer, FrozenOrbitState> frozenOrbits = [];
    private scrVfxPlus frozenVfx;
    private double orbitStartedRealtime;
    internal static void HideStartUi(scrController controller) {
        controller.goShown = true;
        scrCountdown countdown = UnityEngine.Object.FindAnyObjectByType<scrCountdown>();
        countdown?.CancelGo();
        scrPressToStart prompt = UnityEngine.Object.FindAnyObjectByType<scrPressToStart>();
        prompt?.HideText();
    }
    internal void ScrubToTime(double logicalSongPosition) {
        if(scrVfxPlus.instance == null) return;
        frozenVfx = scrVfxPlus.instance;
        frozenVfx.pausedTweens.Clear();
        frozenVfx.ScrubToTime((float)logicalSongPosition);
        foreach(DG.Tweening.Tween tween in frozenVfx.pausedTweens) {
            if(tween != null && tween.active) tween.Pause();
        }
    }
    internal void StartPreLandingMotion(MetronomePlayback? playback) {
        RestoreOrbitAngles();
        if(!CountdownFeature.Conf.AnimatePlanets) return;
        orbitStartedRealtime = playback?.StartedRealtime ?? Time.realtimeSinceStartupAsDouble;
        foreach(scrPlayer player in ADOBase.playerManager) {
            scrPlanet chosenPlanet = player?.planetarySystem?.chosenPlanet;
            if(chosenPlanet == null) continue;
            PreparePlanetLayout(player, chosenPlanet);
            NormalizeOrbitRadius(player);
            SetRingOwnership(player, chosenPlanet);
            float direction = chosenPlanet.planetarySystem.isCW ? -1f : 1f;
            double travelRadians = playback == null ? FrozenVisualLeadRadians : CalculateTravelRadians(chosenPlanet);
            double duration = CalculateDuration(playback, travelRadians);
            if(duration <= 0.0) travelRadians = FrozenVisualLeadRadians;
            FrozenOrbitState orbit = new(chosenPlanet, chosenPlanet.cosmeticAngle, direction, travelRadians, duration);
            frozenOrbits[player] = orbit;
            ApplyOrbitAngle(orbit, phase: 0.0);
        }
    }
    internal void UpdatePreLandingMotion() {
        if(frozenOrbits.Count == 0) return;
        double elapsed = Math.Max(0.0, Time.realtimeSinceStartupAsDouble - orbitStartedRealtime);
        foreach(KeyValuePair<scrPlayer, FrozenOrbitState> pair in frozenOrbits) {
            FrozenOrbitState orbit = pair.Value;
            if(pair.Key == null || orbit.ChosenPlanet == null || orbit.Duration <= 0.0) continue;
            double phase = elapsed / orbit.Duration;
            phase -= Math.Floor(phase);
            ApplyOrbitAngle(orbit, phase);
        }
    }
    internal void RestorePlayer(scrPlayer player) {
        if(player == null || !frozenOrbits.TryGetValue(player, out FrozenOrbitState orbit)) return;
        RestoreOrbit(orbit);
        frozenOrbits.Remove(player);
    }
    internal void RestoreAll() {
        RestoreOrbitAngles();
        if(frozenVfx == null) return;
        foreach(DG.Tweening.Tween tween in frozenVfx.pausedTweens) {
            if(tween != null && tween.active) tween.Play();
        }
        frozenVfx.pausedTweens.Clear();
        frozenVfx = null;
    }
    private void RestoreOrbitAngles() {
        foreach(KeyValuePair<scrPlayer, FrozenOrbitState> pair in frozenOrbits) RestoreOrbit(pair.Value);
        frozenOrbits.Clear();
    }
    private static void NormalizeOrbitRadius(scrPlayer player) {
        scrFloor floor = player?.currFloor;
        List<scrPlanet> planets = player?.planetarySystem?.planetList;
        if(floor == null || planets == null || scrController.instance == null) return;
        float radius = scrController.instance.tileSize * floor.radiusScale;
        foreach(scrPlanet planet in planets) {
            if(planet != null) planet.cosmeticRadius = radius;
        }
    }
    private static void PreparePlanetLayout(scrPlayer player, scrPlanet chosenPlanet) {
        if(ADOBase.customLevel != null || RDC.debug || player?.currFloor == null) return;
        scrFloor floor = player.currFloor;
        chosenPlanet.transform.position = floor.stickToFloor ? floor.transform.position : floor.startPos;
        chosenPlanet.Update_RefreshAngles();
    }
    private static void SetRingOwnership(scrPlayer player, scrPlanet chosenPlanet) {
        List<scrPlanet> planets = player?.planetarySystem?.planetList;
        if(planets == null) return;
        foreach(scrPlanet planet in planets) {
            try {
                planet?.planetRenderer?.ringComp?.Switch(planet == chosenPlanet, instant: true);
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        }
    }
    private static double CalculateTravelRadians(scrPlanet planet) {
        double timingDirection = planet.planetarySystem.isCW ? 1.0 : -1.0;
        double travel = (planet.targetExitAngle - planet.snappedLastAngle) * timingDirection;
        return IsFinite(travel) && travel > FrozenVisualLeadRadians ? travel : FrozenVisualLeadRadians;
    }
    private static double CalculateDuration(MetronomePlayback? playback, double travelRadians) {
        if(playback == null) return 0.0;
        double movingRadians = travelRadians - FrozenVisualLeadRadians;
        if(movingRadians <= 0.0) return 0.0;
        double duration = movingRadians / Math.PI * playback.Value.OriginalBeatInterval;
        return IsFinite(duration) && duration > 0.0 ? duration : 0.0;
    }
    private static void ApplyOrbitAngle(FrozenOrbitState orbit, double phase) {
        double remainingRadians =
            FrozenVisualLeadRadians + (orbit.TravelRadians - FrozenVisualLeadRadians) * (1.0 - phase);
        orbit.ChosenPlanet.cosmeticAngle = orbit.OriginalAngle + (float)(remainingRadians * orbit.Direction);
        orbit.ChosenPlanet.Update_RefreshAngles();
    }
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static void RestoreOrbit(FrozenOrbitState orbit) {
        if(orbit.ChosenPlanet == null) return;
        orbit.ChosenPlanet.cosmeticAngle = orbit.OriginalAngle;
        orbit.ChosenPlanet.Update_RefreshAngles();
    }
    private readonly struct FrozenOrbitState(
        scrPlanet chosenPlanet,
        float originalAngle,
        float direction,
        double travelRadians,
        double duration
    ) {
        internal scrPlanet ChosenPlanet { get; } = chosenPlanet;
        internal float OriginalAngle { get; } = originalAngle;
        internal float Direction { get; } = direction;
        internal double TravelRadians { get; } = travelRadians;
        internal double Duration { get; } = duration;
    }
}
