using System;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal sealed partial class CountdownMetronome {
    private const double MinimumInitialBpm = 200.0;
    private const double MaximumInitialBpm = 500.0;
    private const double SchedulingLeadSeconds = 0.05;
    private CountdownPanel controlPanel;
    private CountdownDisplay display;
    private GameObject metronomeObject;
    private AudioSource metronomeSource;
    private AudioClip metronomeLoopClip;
    private AudioSource pendingSource;
    private AudioClip pendingLoopClip;
    private MetronomePlayback playback;
    private MetronomePlayback pendingPlayback;
    private MetronomeSettings activeSettings;
    private MetronomeSettings pendingSettings;
    private double defaultClickBpm;
    private bool hasPlayback;
    private bool hasPendingPlayback;
    private bool disableRequested;
    private bool enabledForSession = true;
    internal bool IsEnabledForSession => CountdownFeature.MetronomeActive && enabledForSession;
    internal bool IsUiConsumingInput => controlPanel?.IsConsumingInput == true;
    internal bool ConsumeDisableRequest() {
        if(!disableRequested) return false;
        disableRequested = false;
        return true;
    }
    internal MetronomePlayback? Start() {
        Stop();
        if(!IsEnabledForSession) return null;
        CountdownSettings conf = CountdownFeature.Conf;
        scrConductor conductor = ADOBase.conductor;
        if(conductor == null) return null;
        double originalInterval = Math.Abs(conductor.GetCountdownTime(1) - conductor.GetCountdownTime(0));
        if(originalInterval <= 0.0 || double.IsNaN(originalInterval) || double.IsInfinity(originalInterval)) {
            CountdownWorld.Log("skipped the metronome: the game returned an invalid countdown interval");
            return null;
        }
        double originalBpm = 60.0 / originalInterval;
        defaultClickBpm = NormalizeInitialBpm(originalBpm);
        MetronomeSettings settings = new(
            conf.UseCustomBpm ? conf.ClickBpm : defaultClickBpm,
            conf.Numerator,
            conf.Denominator
        );
        try {
            AudioClip hatClip = AudioManager.Instance.FindOrLoadAudioClip("sndHat");
            if(hatClip == null) {
                CountdownWorld.Log("skipped the metronome: sndHat could not be loaded");
                return null;
            }
            metronomeLoopClip = CreateLoopClip(hatClip, TryLoadKickClip(), settings, out int loopFrames);
            metronomeObject = new GameObject("Quartz Countdown Metronome");
            UnityEngine.Object.DontDestroyOnLoad(metronomeObject);
            metronomeSource = CreateSource(metronomeLoopClip, conductor.hitSoundVolume, conductor.hitSoundGroup);
            double clickInterval = (double)loopFrames / hatClip.frequency;
            double dspStartTime = AudioSettings.dspTime + SchedulingLeadSeconds;
            metronomeSource.PlayScheduled(dspStartTime);
            playback = new MetronomePlayback(
                originalBpm,
                settings.ClickBpm,
                Time.realtimeSinceStartupAsDouble,
                dspStartTime,
                clickInterval,
                loopFrames
            );
            hasPlayback = true;
            activeSettings = settings;
            CreateOverlays(settings);
            CountdownWorld.Log(
                $"metronome started at {settings.ClickBpm:F1} BPM, {settings.Numerator}/{settings.Denominator} "
                    + $"(game countdown {originalBpm:F3} BPM)");
            return playback;
        } catch(Exception e) {
            CountdownWorld.Warn(e, "MetronomeStart");
            Stop("startup failed");
            return null;
        }
    }
    private void CreateOverlays(MetronomeSettings settings) {
        CountdownSettings conf = CountdownFeature.Conf;
        if(conf.ShowMetronomeIcon) {
            try {
                display = CountdownDisplay.Create(playback);
            } catch(Exception e) {
                CountdownWorld.Warn(e, "MetronomeDisplay");
                display = null;
            }
        }
        if(!conf.ShowControlPanel) return;
        try {
            controlPanel = CountdownPanel.Create(settings, defaultClickBpm, RequestSettings, RequestDisable);
        } catch(Exception e) {
            CountdownWorld.Warn(e, "MetronomePanel");
            controlPanel = null;
        }
    }
    internal void UpdateDisplay() {
        PromotePendingPlaybackIfDue();
        if(display != null && metronomeSource != null)
            display.Tick(metronomeSource.timeSamples, metronomeSource.isPlaying);
    }
    internal void ResetSessionState() {
        Stop();
        activeSettings = default;
        pendingSettings = default;
        defaultClickBpm = 0.0;
        enabledForSession = true;
        disableRequested = false;
    }
    internal void Stop(string reason = null) {
        bool wasRunning = metronomeObject != null || display != null || controlPanel != null;
        controlPanel?.Dispose();
        display?.Dispose();
        metronomeSource?.Stop();
        pendingSource?.Stop();
        DestroyObject(metronomeObject);
        DestroyObject(metronomeLoopClip);
        DestroyObject(pendingLoopClip);
        controlPanel = null;
        display = null;
        metronomeSource = null;
        metronomeObject = null;
        metronomeLoopClip = null;
        pendingSource = null;
        pendingLoopClip = null;
        playback = default;
        pendingPlayback = default;
        activeSettings = default;
        pendingSettings = default;
        hasPlayback = false;
        hasPendingPlayback = false;
        if(wasRunning && !string.IsNullOrEmpty(reason)) CountdownWorld.Log($"metronome stopped: {reason}");
    }
    private void RequestSettings(MetronomeSettings requested) {
        PromotePendingPlaybackIfDue();
        MetronomeSettings comparison = hasPendingPlayback ? pendingSettings : activeSettings;
        PersistSettings(requested);
        if(!hasPlayback || metronomeSource == null || requested == comparison) {
            controlPanel?.SetSettings(requested);
            return;
        }
        if(requested.ClickBpm.Equals(comparison.ClickBpm) && requested.Numerator == comparison.Numerator) {
            if(hasPendingPlayback) pendingSettings = requested;
            else activeSettings = requested;
            controlPanel?.SetSettings(requested);
            return;
        }
        AudioClip replacementClip = null;
        AudioSource replacementSource = null;
        try {
            AudioClip hatClip = AudioManager.Instance.FindOrLoadAudioClip("sndHat")
                ?? throw new InvalidOperationException("sndHat could not be loaded for the metronome change.");
            replacementClip = CreateLoopClip(hatClip, TryLoadKickClip(), requested, out int loopFrames);
            replacementSource = CreateSource(replacementClip, metronomeSource.volume, metronomeSource.outputAudioMixerGroup);
            double transitionTime = NextSafeTickTime(playback, AudioSettings.dspTime + SchedulingLeadSeconds);
            MetronomePlayback replacementPlayback = new(
                playback.OriginalBpm,
                requested.ClickBpm,
                Time.realtimeSinceStartupAsDouble,
                transitionTime,
                (double)loopFrames / hatClip.frequency,
                loopFrames
            );
            replacementSource.PlayScheduled(transitionTime);
            metronomeSource.SetScheduledEndTime(transitionTime);
            CancelPendingPlayback();
            pendingLoopClip = replacementClip;
            pendingSource = replacementSource;
            pendingPlayback = replacementPlayback;
            pendingSettings = requested;
            hasPendingPlayback = true;
            replacementClip = null;
            replacementSource = null;
            controlPanel?.SetSettings(requested);
        } catch(Exception e) {
            replacementSource?.Stop();
            DestroyObject(replacementSource);
            DestroyObject(replacementClip);
            controlPanel?.SetSettings(requested);
            CountdownWorld.Warn(e, "MetronomeChange");
        }
    }
    private static void PersistSettings(MetronomeSettings requested) {
        CountdownSettings conf = CountdownFeature.Conf;
        conf.ClickBpm = (float)requested.ClickBpm;
        conf.UseCustomBpm = true;
        conf.Numerator = requested.Numerator;
        conf.Denominator = requested.Denominator;
        CountdownFeature.Save();
    }
    private void RequestDisable() {
        enabledForSession = false;
        disableRequested = true;
    }
    private void PromotePendingPlaybackIfDue() {
        if(!hasPendingPlayback || AudioSettings.dspTime < pendingPlayback.DspStartTime) return;
        AudioSource previousSource = metronomeSource;
        AudioClip previousClip = metronomeLoopClip;
        metronomeSource = pendingSource;
        metronomeLoopClip = pendingLoopClip;
        playback = pendingPlayback;
        activeSettings = pendingSettings;
        pendingSource = null;
        pendingLoopClip = null;
        pendingPlayback = default;
        pendingSettings = default;
        hasPendingPlayback = false;
        display?.SetPlayback(playback);
        DestroyObject(previousSource);
        DestroyObject(previousClip);
    }
    private void CancelPendingPlayback() {
        pendingSource?.Stop();
        DestroyObject(pendingSource);
        DestroyObject(pendingLoopClip);
        pendingSource = null;
        pendingLoopClip = null;
        pendingPlayback = default;
        pendingSettings = default;
        hasPendingPlayback = false;
    }
    private AudioSource CreateSource(AudioClip clip, float volume, UnityEngine.Audio.AudioMixerGroup mixerGroup) {
        AudioSource source = metronomeObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.pitch = 1f;
        source.priority = 10;
        source.volume = volume * Mathf.Clamp01(CountdownFeature.Conf.Volume);
        source.outputAudioMixerGroup = mixerGroup;
        source.ignoreListenerPause = true;
        source.clip = clip;
        return source;
    }
    private static void DestroyObject(UnityEngine.Object target) {
        if(target != null) UnityEngine.Object.Destroy(target);
    }
    private static double NextSafeTickTime(MetronomePlayback activePlayback, double earliestTime) {
        if(earliestTime <= activePlayback.DspStartTime) return activePlayback.DspStartTime;
        double elapsedTicks = (earliestTime - activePlayback.DspStartTime) / activePlayback.ClickInterval;
        return activePlayback.DspStartTime + Math.Ceiling(elapsedTicks) * activePlayback.ClickInterval;
    }
    private static double NormalizeInitialBpm(double bpm) {
        while(bpm < MinimumInitialBpm) bpm *= 2.0;
        while(bpm > MaximumInitialBpm) bpm *= 0.5;
        return bpm;
    }
    private static AudioClip TryLoadKickClip() {
        try {
            return AudioManager.Instance.FindOrLoadAudioClip("sndKick");
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
}
