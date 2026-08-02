using System;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal sealed class CountdownAudio {
    private scrConductor conductor;
    private float preparedAudioTime;
    private bool hasPreparedAudioTime;
    internal bool IsAvailable => conductor != null;
    internal double CurrentSongPosition => conductor.songposition_minusi;
    internal double Crotchet => conductor.crotchetAtStart;
    internal float Pitch => conductor.song.pitch;
    internal double Calibration => scrConductor.calibration_i;
    internal AudioRuntimeSnapshot CaptureAndFreeze() {
        scrConductor currentConductor = ADOBase.conductor;
        if(currentConductor == null || ADOBase.playerManager == null)
            throw new InvalidOperationException("The conductor or player manager is unavailable.");
        conductor = currentConductor;
        AudioRuntimeSnapshot snapshot = new(
            Time.timeScale,
            AudioListener.pause,
            conductor.enabled,
            conductor.songposition_minusi
        );
        Time.timeScale = 0f;
        conductor.enabled = false;
        return snapshot;
    }
    internal void PrimeSongSources() {
        if(!hasPreparedAudioTime && conductor.song != null && conductor.song.clip != null) {
            preparedAudioTime = conductor.song.time;
            hasPreparedAudioTime = true;
        }
        if(conductor.song != null && ADOBase.controller != null && ADOBase.controller.startVolume > 0f)
            conductor.song.volume = ADOBase.controller.startVolume;
        PrimeSongSource(conductor.song);
        PrimeSongSource(conductor.song2);
        PrimeSongSource(conductor.song3);
    }
    internal void PauseListener() => AudioListener.pause = true;
    internal double GetInputElapsedSeconds(ulong? inputTick) {
        if(!inputTick.HasValue || !AsyncInputManager.isActive) return 0.0;
        ulong currentTick = (ulong)DateTime.Now.Ticks;
        return currentTick > inputTick.Value ? (currentTick - inputTick.Value) / 10000000.0 : 0.0;
    }
    internal bool RebaseAtFrozenTime(double frozenSongPosition, double elapsedSinceFirstInput = 0.0) {
        if(conductor?.song == null || conductor.song.pitch == 0f) return false;
        double now = AudioSettings.dspTime;
        conductor.dspTime = now;
        conductor.prev_dspTime = now;
        conductor.dspTimeSong =
            now
            - elapsedSinceFirstInput
            - scrConductor.calibration_i
            - (frozenSongPosition + conductor.addoffset) / conductor.song.pitch;
        double resumedSongPosition = frozenSongPosition + elapsedSinceFirstInput * conductor.song.pitch;
        conductor.songposition_minusi = resumedSongPosition;
        conductor.deltaSongPos = 0.0;
        CountdownWorld.Log(
            $"rebased frozen audio: frozenSong={frozenSongPosition:F6}, "
                + $"inputElapsedMs={elapsedSinceFirstInput * 1000.0:F3}, resumedSong={resumedSongPosition:F6}, "
                + $"dsp={now:F6}, dspTimeSong={conductor.dspTimeSong:F6}, "
                + $"calibration={scrConductor.calibration_i:F6}, pitch={conductor.song.pitch:F4}, "
                + $"addoffset={conductor.addoffset:F6}");
        return true;
    }
    internal void AdvanceAndReleasePrimedSources(double elapsedSinceFirstInput) {
        if(elapsedSinceFirstInput > 0.0) {
            AdvancePrimedSongSource(conductor.song, elapsedSinceFirstInput);
            AdvancePrimedSongSource(conductor.song2, elapsedSinceFirstInput);
            AdvancePrimedSongSource(conductor.song3, elapsedSinceFirstInput);
        }
        UnpauseSongSources();
        AudioListener.pause = false;
    }
    internal void RefreezePrimedSources() {
        PrimeSongSources();
        AudioListener.pause = true;
    }
    internal void Restore(AudioRuntimeSnapshot snapshot, bool unpausePrimedSources, bool logSongSources) {
        try {
            if(conductor != null) conductor.enabled = snapshot.ConductorEnabled;
            if(unpausePrimedSources && conductor != null) UnpauseSongSources();
            AudioListener.pause = snapshot.ListenerPaused;
            Time.timeScale = snapshot.TimeScale;
            if(logSongSources && conductor?.song != null)
                CountdownWorld.Log(
                    $"resumed song sources: playing={conductor.song.isPlaying}, time={conductor.song.time:F3}, "
                        + $"volume={conductor.song.volume:F3}, listenerPaused={AudioListener.pause}, "
                        + $"songPos={conductor.songposition_minusi:F6}");
        } finally {
            conductor = null;
            preparedAudioTime = 0f;
            hasPreparedAudioTime = false;
        }
    }
    internal static void RebaseAsyncInputClock() {
        if(!AsyncInputManager.isActive || !CountdownAsyncClock.Available) return;
        scrConductor activeConductor = ADOBase.conductor;
        ulong wallTickBefore = (ulong)DateTime.Now.Ticks;
        double currentDspTime = AudioSettings.dspTime;
        ulong wallTickAfter = (ulong)DateTime.Now.Ticks;
        ulong nowTick = wallTickBefore + (wallTickAfter - wallTickBefore) / 2UL;
        ulong currentDspTick = (ulong)Math.Max(0.0, currentDspTime * TimeSpan.TicksPerSecond);
        ulong newOffsetTick = nowTick >= currentDspTick ? nowTick - currentDspTick : 0UL;
        double staleConductorDsp = activeConductor?.dspTime ?? double.NaN;
        ulong previousOffsetTick = CountdownAsyncClock.OffsetTick;
        CountdownAsyncClock.Rebase(nowTick, newOffsetTick);
        if(activeConductor == null || activeConductor.song == null || activeConductor.song.pitch == 0f) {
            CountdownWorld.Log(
                $"rebased async input clock with no active song: wallTick={nowTick}, dsp={currentDspTime:F6}, "
                    + $"offsetDeltaMs={((double)newOffsetTick - previousOffsetTick) / TimeSpan.TicksPerMillisecond:F3}");
            return;
        }
        double pitch = activeConductor.song.pitch;
        double expectedSongPosition =
            (currentDspTime - activeConductor.dspTimeSong - scrConductor.calibration_i) * pitch
            - activeConductor.addoffset;
        double currentSongPosition = activeConductor.songposition_minusi;
        activeConductor.dspTime = currentDspTime;
        activeConductor.prev_dspTime = currentDspTime;
        CountdownWorld.Log(
            $"rebased async input clock: staleDspMs={(currentDspTime - staleConductorDsp) * 1000.0:F3}, "
                + $"offsetDeltaMs={((double)newOffsetTick - previousOffsetTick) / TimeSpan.TicksPerMillisecond:F3}, "
                + $"currentSong={currentSongPosition:F6}, expectedSong={expectedSongPosition:F6}, "
                + $"storedSongErrorMs={(currentSongPosition - expectedSongPosition) / pitch * 1000.0:F3}");
    }
    private void PrimeSongSource(AudioSource source) {
        if(source == null || source.clip == null) return;
        source.Play();
        source.time = Mathf.Clamp(preparedAudioTime, 0f, source.clip.length);
        source.Pause();
    }
    private void AdvancePrimedSongSource(AudioSource source, double elapsedSinceFirstInput) {
        if(source == null || source.clip == null) return;
        float resumedTime = preparedAudioTime + (float)(elapsedSinceFirstInput * source.pitch);
        source.time = Mathf.Clamp(resumedTime, 0f, source.clip.length);
    }
    private void UnpauseSongSources() {
        try {
            conductor.song?.UnPause();
            conductor.song2?.UnPause();
            conductor.song3?.UnPause();
        } catch(Exception e) {
            Diag.Warn(e, "Countdown/UnpauseSong");
        }
    }
}
