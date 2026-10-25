using SkyHook;
using UnityEngine;
namespace Quartz.Features.AntiChatter;
public static partial class AntiChatter {
    // Sorts the queued hook events by time and splits them into simulation steps: different keys at the same tick
    // share a step, a key's repeated change or a new tick starts the next one, so the game judges between every event
    // even inside one frame.
    public static bool ExtractSortedSimulationSteps(out List<List<SkyHookEvent>> steps) {
        steps = null;
        ReconnectedHoldKeysThisFrame.Clear();
        if(!AsyncInputManager.isActive || AsyncInputManager.keyQueue == null || AsyncInputManager.keyQueue.IsEmpty)
            return false;
        List<SkyHookEvent> rawEvents = [];
        while(AsyncInputManager.keyQueue.TryDequeue(out SkyHookEvent dequeued)) rawEvents.Add(dequeued);
        if(rawEvents.Count == 0) return false;
        if(rawEvents.Count > 1) rawEvents.Sort(static (a, b) => a.GetTimeInTicks().CompareTo(b.GetTimeInTicks()));
        steps = [];
        List<SkyHookEvent> currentStep = [];
        HashSet<ushort> keysInCurrentStep = [];
        long currentTick = rawEvents[0].GetTimeInTicks();
        foreach(SkyHookEvent ev in rawEvents) {
            long evTick = ev.GetTimeInTicks();
            if(currentStep.Count > 0 && (evTick != currentTick || keysInCurrentStep.Contains(ev.Key))) {
                steps.Add(currentStep);
                currentStep = [];
                keysInCurrentStep.Clear();
                currentTick = evTick;
            }
            currentStep.Add(ev);
            keysInCurrentStep.Add(ev.Key);
        }
        if(currentStep.Count > 0) steps.Add(currentStep);
        return steps.Count > 0;
    }
    public static void FilterAsyncSimulationStep(List<SkyHookEvent> stepEvents, out List<SkyHookEvent> keptEvents,
        out bool anyKeyPressedSeen) {
        anyKeyPressedSeen = false;
        keptEvents = new List<SkyHookEvent>(stepEvents.Count);
        bool enabled = Enabled;
        long thresholdTicks = GetThresholdTicks();
        bool holdBounce = HoldBounceProtection;
        foreach(SkyHookEvent ev in stepEvents) {
            ushort rawKey = ev.Key;
            long evTick = ev.GetTimeInTicks();
            CurrentAsyncSimulationTick = evTick;
            if(ev.Type == SkyHook.EventType.KeyPressed) {
                anyKeyPressedSeen = true;
                // OS key repeat fires while the key is already down: keep it only if the press counted, and don't
                // move the debounce origin.
                if(AsyncPhysicallyDown.ContainsKey(rawKey)) {
                    if(AsyncLogicallyDown.Contains(rawKey)) keptEvents.Add(ev);
                    continue;
                }
                AsyncKeyCode asyncKey = new(rawKey, ev.Label);
                AsyncPhysicallyDown[rawKey] = asyncKey;
                if(!enabled || thresholdTicks <= 0L) {
                    AcceptAsyncPress(rawKey, evTick);
                    keptEvents.Add(ev);
                    continue;
                }
                // A hold that bounced open and closed inside the threshold: restore the hold, not a new hit.
                if(holdBounce && IsAsyncKeyInProtectedHold(rawKey) && AsyncLastReleaseTick.TryGetValue(rawKey, out long releasedAt)) {
                    long bounce = evTick - releasedAt;
                    if(bounce >= 0L && bounce < thresholdTicks) {
                        AsyncLogicallyDown.Add(rawKey);
                        ClearPendingEarlyReleaseForAsyncKey(rawKey);
                        if(!ReconnectedHoldKeysThisFrame.Contains(asyncKey)) ReconnectedHoldKeysThisFrame.Add(asyncKey);
                        AsyncInputManager.keyMask.Add(asyncKey);
                        AsyncInputManager.frameDependentKeyMask.Add(asyncKey);
                        RecordBlocked(ev.Label.ToString(), bounce);
                        continue;
                    }
                }
                // A re-press inside the threshold of the last valid press is dropped; the origin stays put.
                if(AsyncLastValidPressTick.TryGetValue(rawKey, out long lastValid)) {
                    long delta = evTick - lastValid;
                    if(delta >= 0L && delta < thresholdTicks) {
                        AsyncLogicallyDown.Remove(rawKey);
                        RecordBlocked(ev.Label.ToString(), delta);
                        continue;
                    }
                }
                AcceptAsyncPress(rawKey, evTick);
                keptEvents.Add(ev);
            } else if(ev.Type == SkyHook.EventType.KeyReleased) {
                AsyncPhysicallyDown.Remove(rawKey);
                AsyncLastReleaseTick[rawKey] = evTick;
                ReconnectedHoldKeysThisFrame.RemoveAll(k => k.key == rawKey);
                if(!enabled || thresholdTicks <= 0L) {
                    AsyncLogicallyDown.Remove(rawKey);
                    keptEvents.Add(ev);
                    continue;
                }
                // The release of a dropped press is dropped too, and cleared from the masks so the key can't stick.
                if(!AsyncLogicallyDown.Remove(rawKey)) {
                    AsyncKeyCode released = new(rawKey, ev.Label);
                    AsyncInputManager.keyMask.Remove(released);
                    AsyncInputManager.frameDependentKeyMask.Remove(released);
                    continue;
                }
                keptEvents.Add(ev);
            } else {
                keptEvents.Add(ev);
            }
        }
    }
    private static void AcceptAsyncPress(ushort rawKey, long tick) {
        AsyncLastValidPressTick[rawKey] = tick;
        AsyncLogicallyDown.Add(rawKey);
        ClearPendingEarlyReleaseForAsyncKey(rawKey);
    }
    public static void FilterSyncKeyState(KeyCode key, ButtonState state, ref bool result) {
        if(!Enabled || RDInput.keyboardInput == null || !RDInput.keyboardInput.isActive) return;
        if(ADOBase.controller == null || ADOBase.controller.state != States.PlayerControl) return;
        long thresholdTicks = GetThresholdTicks();
        if(thresholdTicks <= 0L) return;
        int frame = Time.frameCount;
        // The game asks the same key/state several times a frame; the stateful filter must answer once.
        if(SyncFrameCache.TryGetValue((key, state), out (int Frame, bool Result) cached) && cached.Frame == frame) {
            result = cached.Result;
            return;
        }
        long now = GetStopwatchTicks();
        switch(state) {
            case ButtonState.WentDown:
                if(result) result = AcceptSyncPress(key, now, thresholdTicks);
                break;
            case ButtonState.WentUp:
                if(result) {
                    if(SyncLogicallyDown.Remove(key)) SyncLastReleaseTick[key] = now;
                    else result = false;
                }
                break;
            case ButtonState.IsDown:
                if(result && !SyncLogicallyDown.Contains(key) && !Input.GetKeyDown(key)) result = false;
                break;
        }
        SyncFrameCache[(key, state)] = (frame, result);
    }
    private static bool AcceptSyncPress(KeyCode key, long now, long thresholdTicks) {
        if(HoldBounceProtection && IsSyncKeyInProtectedHold(key) && SyncLastReleaseTick.TryGetValue(key, out long releasedAt)) {
            long bounce = now - releasedAt;
            if(bounce >= 0L && bounce < thresholdTicks) {
                SyncLogicallyDown.Add(key);
                SyncLastReleaseTick.Remove(key);
                ClearPendingEarlyReleaseForSyncKey(key);
                RecordBlocked(key.ToString(), bounce);
                return false;
            }
        }
        if(SyncLastValidPressTick.TryGetValue(key, out long lastValid)) {
            long delta = now - lastValid;
            if(delta >= 0L && delta < thresholdTicks) {
                SyncLogicallyDown.Remove(key);
                RecordBlocked(key.ToString(), delta);
                return false;
            }
        }
        SyncLastValidPressTick[key] = now;
        SyncLogicallyDown.Add(key);
        SyncLastReleaseTick.Remove(key);
        ClearPendingEarlyReleaseForSyncKey(key);
        return true;
    }
}
