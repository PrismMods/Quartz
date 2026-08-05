using System;
namespace Quartz.Features.Countdown;
internal sealed class CountdownCoordinator {
    private readonly CountdownAudio audio = new();
    private readonly CountdownMetronome metronome = new();
    private readonly CountdownVisuals visuals = new();
    private readonly CountdownPreparer preparer;
    private readonly CountdownRestorer restorer;
    private CountdownSession session;
    private bool restartingWithNativeCountdown;
    internal CountdownCoordinator() {
        preparer = new CountdownPreparer(audio, metronome, visuals);
        restorer = new CountdownRestorer(audio, visuals);
    }
    internal bool IsFrozen => session?.Phase == FrozenStartPhase.Frozen;
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
        CountdownWorld.Log(CountdownWorld.DescribeInput(player));
        return true;
    }
    internal void OnManualHitStarting(scrPlayer player, bool isAuto) {
        if(!IsFrozen || isAuto || player == null) return;
        visuals.RestorePlayer(player);
        if(session.PendingInputPlayer == player) restorer.ReleaseAudioForInput(session);
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
        if(!IsFrozen) return;
        metronome.UpdateDisplay();
        if(metronome.ConsumeDisableRequest()) {
            RestartWithNativeCountdown();
            return;
        }
        visuals.UpdatePreLandingMotion();
    }
    internal void OnPauseRequested(scrController controller) {
        if(IsFrozen && controller == session.Controller && CountdownWorld.IsPauseRequest(controller))
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
        ResetSession();
    }
    private void RestoreAndReset(string reason) {
        metronome.Stop(reason);
        if(session?.Phase is FrozenStartPhase.Frozen or FrozenStartPhase.Preparing or FrozenStartPhase.Releasing) {
            restorer.Restore(
                session,
                restartAudio: session.Phase is FrozenStartPhase.Frozen or FrozenStartPhase.Preparing
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
