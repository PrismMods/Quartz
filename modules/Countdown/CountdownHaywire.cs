using System;
using System.Collections.Generic;
using ADOFAI;
using Quartz.Core;
namespace Quartz.Features.Countdown;
internal static class CountdownHaywire {
    private static readonly List<StretchedFloorState> ModifiedFloors = [];
    private static List<scrFloor> stretchedFloorsRef;
    private static bool audioShiftApplied;
    internal static double TimelineShift { get; private set; }
    internal static bool SpeedOverrideActive { get; private set; }
    internal static bool AttemptPending { get; set; }
    internal static int PendingCheckpoint { get; set; } = -1;
    internal static void RestoreSpeeds() {
        if(ModifiedFloors.Count == 0) return;
        scrLevelMaker maker = scrLevelMaker.instance;
        List<scrFloor> floors = maker?.listFloors;
        if(maker != null && floors != null && ReferenceEquals(floors, stretchedFloorsRef)) {
            foreach(StretchedFloorState state in ModifiedFloors) {
                if(state.Index >= floors.Count) continue;
                scrFloor floor = floors[state.Index];
                floor.speed = state.Speed;
                floor.extraBeats = state.ExtraBeats;
                floor.holdLength = state.HoldLength;
            }
            maker.CalculateFloorEntryTimes();
            RebakeFxStartTimes(floors);
        }
        ClearBookkeeping();
    }
    internal static void ClearBookkeeping() {
        ModifiedFloors.Clear();
        stretchedFloorsRef = null;
        SpeedOverrideActive = false;
        TimelineShift = 0.0;
        audioShiftApplied = false;
    }
    internal static void ApplyStretch(int checkpoint = -1) {
        try {
            RestoreSpeeds();
            Stretch(checkpoint);
        } catch(Exception e) {
            CountdownWorld.Warn(e, "Haywire");
            ClearBookkeeping();
        }
    }
    private static void Stretch(int checkpoint) {
        int cp = checkpoint >= 0
            ? checkpoint
            : PendingCheckpoint >= 0 ? PendingCheckpoint : GCS.checkpointNum;
        if(!CountdownFeature.HaywireActive || cp == 0) return;
        scrConductor conductor = scrConductor.instance;
        scrLevelMaker maker = scrLevelMaker.instance;
        List<scrFloor> floors = maker?.listFloors;
        if(conductor == null || conductor.song == null || maker == null || floors == null) return;
        if(cp <= 0 || cp >= floors.Count - 1) return;
        scrFloor cpFloor = floors[cp];
        double tickBpm = (double)conductor.bpm
            * cpFloor.speed
            * (cpFloor.numPlanets * 0.5f)
            * conductor.song.pitch
            * conductor.countdownSpeedMultiplier;
        if(tickBpm <= 0.0 || double.IsNaN(tickBpm) || double.IsInfinity(tickBpm)) return;
        CountdownSettings conf = CountdownFeature.Conf;
        double factor = ChoosePow2Factor(tickBpm, conf.MinBpm, Math.Max(conf.MinBpm, conf.MaxBpm));
        if(factor == 1.0) return;
        int firstStretched = Math.Max(1, cp - 5);
        double firstHitEntryOld = floors[cp + 1].entryTime;
        float heldBeats = 0f;
        double holdBeatDrift = 0.0;
        for(int i = firstStretched; i <= cp; i++) {
            scrFloor floor = floors[i];
            ModifiedFloors.Add(new StretchedFloorState(i, floor.speed, floor.extraBeats, floor.holdLength));
            floor.speed *= (float)factor;
            if(floor.extraBeats > 0f) {
                heldBeats += floor.extraBeats;
                floor.extraBeats *= (float)factor;
            }
            if(floor.holdLength <= 0) continue;
            int scaledHold = Math.Max(0, (int)Math.Round(floor.holdLength * factor, MidpointRounding.AwayFromZero));
            heldBeats += floor.holdLength * 2;
            holdBeatDrift += (scaledHold / factor - floor.holdLength) * 2;
            floor.holdLength = scaledHold;
        }
        maker.CalculateFloorEntryTimes();
        RebakeFxStartTimes(floors);
        TimelineShift = floors[cp + 1].entryTime - firstHitEntryOld;
        stretchedFloorsRef = floors;
        SpeedOverrideActive = true;
        audioShiftApplied = false;
        CountdownWorld.Log(
            $"haywire clamp: {tickBpm:0.#} BPM -> {tickBpm * factor:0.#} BPM "
                + $"(speed x{factor} on tiles {firstStretched}-{cp}, timeline +{TimelineShift:0.###}s)");
        if(heldBeats > 0f)
            CountdownWorld.Log(
                $"haywire held {heldBeats:0.###} pause/free-roam/hold beats at their real length "
                    + $"(extraBeats and holdLength x{factor} on the stretched tiles"
                    + (Math.Abs(holdBeatDrift) > 0.0005
                        ? $", {holdBeatDrift:+0.###;-0.###} beats of hold rounding"
                        : "")
                    + ")");
    }
    internal static double ClaimAudioShift(scrConductor conductor, double scrubTime) {
        if(audioShiftApplied || !SpeedOverrideActive || TimelineShift == 0.0) return 0.0;
        if(conductor == null || conductor.song == null || conductor.song.clip == null) return 0.0;
        float pitch = conductor.song.pitch;
        if(pitch <= 0f || float.IsNaN(pitch) || float.IsInfinity(pitch)) return 0.0;
        audioShiftApplied = true;
        double countdownLead =
            conductor.separateCountdownTime ? conductor.crotchetAtStart * conductor.countdownTicks : 0.0;
        double baseSongTime = scrubTime + conductor.addoffset - countdownLead;
        double limit = Math.Max(0.0, conductor.song.clip.length - 0.01);
        double target = Math.Min(limit, Math.Max(0.0, baseSongTime - TimelineShift));
        double shift = baseSongTime - target;
        double residual = TimelineShift - shift;
        if(Math.Abs(residual) > 0.0005)
            CountdownWorld.Log(
                $"haywire audio pull-back clamped inside the clip: moved {shift:0.###}s "
                    + $"of {TimelineShift:0.###}s ({residual:0.###}s residual desync)");
        else
            CountdownWorld.Log(
                $"haywire audio pull-back: scrub {scrubTime:0.###}s -> song {target:0.###}s "
                    + $"(shift {shift:0.###}s)");
        return shift;
    }
    internal static void RestoreLogicalClock(scrConductor conductor, double shift) {
        if(conductor == null || conductor.song == null) return;
        float pitch = conductor.song.pitch;
        if(pitch <= 0f || float.IsNaN(pitch) || float.IsInfinity(pitch)) return;
        conductor.dspTimeSong -= shift / pitch;
        if(ADOBase.playerManager == null) return;
        foreach(scrPlayer player in ADOBase.playerManager) {
            if(player != null) player.lastHit += shift;
        }
    }
    internal static void OnScrubCompleted() {
        if(SpeedOverrideActive) AttemptPending = false;
    }
    private static void RebakeFxStartTimes(List<scrFloor> floors) {
        scrConductor conductor = scrConductor.instance;
        if(conductor == null) return;
        foreach(scrFloor floor in floors) {
            foreach(ffxPlusBase fx in floor.GetComponents<ffxPlusBase>()) {
                try {
                    fx.SetStartTime(conductor.bpm, fx.degreeOffset);
                } catch(Exception e) {
                    Diag.Ignore(e);
                }
            }
        }
    }
    internal static double ChoosePow2Factor(double bpm, double min, double max) {
        double bestFactor = 1.0;
        double bestDistance = double.MaxValue;
        int bestSteps = int.MaxValue;
        for(int k = -12; k <= 12; k++) {
            double factor = Math.Pow(2.0, k);
            double result = bpm * factor;
            double distance = result < min ? min - result : result > max ? result - max : 0.0;
            int steps = Math.Abs(k);
            if(distance < bestDistance - 0.0001
                || (Math.Abs(distance - bestDistance) <= 0.0001 && steps < bestSteps)) {
                bestDistance = distance;
                bestSteps = steps;
                bestFactor = factor;
            }
        }
        return bestFactor;
    }
}
