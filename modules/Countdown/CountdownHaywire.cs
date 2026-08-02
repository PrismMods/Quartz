using System;
using System.Collections.Generic;
using ADOFAI;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal static class CountdownHaywire {
    private static readonly List<KeyValuePair<int, float>> ModifiedFloors = [];
    private static List<scrFloor> stretchedFloorsRef;
    internal static double TimelineShift { get; private set; }
    internal static bool SpeedOverrideActive { get; private set; }
    internal static bool AttemptPending { get; set; }
    internal static int PendingCheckpoint { get; set; } = -1;
    internal static void RestoreSpeeds() {
        if(ModifiedFloors.Count == 0) return;
        scrLevelMaker maker = scrLevelMaker.instance;
        List<scrFloor> floors = maker?.listFloors;
        if(maker != null && floors != null && ReferenceEquals(floors, stretchedFloorsRef)) {
            foreach(KeyValuePair<int, float> pair in ModifiedFloors) {
                if(pair.Key < floors.Count) floors[pair.Key].speed = pair.Value;
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
        for(int i = firstStretched; i <= cp; i++) {
            ModifiedFloors.Add(new KeyValuePair<int, float>(i, floors[i].speed));
            floors[i].speed *= (float)factor;
        }
        maker.CalculateFloorEntryTimes();
        RebakeFxStartTimes(floors);
        TimelineShift = floors[cp + 1].entryTime - firstHitEntryOld;
        stretchedFloorsRef = floors;
        SpeedOverrideActive = true;
        CountdownWorld.Log(
            $"haywire clamp: {tickBpm:0.#} BPM -> {tickBpm * factor:0.#} BPM "
                + $"(speed x{factor} on tiles {firstStretched}-{cp}, timeline +{TimelineShift:0.###}s)");
    }
    internal static void PullBackAudioSeek() {
        if(!SpeedOverrideActive || TimelineShift == 0.0) return;
        scrConductor conductor = scrConductor.instance;
        if(conductor == null || conductor.song == null || conductor.song.clip == null) return;
        float newTime = conductor.song.time - (float)TimelineShift;
        conductor.song.time = Mathf.Clamp(newTime, 0f, conductor.song.clip.length - 0.01f);
        AttemptPending = false;
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
