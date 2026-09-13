using System.Diagnostics;
using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
using SkyHook;
using UnityEngine;
using System.Reflection;
using Quartz.Compat.Game;
using Quartz.Utility;
namespace Quartz.Features.ChatterBlocker;
public static class ChatterBlocker {
    public static SettingsFile<ChatterBlockerSettings> ConfMgr { get; private set; }
    public static ChatterBlockerSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<ChatterBlockerSettings>.Loaded("ChatterBlocker.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool IsActive() {
        EnsureConf();
        return MainCore.IsModEnabled && Conf.Enabled;
    }
    private static bool HasAnyFilter() =>
        IsActive() || KeyLimiter.KeyLimiter.IsEnabled() || KeyLimiter.KeyLimiter.IsMenuBlockEnabled()
        || Quartz.Game.InjectedKeys.GuardActive;
    private static long ThresholdMs() => Math.Max(0L, (long)Math.Round(Conf?.ThresholdMs ?? 0f));
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static long NowMs() => clock.ElapsedMilliseconds;
    private static readonly Dictionary<KeyCode, long> lastKeyPress = [];
    private static readonly Dictionary<ushort, long> lastAsyncKeyPress = [];
    private static readonly HashSet<KeyCode> reportedKeysThisFrame = [];
    // RDInput merges every active backend. Unity and the async hook can therefore
    // describe the same physical edge in one list; gameplay must see it once.
    private static readonly HashSet<KeyCode> physicalKeysThisEvaluation = [];
    private static readonly HashSet<ushort> unknownAsyncKeysThisEvaluation = [];
    private static int reportedKeysFrame = -1;
    private enum InputSource : byte { Unity, Async }
    private readonly struct PhysicalReport(long at, InputSource source) {
        public readonly long At = at;
        public readonly InputSource Source = source;
    }
    private static readonly Dictionary<KeyCode, PhysicalReport> lastPhysicalReport = [];
    // A backend can queue its copy one frame later. This is separate from the
    // user-configured chatter threshold and remains active with only Key Limiter.
    private const long CrossSourceDuplicateGraceMs = 8L;
    private static readonly HashSet<KeyCode> injectedKeyHeldPrev = [];
    // Presses we counted ourselves because the game had not reported them yet. The
    // hook bit is set on the hook thread before the game queues the same async event,
    // so the game can still report that press a frame or two later; it must not count
    // a second time.
    private static readonly Dictionary<KeyCode, long> injectedAwaitingGame = [];
    private const long InjectedReportGraceMs = 100L;
    private static readonly bool DebugLog = false;
    private static bool AcceptNormalKey(KeyCode key, long now, long thresholdMs, bool active) {
        if(!active) return true;
        if(!lastKeyPress.TryGetValue(key, out long last)) {
            lastKeyPress[key] = now;
            return true;
        }
        long elapsed = now - last;
        if(thresholdMs <= 0L || elapsed > thresholdMs) {
            lastKeyPress[key] = now;
            return true;
        }
        if(DebugLog) MainCore.Log.Msg($"[ChatterBlocker] Blocked Key: {key} time: {elapsed}ms.");
        return false;
    }
    private static bool AcceptAsyncKey(ushort key, long now, long thresholdMs, bool active) {
        if(!active) return true;
        if(!lastAsyncKeyPress.TryGetValue(key, out long last)) {
            lastAsyncKeyPress[key] = now;
            return true;
        }
        long elapsed = now - last;
        if(thresholdMs <= 0L || elapsed > thresholdMs) {
            lastAsyncKeyPress[key] = now;
            return true;
        }
        if(DebugLog) MainCore.Log.Msg($"[ChatterBlocker] Blocked Async Key: {key} time: {elapsed}ms.");
        return false;
    }
    private static void RecordKeyStats(scrController controller, object key) {
        try {
            GameApi.RecordKeyPress(controller, key);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static int overCounterResetFrame = -1;
    private static void ResetKeyLimiterOverCounter(scrController controller) {
        int frame = UnityEngine.Time.frameCount;
        if(overCounterResetFrame == frame) return;
        overCounterResetFrame = frame;
        GameApi.ResetKeyLimiterOverCounter(controller);
    }
    private static int CountValidKeysPressed() {
        // The game calls this once as a boolean probe and again to get the count.
        // Replaying the filters makes their stateful answer differ on call two.
        if(inPlayerBatch && evaluatedBatch == injectionBatch) return evaluatedBatchCount;
        scrController controller = scrController.instance;
        if(controller == null) return CacheEvaluation(0);
        ResetKeyLimiterOverCounter(controller);
        if(KeyLimiter.KeyLimiter.IsMenuBlockActive()) return CacheEvaluation(0);
        bool chatterActive = IsActive();
        long now = NowMs();
        long threshold = ThresholdMs();
        int count = 0;
        physicalKeysThisEvaluation.Clear();
        unknownAsyncKeysThisEvaluation.Clear();
        int frame = UnityEngine.Time.frameCount;
        if(reportedKeysFrame != frame) {
            reportedKeysFrame = frame;
            reportedKeysThisFrame.Clear();
        }
        foreach(AnyKeyCode mainPressKey in RDInput.GetMainPressKeys()) {
            object value = mainPressKey.value;
            if(value is KeyCode key) {
                KeyCode normalized = KeyCodes.Normalize(key);
                reportedKeysThisFrame.Add(normalized);
                if(normalized != KeyCode.None && !physicalKeysThisEvaluation.Add(normalized)) continue;
                if(Quartz.Game.InjectedKeys.Is(normalized)) continue;
                if(ConsumeLateReportOfInjected(normalized, now)) continue;
                if(KeyLimiter.KeyLimiter.ShouldBlockKey(key)) continue;
                if(IsCrossSourceDuplicate(normalized, InputSource.Unity, now)) continue;
                RecordKeyStats(controller, key);
                if(AcceptNormalKey(normalized, now, threshold, chatterActive)) count++;
            } else if(value is AsyncKeyCode asyncKey) {
                KeyCode physical = KeyCodes.Normalize(
                    KeyLimiter.KeyLimiter.HookKeyToPhysicalUnityKey(asyncKey.key, asyncKey.label));
                if(physical != KeyCode.None) reportedKeysThisFrame.Add(physical);
                if(physical != KeyCode.None) {
                    if(!physicalKeysThisEvaluation.Add(physical)) continue;
                } else if(!unknownAsyncKeysThisEvaluation.Add(asyncKey.key)) {
                    continue;
                }
                if(Quartz.Game.InjectedKeys.Is(physical)) continue;
                if(ConsumeLateReportOfInjected(physical, now)) continue;
                if(KeyLimiter.KeyLimiter.ShouldBlockAsyncKeyFromHook(asyncKey.key, asyncKey.label)) continue;
                if(IsCrossSourceDuplicate(physical, InputSource.Async, now)) continue;
                RecordKeyStats(controller, asyncKey);
                if(physical != KeyCode.None) {
                    if(AcceptNormalKey(physical, now, threshold, chatterActive)) count++;
                } else if(AcceptAsyncKey(asyncKey.key, now, threshold, chatterActive)) {
                    count++;
                }
            }
        }
        count += CountKeysMissedByGame(controller, now, threshold, chatterActive);
        return CacheEvaluation(count);
    }
    private static bool IsCrossSourceDuplicate(KeyCode key, InputSource source, long now) {
        if(key == KeyCode.None) return false;
        bool duplicate = lastPhysicalReport.TryGetValue(key, out PhysicalReport last)
            && last.Source != source && now - last.At <= CrossSourceDuplicateGraceMs;
        if(duplicate) return true;
        lastPhysicalReport[key] = new PhysicalReport(now, source);
        return false;
    }
    private static bool ConsumeLateReportOfInjected(KeyCode key, long now) {
        if(key == KeyCode.None || !injectedAwaitingGame.TryGetValue(key, out long injectedAt)) return false;
        injectedAwaitingGame.Remove(key);
        return now - injectedAt <= InjectedReportGraceMs;
    }
    private static int injectionBatch;
    private static bool inPlayerBatch;
    private static int evaluatedBatch = -1;
    private static int evaluatedBatchCount;
    private static int injectedComputeFrame = -1;
    private static int injectedBatch = -1;
    private static int injectedCount;
    private static readonly KeyCode[] hookFallbackKeys = [
        KeyCode.LeftShift, KeyCode.RightShift,
        KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.LeftAlt, KeyCode.RightAlt,
    ];
    public static void NotePlayerBatch(bool entered) {
        inPlayerBatch = entered;
        if(entered) injectionBatch++;
    }
    private static int CacheEvaluation(int count) {
        if(inPlayerBatch) {
            evaluatedBatch = injectionBatch;
            evaluatedBatchCount = count;
        }
        return count;
    }
    private static int CountKeysMissedByGame(scrController controller, long now, long threshold, bool chatterActive) {
        bool limiterActive = KeyLimiter.KeyLimiter.IsActive();
        if((!limiterActive && !chatterActive) || !KeyLimiter.KeyLimiter.InPlayerControl()) {
            injectedKeyHeldPrev.Clear();
            injectedAwaitingGame.Clear();
            injectedComputeFrame = -1;
            return 0;
        }
        if(!inPlayerBatch) return 0;
        int frame = UnityEngine.Time.frameCount;
        if(injectedComputeFrame != frame) {
            injectedComputeFrame = frame;
            injectedBatch = injectionBatch;
            injectedCount = ComputeInjectedKeys(controller, now, threshold, chatterActive, limiterActive);
        }
        return injectionBatch == injectedBatch ? injectedCount : 0;
    }
    private static int ComputeInjectedKeys(scrController controller, long now, long threshold, bool chatterActive,
        bool limiterActive) {
        bool asyncActive = AsyncKeyboardActive();
        int injected = 0;
        if(limiterActive) {
            int[] allowed = KeyLimiter.KeyLimiter.Conf?.AllowedKeys;
            if(allowed == null || allowed.Length == 0) {
                injectedKeyHeldPrev.Clear();
                return 0;
            }
            for(int i = 0; i < allowed.Length; i++) {
                KeyCode key = KeyCodes.Normalize((KeyCode)allowed[i]);
                injected += TryInjectMissedKey(controller, key, now, threshold, chatterActive, asyncActive);
            }
            return injected;
        }
        for(int i = 0; i < hookFallbackKeys.Length; i++) {
            KeyCode key = hookFallbackKeys[i];
            if(!Quartz.Game.HookInput.IsHookTrackedKey(key)) continue;
            injected += TryInjectMissedKey(controller, key, now, threshold, chatterActive, asyncActive);
        }
        return injected;
    }
    private static int TryInjectMissedKey(scrController controller, KeyCode key, long now, long threshold,
        bool chatterActive, bool asyncActive) {
        if(key == KeyCode.None || KeyLimiter.KeyLimiter.IsMouseKey(key)) return 0;
        if(reportedKeysThisFrame.Contains(key)) {
            injectedKeyHeldPrev.Add(key);
            return 0;
        }
        if(asyncActive && KeyLimiter.KeyLimiter.HookEverSaw(key)
            && !Quartz.Game.HookInput.IsHookTrackedKey(key)) {
            injectedKeyHeldPrev.Remove(key);
            return 0;
        }
        bool held;
        try { held = UnityEngine.Input.GetKey(key); }
        catch(Exception e) { Diag.Ignore(e); return 0; }
        if(!held) held = KeyLimiter.KeyLimiter.HookKeyHeld(key);
        if(held && !injectedKeyHeldPrev.Contains(key)) {
            RecordKeyStats(controller, key);
            if(AcceptNormalKey(key, now, threshold, chatterActive)) {
                injectedKeyHeldPrev.Add(key);
                injectedAwaitingGame[key] = now;
                return 1;
            }
        }
        if(held) injectedKeyHeldPrev.Add(key);
        else injectedKeyHeldPrev.Remove(key);
        return 0;
    }
    private static readonly List<KeyCode> injectedReleaseScratch = [];
    public static void SampleInjectedKeyReleases() {
        if(injectedKeyHeldPrev.Count == 0) return;
        injectedReleaseScratch.Clear();
        foreach(KeyCode key in injectedKeyHeldPrev) injectedReleaseScratch.Add(key);
        for(int i = 0; i < injectedReleaseScratch.Count; i++) {
            KeyCode key = injectedReleaseScratch[i];
            bool held;
            try { held = UnityEngine.Input.GetKey(key); }
            catch(Exception e) { Diag.Ignore(e); held = false; }
            if(!held) held = KeyLimiter.KeyLimiter.HookKeyHeld(key);
            if(!held) injectedKeyHeldPrev.Remove(key);
        }
    }
    private static bool AsyncKeyboardActive() {
        try {
            return GameApi.AsyncKeyboardActive();
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    [HarmonyPatch]
    private static class SimulatedPlayerControlUpdatePatch {
        private static MethodBase TargetMethod() => GameApi.PlayerControlUpdateTarget;
        private static void Prefix() => NotePlayerBatch(true);
        private static void Postfix() => NotePlayerBatch(false);
    }
    [HarmonyPatch]
    private static class CountValidKeysPressedPatch {
        private static MethodBase TargetMethod() => GameApi.CountValidKeysPressedTarget;
        private static bool Prefix(ref int __result) {
            if(!HasAnyFilter()) return true;
            __result = CountValidKeysPressed();
            return false;
        }
    }
}
