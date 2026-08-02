using System;
namespace Quartz.Features.Countdown;
internal sealed class CountdownSession(scrController controller) {
    internal scrController Controller { get; } = controller;
    internal FrozenStartPhase Phase { get; set; } = FrozenStartPhase.WaitingForScrub;
    internal AudioRuntimeSnapshot AudioSnapshot { get; set; }
    internal bool HasAudioSnapshot { get; set; }
    internal scrPlayer PendingInputPlayer { get; set; }
    internal ulong? PendingInputTick { get; set; }
    internal int FrozenFrame { get; set; }
    internal double FrozenSongPosition { get; set; }
    internal double FrozenAudioSongPosition { get; set; }
    internal bool AudioReleasedForInput { get; set; }
    internal void ClearPendingInput() {
        PendingInputPlayer = null;
        PendingInputTick = null;
    }
}
internal sealed class CountdownPreparer(CountdownAudio audio, CountdownMetronome metronome, CountdownVisuals visuals) {
    internal bool Prepare(CountdownSession session) {
        session.Phase = FrozenStartPhase.Preparing;
        session.AudioSnapshot = audio.CaptureAndFreeze();
        session.HasAudioSnapshot = true;
        session.FrozenSongPosition = session.AudioSnapshot.SongPosition;
        CountdownWorld.EnterPlayerControl(session.Controller);
        CountdownVisuals.HideStartUi(session.Controller);
        scrPlayer primary = CountdownWorld.GetPrimaryPlayer(session.Controller);
        if(!CountdownWorld.HasChosenPlanet(primary))
            throw new InvalidOperationException("The primary player is unavailable.");
        int safetyLimit = CountdownWorld.AutomaticTileSafetyLimit;
        while(safetyLimit-- > 0 && CountdownWorld.IsNextTileAutomatic(primary)) {
            double automaticHitTime = CalculatePerfectSongPosition(primary);
            CountdownWorld.SeekLoadedWorld(automaticHitTime, automaticHitTime);
            CountdownWorld.AdvanceAutomaticTiles();
        }
        if(safetyLimit <= 0)
            throw new InvalidOperationException("Automatic tile preparation exceeded the floor count.");
        if(!CountdownWorld.HasFollowingTile(primary)) {
            CountdownWorld.Log("the selected start has no following manual tile; continuing without a freeze");
            session.FrozenSongPosition = audio.CurrentSongPosition;
            return false;
        }
        session.FrozenSongPosition = CalculatePerfectSongPosition(primary);
        session.FrozenAudioSongPosition = FrozenStartTiming.CalculateCalibratedAudioSongPosition(
            session.FrozenSongPosition,
            audio.Calibration,
            audio.Pitch
        );
        CountdownWorld.SeekLoadedWorld(session.FrozenSongPosition, session.FrozenAudioSongPosition);
        visuals.ScrubToTime(session.FrozenSongPosition);
        audio.PrimeSongSources();
        audio.PauseListener();
        CountdownVisuals.HideStartUi(session.Controller);
        session.FrozenFrame = CountdownWorld.CurrentFrame;
        session.Phase = FrozenStartPhase.Frozen;
        MetronomePlayback? playback = metronome.Start();
        visuals.StartPreLandingMotion(playback);
        CountdownWorld.Log(
            $"frozen start at tile {CountdownWorld.GetCurrentFloorId(primary)}, "
                + $"song {session.FrozenSongPosition:F6}, audio {session.FrozenAudioSongPosition:F6}");
        return true;
    }
    private double CalculatePerfectSongPosition(scrPlayer player) =>
        FrozenStartTiming.CalculatePerfectSongPosition(
            CountdownWorld.GetPerfectTimingInput(player, audio.Crotchet));
}
internal sealed class CountdownRestorer(CountdownAudio audio, CountdownVisuals visuals) {
    internal void ReleaseAudioForInput(CountdownSession session) {
        if(session.AudioReleasedForInput || session.AudioSnapshot.ListenerPaused || !audio.IsAvailable) return;
        double elapsed = audio.GetInputElapsedSeconds(session.PendingInputTick);
        if(audio.RebaseAtFrozenTime(session.FrozenSongPosition, elapsed)) CountdownHitSounds.RebuildFromCheckpoint();
        audio.AdvanceAndReleasePrimedSources(elapsed);
        session.AudioReleasedForInput = true;
        CountdownWorld.Log(
            $"released primed audio on first input: inputElapsedMs={elapsed * 1000.0:F3}, "
                + $"resumedSong={session.FrozenSongPosition + elapsed * audio.Pitch:F6}");
    }
    internal void RefreezeAfterRejectedInput(CountdownSession session) {
        if(!session.AudioReleasedForInput) return;
        audio.RefreezePrimedSources();
        session.AudioReleasedForInput = false;
    }
    internal void Restore(CountdownSession session, bool restartAudio) {
        bool unpausePrimedSources = false;
        bool logSongSources = false;
        try {
            visuals.RestoreAll();
            if(session.HasAudioSnapshot && audio.IsAvailable && restartAudio && !session.AudioReleasedForInput) {
                if(audio.RebaseAtFrozenTime(session.FrozenSongPosition))
                    CountdownHitSounds.RebuildFromCheckpoint();
                unpausePrimedSources = !session.AudioSnapshot.ListenerPaused;
            }
            logSongSources = session.HasAudioSnapshot
                && audio.IsAvailable
                && restartAudio
                && !session.AudioSnapshot.ListenerPaused;
        } catch(Exception e) {
            CountdownWorld.Warn(e, "Restore");
        } finally {
            if(session.HasAudioSnapshot)
                audio.Restore(session.AudioSnapshot, unpausePrimedSources, logSongSources);
        }
    }
}
