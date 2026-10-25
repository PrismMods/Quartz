using HarmonyLib;
using SkyHook;
using UnityEngine;
namespace Quartz.Features.AntiChatter;
public static partial class AntiChatter {
    // The hold bridge needs the multi-planet player; older games skip every patch. Declared first: the fields below read it.
    internal static readonly bool Supported = Quartz.Compat.Game.Refl.Type("scrPlayer") != null;
    private static readonly AccessTools.FieldRef<scrController, int> FrameOnAsyncInputDetected =
        Supported ? AccessTools.FieldRefAccess<scrController, int>("frameOnAsyncInputDetected") : null;
    private static readonly Action<scrController, long> ProcessKeyInputs =
        Supported && AccessTools.Method(typeof(scrController), "ProcessKeyInputs", [typeof(long)]) is { } method
            ? (Action<scrController, long>)Delegate.CreateDelegate(typeof(Action<scrController, long>), method)
            : null;
    private static bool IsAnyAsyncKeyboardActive() =>
        RDInput.asyncKeyboard is { isActive: true } || RDInput.asyncKeyboardLeft is { isActive: true }
        || RDInput.asyncKeyboardRight is { isActive: true };
    private static void ApplyEventsToAsyncMasks(List<SkyHookEvent> events) {
        foreach(SkyHookEvent ev in events) {
            AsyncKeyCode item = new(ev.Key, ev.Label);
            if(ev.Type == SkyHook.EventType.KeyPressed) {
                if(AsyncInputManager.keyMask.Contains(item)) continue;
                AsyncInputManager.keyMask.Add(item);
                AsyncInputManager.keyDownMask.Add(item);
                AsyncInputManager.frameDependentKeyMask.Add(item);
                AsyncInputManager.frameDependentKeyDownMask.Add(item);
            } else if(ev.Type == SkyHook.EventType.KeyReleased) {
                AsyncInputManager.keyMask.Remove(item);
                AsyncInputManager.keyUpMask.Add(item);
                AsyncInputManager.frameDependentKeyMask.Remove(item);
                AsyncInputManager.frameDependentKeyUpMask.Add(item);
            }
        }
    }
    // A bridged hold whose bounce outlived the threshold is a real release: judge it at the tick it expired.
    private static void FlushExpiredPendingReleasesUpTo(scrController controller, long upToTick) {
        if(ProcessKeyInputs == null) return;
        int safety = 0;
        while(safety++ < 8 && TryGetEarliestExpiredPendingReleaseTick(upToTick, out long expireTick)) {
            AsyncInputManager.keyDownMask.Clear();
            AsyncInputManager.keyUpMask.Clear();
            ProcessKeyInputs(controller, expireTick);
            RestoreReconnectedAsyncHoldKeys();
        }
    }
    // Feeds the game the first step that survives the filter; the rest are judged one by one in the postfix.
    [HarmonyPatch(typeof(scrController), nameof(scrController.UpdateInput))]
    private static class UpdateInputPatch {
        private static bool Prepare() => Supported;
        private static void Prefix(scrController __instance, out (bool AnyPressSeen, List<List<SkyHookEvent>> Remaining) __state) {
            __state = (false, null);
            if(!Enabled) {
                ReconnectedHoldKeysThisFrame.Clear();
                return;
            }
            if(!IsAnyAsyncKeyboardActive()) return;
            if(!Application.isFocused) {
                AsyncInputManager.ClearKeys();
                return;
            }
            if(!ExtractSortedSimulationSteps(out List<List<SkyHookEvent>> steps) || steps == null || steps.Count == 0) return;
            bool anyPressSeen = false;
            int firstKept = -1;
            for(int i = 0; i < steps.Count; i++) {
                FlushExpiredPendingReleasesUpTo(__instance, steps[i][0].GetTimeInTicks());
                FilterAsyncSimulationStep(steps[i], out List<SkyHookEvent> kept, out bool sawPress);
                anyPressSeen |= sawPress;
                if(kept.Count == 0) continue;
                foreach(SkyHookEvent ev in kept) AsyncInputManager.keyQueue.Enqueue(ev);
                firstKept = i;
                break;
            }
            List<List<SkyHookEvent>> remaining = firstKept >= 0 && firstKept + 1 < steps.Count
                ? steps.GetRange(firstKept + 1, steps.Count - firstKept - 1)
                : null;
            if(anyPressSeen && AsyncInputManager.isActive) FrameOnAsyncInputDetected(__instance) = Time.frameCount;
            __state = (anyPressSeen, remaining);
        }
        private static void Postfix(scrController __instance, (bool AnyPressSeen, List<List<SkyHookEvent>> Remaining) __state) {
            if(!Enabled || !AsyncInputManager.isActive || !IsAnyAsyncKeyboardActive()) return;
            bool anyPressSeen = __state.AnyPressSeen;
            RestoreReconnectedAsyncHoldKeys();
            if(__state.Remaining != null && ProcessKeyInputs != null) {
                foreach(List<SkyHookEvent> step in __state.Remaining) {
                    long stepTick = step[0].GetTimeInTicks();
                    FlushExpiredPendingReleasesUpTo(__instance, stepTick);
                    FilterAsyncSimulationStep(step, out List<SkyHookEvent> kept, out bool sawPress);
                    anyPressSeen |= sawPress;
                    if(kept.Count > 0) {
                        AsyncInputManager.keyDownMask.Clear();
                        AsyncInputManager.keyUpMask.Clear();
                        ApplyEventsToAsyncMasks(kept);
                        ProcessKeyInputs(__instance, stepTick);
                    }
                    RestoreReconnectedAsyncHoldKeys();
                }
            }
            // An early hold release already past the threshold by the end of this frame is judged now, not next frame.
            FlushExpiredPendingReleasesUpTo(__instance, AsyncInputManager.currFrameTick);
            if(anyPressSeen) FrameOnAsyncInputDetected(__instance) = Time.frameCount;
        }
    }
    [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Simulated_PlayerControl_Update))]
    private static class SimulatedPlayerControlUpdatePatch {
        private static bool Prepare() => Supported;
        private static void Prefix(long? targetTick) =>
            CurrentAsyncSimulationTick = targetTick ?? (AsyncInputManager.isActive ? AsyncInputManager.currFrameTick : 0L);
    }
    [HarmonyPatch(typeof(AsyncInputManager), nameof(AsyncInputManager.ClearKeys))]
    private static class ClearKeysPatch {
        private static bool Prepare() => Supported;
        private static void Postfix() => ClearAsyncState();
    }
    [HarmonyPatch(typeof(RDInputType_Keyboard), "CheckKeyState")]
    private static class CheckKeyStatePatch {
        private static bool Prepare() => Supported;
        private static void Postfix(KeyCode key, ButtonState state, ref bool __result) =>
            FilterSyncKeyState(key, state, ref __result);
    }
    // A hold key that bounced open inside the threshold is put back, so the hold doesn't end early.
    [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.ValidInputWasReleased))]
    private static class ValidInputWasReleasedPatch {
        private static bool Prepare() => Supported;
        private static void Prefix(scrPlayer __instance, out (List<AnyKeyCode> Keys, bool InProtectedBody)? __state) {
            __state = null;
            CleanupStaleEarlyReleases(__instance);
            if(!HoldBounceProtection) return;
            if(AsyncInputManager.isActive) RestoreReconnectedAsyncHoldKeys();
            if(__instance.holdKeys == null || __instance.holdKeys.Count == 0) return;
            bool inProtectedBody = IsPlayerInProtectedHoldBody(__instance);
            if(!inProtectedBody && !HasPendingEarlyHoldRelease(__instance)) return;
            __state = (new List<AnyKeyCode>(__instance.holdKeys), inProtectedBody);
        }
        private static void Postfix(scrPlayer __instance, (List<AnyKeyCode> Keys, bool InProtectedBody)? __state,
            ref bool __result) {
            if(!__state.HasValue || __instance.holdKeys == null || !__result) return;
            (List<AnyKeyCode> keys, bool inProtectedBody) = __state.Value;
            if(keys.Count == 0) return;
            long now = GetStopwatchTicks();
            bool anyGenuinelyReleased = false;
            foreach(AnyKeyCode key in keys) {
                if(key.value == null || __instance.holdKeys.Contains(key)) continue;
                if(HandleHoldKeyReleaseCheck(__instance, key, inProtectedBody, now)) __instance.holdKeys.Add(key);
                else anyGenuinelyReleased = true;
            }
            __result = anyGenuinelyReleased;
        }
    }
    // Once a bridged release is confirmed, the hold is judged from where it was really let go.
    [HarmonyPatch(typeof(scrPlayer), "UpdateHoldBehavior")]
    private static class UpdateHoldBehaviorPatch {
        private static bool Prepare() => Supported;
        private static void Prefix(scrPlayer __instance, out (scrFloor Floor, float OriginalCompletion)? __state) {
            __state = null;
            if(!TryConsumeConfirmedEarlyRelease(__instance, out float releaseCompletion) || __instance.currFloor == null) return;
            scrFloor floor = __instance.currFloor;
            if(floor.holdCompletion <= releaseCompletion) return;
            __state = (floor, floor.holdCompletion);
            floor.holdCompletion = releaseCompletion;
        }
        private static void Postfix((scrFloor Floor, float OriginalCompletion)? __state) {
            if(!__state.HasValue) return;
            (scrFloor floor, float original) = __state.Value;
            if(floor != null && floor.holdCompletion > 0f && floor.holdCompletion < original) floor.holdCompletion = original;
        }
    }
}
