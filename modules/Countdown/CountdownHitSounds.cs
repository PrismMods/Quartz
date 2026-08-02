using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal static class CountdownHitSounds {
    private static readonly FieldInfo HitSoundsDataField = AccessTools.Field(typeof(scrConductor), "hitSoundsData");
    internal static void RebuildFromCheckpoint() {
        scrConductor conductor = ADOBase.conductor;
        if(conductor == null) return;
        AudioManager.Instance.StopAllSounds();
        double currentDspTime = conductor.dspTime;
        ScheduledHitSound? missedHitSound;
        try {
            conductor.dspTime = double.NegativeInfinity;
            conductor.PlayHitTimes();
            missedHitSound = CaptureFirstElapsed(conductor, currentDspTime);
            conductor.dspTime = currentDspTime;
            conductor.PlayHitTimes();
        } finally {
            conductor.dspTime = currentDspTime;
        }
        if(missedHitSound is not ScheduledHitSound hitSound) return;
        double playbackTime = Math.Max(currentDspTime, AudioSettings.dspTime);
        AudioManager.Play("snd" + hitSound.SoundName, playbackTime, conductor.hitSoundGroup, hitSound.Volume);
    }
    private static ScheduledHitSound? CaptureFirstElapsed(scrConductor conductor, double currentDspTime) {
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
}
