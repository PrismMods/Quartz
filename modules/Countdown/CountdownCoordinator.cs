using System;
namespace Quartz.Features.Countdown;
internal sealed class CountdownCoordinator {
    private const int MinimumWarmupFrames = 2;
    private const int MaximumWarmupFrames = 5;
    private const double WarmupFrameDurationTolerance = 0.25;
    private readonly CountdownAudio audio = new();
    private readonly CountdownHitSounds hitSounds = new();
    private readonly CountdownMetronome metronome = new();
    private readonly CountdownVisuals visuals = new();
    private readonly CountdownPreparer preparer;
    private readonly CountdownRestorer restorer;
    private CountdownSession session;
    private bool restartingWithNativeCountdown;
    internal CountdownCoordinator() {
        preparer = new CountdownPreparer(audio, hitSounds, visuals);
        restorer = new CountdownRestorer(audio, hitSounds, visuals);
    }
    internal bool IsFrozen => session?.Phase == FrozenStartPhase.Frozen;
    private bool IsWarming => session?.Phase == FrozenStartPhase.Warming;
    internal void OnStartRewind(scrController controller, int requestedFloor) {
        RestoreAndReset("restart");
        if(!metronome.IsEnabledForSession) return;
        int startFloor = CountdownWorld.ResolveStartFloor(requestedFloor);
        if(CountdownWorld.CanArm(controller, startFloor)) session = new CountdownSession(controller);
    }
    internal bool PrepareInitialScrub(int floorNumber) {
        if(session?.Phase != FrozenStartPhase.WaitingForScrub
            || !CountdownWorld.CanPrepareInitialScrub(session.Controller, floorNumber))
            return false;
        session.Phase = FrozenStartPhase.WaitingForSchedule;
        return true;
    }
    internal void OnMusicScheduled(scrController controller) {
        if(session?.Phase != FrozenStartPhase.WaitingForSchedule
            || controller != session.Controller
            || !CountdownWorld.CanHandleMusicScheduled(controller))
            return;
        try {
            if(preparer.Prepare(session)) return;
            restorer.Restore(session, restartAudio: true);
            ResetSession();
        } catch(Exception e) {
            CountdownWorld.Warn(e, "Prepare");
            RestoreAndReset("preparation failed");
        }
    }
    internal bool PreparePlayerUpdate(scrPlayer player, ref ulong? targetTick) {
        if(IsWarming) return false;
        if(!IsFrozen) return true;
        if(CountdownWorld.IsAutoplayOn) {
            RestoreAndReset("autoplay enabled");
            return true;
        }
        if(!CountdownWorld.IsRuntimeValid(session.Controller)) {
            RestoreAndReset("run became invalid");
            return true;
        }
        if(CountdownWorld.CurrentFrame <= session.FrozenFrame || player == null) return false;
        if(metronome.IsUiConsumingInput) return false;
        if(CountdownWorld.UnlockInputIfNeeded(player))
            CountdownWorld.Log("released the inherited player input lock after the launch frame");
        if(!CountdownWorld.ValidInputWasTriggered(player)) return false;
        metronome.Stop("first input accepted");
        session.PendingInputTick = targetTick;
        targetTick = null;
        session.PendingInputPlayer = player;
        restorer.ReleaseAudioForInput(session);
        CountdownWorld.Log(CountdownWorld.DescribeInput(player));
        return true;
    }
    internal void OnManualHitStarting(scrPlayer player, bool isAuto) {
        if(!IsFrozen || isAuto || player == null) return;
        visuals.RestorePlayer(player);
        if(session.PendingInputPlayer == player) restorer.RebaseTimelineForInput(session);
    }
    internal void CompletePlayerUpdate(scrPlayer player) {
        if(!IsFrozen || player == null || session.PendingInputPlayer != player) return;
        if(!CountdownWorld.CanRetryHit(player)) {
            session.ClearPendingInput();
            return;
        }
        CountdownWorld.Log("the original input update did not land; retrying the same input through Hit(false)");
        if(!CountdownWorld.Hit(player))
            CountdownWorld.Log("the fallback Hit(false) was rejected; keeping the frozen start active");
    }
    internal void OnManualHitCompleted(scrPlayer player, bool isAuto, bool moved) {
        if(!IsFrozen || isAuto || player == null) return;
        if(!moved) {
            restorer.RefreezeAfterRejectedInput(session);
            visuals.StartPreLandingMotion(metronome.Start());
            return;
        }
        CountdownWorld.Log("the first input landed naturally at the frozen Pure Perfect angle");
        session.ClearPendingInput();
        ReleaseFrozenStart();
    }
    internal void PumpAsyncInput() {
        if(!IsFrozen) return;
        if(!CountdownWorld.IsRuntimeValid(session.Controller)) {
            RestoreAndReset("scene or editor state changed");
            metronome.ResetSessionState();
            return;
        }
        if(CountdownWorld.IsAsyncInputActive) CountdownWorld.UpdateInput(session.Controller);
    }
    internal void PumpFrozenVisuals() {
        if(IsWarming) {
            PumpWarmup();
            return;
        }
        if(!IsFrozen) return;
        metronome.UpdateDisplay();
        if(metronome.ConsumeDisableRequest()) {
            RestartWithNativeCountdown();
            return;
        }
        visuals.UpdatePreLandingMotion();
    }
    internal void OnPauseRequested(scrController controller) {
        if((IsFrozen || IsWarming) && controller == session.Controller && CountdownWorld.IsPauseRequest(controller))
            RestoreAndReset("pause requested");
    }
    internal void OnEditorPlayModeExited() {
        RestoreAndReset("editor play mode exited");
        if(restartingWithNativeCountdown) {
            restartingWithNativeCountdown = false;
            return;
        }
        metronome.ResetSessionState();
    }
    internal void Shutdown() {
        RestoreAndReset("module shutdown");
        metronome.ResetSessionState();
    }
    private void PumpWarmup() {
        if(!CountdownWorld.IsRuntimeValid(session.Controller)) {
            RestoreAndReset("run became invalid during visual warmup");
            return;
        }
        if(CountdownWorld.CurrentFrame <= session.WarmupStartedFrame) return;
        (int tweens, double frameDuration) = visuals.CaptureWarmupSample();
        bool tweenCountStable = session.WarmupTweenCount < 0 || tweens == session.WarmupTweenCount;
        bool frameDurationStable = DurationsAreStable(session.WarmupFrameDurationSeconds, frameDuration);
        session.WarmupStableFrames = tweenCountStable && frameDurationStable ? session.WarmupStableFrames + 1 : 1;
        session.WarmupTweenCount = tweens;
        session.WarmupFrameDurationSeconds = frameDuration;
        session.WarmupRenderedFrames++;
        session.WarmupStartedFrame = CountdownWorld.CurrentFrame;
        bool stable = session.WarmupRenderedFrames >= MinimumWarmupFrames
            && session.WarmupStableFrames >= MinimumWarmupFrames;
        if(!stable && session.WarmupRenderedFrames < MaximumWarmupFrames) return;
        session.FrozenFrame = CountdownWorld.CurrentFrame;
        session.Phase = FrozenStartPhase.Frozen;
        visuals.StartPreLandingMotion(metronome.Start());
        CountdownWorld.Log(
            $"completed frozen visual warmup: frames={session.WarmupRenderedFrames}, "
                + $"stableFrames={session.WarmupStableFrames}, tweens={session.WarmupTweenCount}");
    }
    private static bool DurationsAreStable(double previous, double current) {
        if(previous <= 0.0 || current <= 0.0) return previous <= 0.0 && current <= 0.0;
        double larger = Math.Max(previous, current);
        double smaller = Math.Min(previous, current);
        return (larger - smaller) / smaller <= WarmupFrameDurationTolerance;
    }
    private void RestartWithNativeCountdown() {
        CountdownWorld.Log("metronome turned off; restarting the playtest with the game's countdown");
        RestoreAndReset("metronome disabled");
        scnEditor editor = ADOBase.editor;
        if(editor == null) return;
        restartingWithNativeCountdown = true;
        try {
            editor.SwitchToEditMode();
            editor.Play();
        } catch(Exception e) {
            restartingWithNativeCountdown = false;
            CountdownWorld.Warn(e, "RestartNative");
            metronome.ResetSessionState();
        }
    }
    private void ReleaseFrozenStart() {
        if(!IsFrozen) return;
        session.Phase = FrozenStartPhase.Releasing;
        restorer.Restore(session, restartAudio: true);
        CountdownAudio.RebaseAsyncInputClock();
        hitSounds.Reset(keepInstalledSchedule: true);
        ResetSession();
    }
    private void RestoreAndReset(string reason) {
        metronome.Stop(reason);
        if(session?.Phase
            is FrozenStartPhase.Warming
                or FrozenStartPhase.Frozen
                or FrozenStartPhase.Preparing
                or FrozenStartPhase.Releasing) {
            restorer.Restore(
                session,
                restartAudio: session.Phase
                    is FrozenStartPhase.Warming or FrozenStartPhase.Frozen or FrozenStartPhase.Preparing
            );
            CountdownWorld.Log($"cleared frozen start state: {reason}");
        }
        ResetSession();
    }
    private void ResetSession() {
        metronome.Stop();
        session = null;
    }
}
