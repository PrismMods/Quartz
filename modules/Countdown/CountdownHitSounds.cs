using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal sealed class CountdownHitSounds {
    private static readonly FieldInfo HitSoundsDataField = AccessTools.Field(typeof(scrConductor), "hitSoundsData");
    private static readonly FieldInfo NextHitSoundField =
        AccessTools.Field(typeof(scrConductor), "nextHitSoundToSchedule");
    private static readonly FieldInfo HoldSoundsDataField = AccessTools.Field(typeof(scrConductor), "holdSoundsData");
    private static readonly FieldInfo NextHoldSoundField =
        AccessTools.Field(typeof(scrConductor), "nextHoldSoundToSchedule");
    private static readonly FieldInfo ExtraTicksField = AccessTools.Field(typeof(scrConductor), "extraTicksCountdown");
    private static readonly FieldInfo NextExtraTickField =
        AccessTools.Field(typeof(scrConductor), "nextExtraTickToSchedule");
    private static readonly FieldInfo CountdownTimesField = AccessTools.Field(typeof(scrConductor), "countdownTimes");
    private static readonly FieldInfo PlayCountdownHihatsField =
        AccessTools.Field(typeof(scrConductor), "playCountdownHihats");
    private static readonly FieldInfo PlayEndingCymbalField =
        AccessTools.Field(typeof(scrConductor), "playEndingCymbal");
    private scrConductor conductor;
    private IList hitSounds;
    private IList holdSounds;
    private IList extraTicks;
    private double[] countdownTimes;
    private double scheduleDspTimeSong;
    private double endingCymbalTime;
    private ScheduledHitSound? missedHitSound;
    private bool playCountdownHihats;
    private bool playEndingCymbal;
    private bool prepared;
    private bool active;
    internal static string GetCompatibilityFailureReason() =>
        ContractIsAvailable() ? null : "the hit-sound schedule layout is incompatible";
    internal bool Prepare() {
        Reset();
        scrConductor current = ADOBase.conductor;
        if(current == null || !ContractIsAvailable()) return false;
        conductor = current;
        try {
            AudioManager.Instance.StopAllSounds();
            double currentDspTime = conductor.dspTime;
            try {
                conductor.dspTime = double.NegativeInfinity;
                conductor.PlayHitTimes();
            } finally {
                conductor.dspTime = currentDspTime;
            }
            hitSounds = HitSoundsDataField.GetValue(conductor) as IList;
            holdSounds = HoldSoundsDataField.GetValue(conductor) as IList;
            extraTicks = ExtraTicksField.GetValue(conductor) as IList;
            if(hitSounds == null || holdSounds == null || extraTicks == null)
                throw new InvalidOperationException("The conductor sound schedule lists are unavailable.");
            missedHitSound = CaptureFirstElapsed(currentDspTime);
            SetFirstFutureIndex(hitSounds, NextHitSoundField, currentDspTime);
            SetFirstFutureIndex(holdSounds, NextHoldSoundField, currentDspTime);
            SetFirstFutureIndex(extraTicks, NextExtraTickField, currentDspTime);
            countdownTimes = (CountdownTimesField.GetValue(conductor) as double[])?.Clone() as double[];
            playCountdownHihats = PlayCountdownHihatsField.GetValue(conductor) is true && !conductor.fastTakeoff;
            playEndingCymbal = PlayEndingCymbalField.GetValue(conductor) is true;
            endingCymbalTime = CalculateEndingCymbalTime();
            scheduleDspTimeSong = conductor.dspTimeSong;
            prepared = true;
            active = false;
            AudioManager.Instance.StopAllSounds();
            return true;
        } catch(Exception e) {
            CountdownWorld.Warn(e, "HitSounds/Prepare");
            RestoreNativeSchedule(conductor);
            return false;
        }
    }
    internal void Activate() {
        if(!prepared || active || conductor == null) return;
        try {
            double shift = conductor.dspTimeSong - scheduleDspTimeSong;
            ShiftSchedule(hitSounds, shift);
            ShiftSchedule(holdSounds, shift);
            ShiftSchedule(extraTicks, shift);
            ShiftCountdownTimes(shift);
            endingCymbalTime += shift;
            scheduleDspTimeSong = conductor.dspTimeSong;
            double now = Math.Max(conductor.dspTime, AudioSettings.dspTime);
            if(missedHitSound is ScheduledHitSound hitSound)
                AudioManager.Play("snd" + hitSound.SoundName, now, conductor.hitSoundGroup, hitSound.Volume);
            ScheduleDirectSounds(now);
            active = true;
        } catch(Exception e) {
            CountdownWorld.Warn(e, "HitSounds/Activate");
            RestoreNativeSchedule(conductor);
        }
    }
    internal void Refreeze() {
        if(!prepared || !active) return;
        AudioManager.Instance.StopAllSounds();
        active = false;
    }
    internal void Reset(bool keepInstalledSchedule = false) {
        if(!prepared) {
            ClearState();
            return;
        }
        scrConductor preparedConductor = conductor;
        ClearState();
        if(keepInstalledSchedule || preparedConductor == null) return;
        try {
            AudioManager.Instance.StopAllSounds();
            preparedConductor.PlayHitTimes();
        } catch(Exception e) {
            CountdownWorld.Warn(e, "HitSounds/Reset");
        }
    }
    private void RestoreNativeSchedule(scrConductor failed) {
        ClearState();
        if(failed == null) return;
        try {
            failed.PlayHitTimes();
        } catch(Exception e) {
            CountdownWorld.Warn(e, "HitSounds/RestoreNative");
        }
    }
    private static bool ContractIsAvailable() =>
        HitSoundsDataField != null
        && NextHitSoundField != null
        && HoldSoundsDataField != null
        && NextHoldSoundField != null
        && ExtraTicksField != null
        && NextExtraTickField != null
        && CountdownTimesField != null
        && PlayCountdownHihatsField != null
        && PlayEndingCymbalField != null;
    private void SetFirstFutureIndex(IList schedule, FieldInfo indexField, double currentDspTime) {
        int index = 0;
        while(index < schedule.Count && ReadTime(schedule[index]) <= currentDspTime) index++;
        indexField.SetValue(conductor, index);
    }
    private static void ShiftSchedule(IList schedule, double shift) {
        if(schedule == null || shift == 0.0) return;
        for(int index = 0; index < schedule.Count; index++) {
            object item = schedule[index];
            if(item == null) continue;
            Type itemType = item.GetType();
            FieldInfo timeField = AccessTools.Field(itemType, "time");
            if(timeField == null) throw new MissingFieldException(itemType.FullName, "time");
            timeField.SetValue(item, (double)timeField.GetValue(item) + shift);
            FieldInfo endTimeField = AccessTools.Field(itemType, "endTime");
            if(endTimeField != null && endTimeField.GetValue(item) is double endTime && endTime > 0.0)
                endTimeField.SetValue(item, endTime + shift);
            schedule[index] = item;
        }
    }
    private void ShiftCountdownTimes(double shift) {
        if(countdownTimes == null || shift == 0.0) return;
        for(int index = 0; index < countdownTimes.Length; index++) {
            if(countdownTimes[index] > 0.0) countdownTimes[index] += shift;
        }
        CountdownTimesField.SetValue(conductor, countdownTimes);
    }
    private void ScheduleDirectSounds(double now) {
        if(playCountdownHihats && countdownTimes != null) {
            foreach(double countdownTime in countdownTimes) {
                if(countdownTime > now)
                    AudioManager.Play("sndHat", countdownTime, conductor.hitSoundGroup, conductor.hitSoundVolume, 10);
            }
        }
        if(playEndingCymbal && endingCymbalTime > now)
            AudioManager.Play(
                "sndCymbalCrash", endingCymbalTime, conductor.hitSoundGroup, conductor.hitSoundVolume, 10);
    }
    private double CalculateEndingCymbalTime() {
        if(!playEndingCymbal || ADOBase.lm?.listFloors == null || ADOBase.lm.listFloors.Count == 0) return 0.0;
        int floorIndex = GCS.practiceMode
            ? Math.Min(GCS.checkpointNum + GCS.practiceLength, ADOBase.lm.listFloors.Count - 1)
            : ADOBase.lm.listFloors.Count - 1;
        return conductor.dspTimeSong
            + ADOBase.lm.listFloors[floorIndex].entryTimePitchAdj
            + conductor.addoffset / conductor.song.pitch;
    }
    private static double ReadTime(object item) {
        if(item == null) return double.PositiveInfinity;
        FieldInfo timeField = AccessTools.Field(item.GetType(), "time");
        if(timeField == null) throw new MissingFieldException(item.GetType().FullName, "time");
        return (double)timeField.GetValue(item);
    }
    private ScheduledHitSound? CaptureFirstElapsed(double currentDspTime) {
        object raw;
        try {
            raw = HitSoundsDataField?.GetValue(conductor);
        } catch(Exception e) {
            Diag.Warn(e, "Countdown/HitSounds");
            return null;
        }
        if(raw is not IEnumerable hitSoundsData) return null;
        foreach(object item in hitSoundsData) {
            if(item == null) continue;
            Type itemType = item.GetType();
            FieldInfo hitSoundField = AccessTools.Field(itemType, "hitSound");
            FieldInfo timeField = AccessTools.Field(itemType, "time");
            FieldInfo volumeField = AccessTools.Field(itemType, "volume");
            if(hitSoundField == null || timeField == null || volumeField == null) continue;
            if(timeField.GetValue(item) is not double time) continue;
            if(time > currentDspTime) return null;
            if(volumeField.GetValue(item) is not float volume) volume = 1f;
            return new ScheduledHitSound(hitSoundField.GetValue(item)?.ToString(), time, volume);
        }
        return null;
    }
    private void ClearState() {
        conductor = null;
        hitSounds = null;
        holdSounds = null;
        extraTicks = null;
        countdownTimes = null;
        scheduleDspTimeSong = 0.0;
        endingCymbalTime = 0.0;
        missedHitSound = null;
        playCountdownHihats = false;
        playEndingCymbal = false;
        prepared = false;
        active = false;
    }
}
