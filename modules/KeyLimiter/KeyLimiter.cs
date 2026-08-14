using Quartz.Core;
using Quartz.IO;
using MonsterLove.StateMachine;
using SkyHook;
using System.Threading;
using UnityEngine;
using Quartz.Compat.Game;
using Quartz.Game;
using Quartz.UI.Utility;
using Quartz.Utility;
namespace Quartz.Features.KeyLimiter;
public static partial class KeyLimiter {
    public static SettingsFile<KeyLimiterSettings> ConfMgr { get; private set; }
    public static KeyLimiterSettings Conf => ConfMgr?.Data;
    public static event Action Changed;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = SettingsFile<KeyLimiterSettings>.Loaded("KeyLimiter.json");
        EnsureTicker();
    }
    public static void Save() => ConfMgr?.RequestSave();
    public static bool IsEnabled() {
        EnsureConf();
        return MainCore.IsModEnabled && Conf.Enabled;
    }
    public static bool IsActive() => IsEnabled() && !IsCapturing;
    public static bool IsMenuBlockEnabled() => MainCore.IsModEnabled && MainCore.Conf.BlockInputsWhileMenuOpen;
    public static bool IsMenuBlockActive() => Quartz.UI.UICore.IsOpen && IsMenuBlockEnabled() && !Autoplaying;
    private static bool Autoplaying {
        get { try { return RDC.auto; } catch(Exception e) { Diag.Ignore(e); return false; } }
    }
    private static int cachedPlayerControlFrame = -1;
    private static bool cachedPlayerControl;
    private static int cachedPlayerControlForHooks;
    public static bool InPlayerControl() {
        int frame = Time.frameCount;
        if(cachedPlayerControlFrame == frame) return cachedPlayerControl;
        cachedPlayerControlFrame = frame;
        SetCachedPlayerControl(false);
        try {
            scrController controller = scrController.instance;
            if(controller == null) return false;
            if(controller.paused || !controller.gameworld) return false;
            SetCachedPlayerControl(((StateBehaviour)controller).stateMachine.GetState() is States state
                && state == States.PlayerControl);
            return cachedPlayerControl;
        } catch(Exception e) {
            Diag.Ignore(e);
            SetCachedPlayerControl(false);
            return false;
        }
    }
    public static bool InPlayerControlCached() => Volatile.Read(ref cachedPlayerControlForHooks) != 0;
    private static void SetCachedPlayerControl(bool value) {
        cachedPlayerControl = value;
        Volatile.Write(ref cachedPlayerControlForHooks, value ? 1 : 0);
    }
    private static readonly HashSet<int> cachedAllowedKeys = [];
    private static int[] cachedAllowedSource;
    private static int cachedAllowedLength = -1;
    public static bool IsAllowedKey(KeyCode key) {
        int[] allowed = Conf?.AllowedKeys;
        if(allowed == null) return false;
        if(!ReferenceEquals(allowed, cachedAllowedSource) || allowed.Length != cachedAllowedLength) {
            cachedAllowedKeys.Clear();
            for(int i = 0; i < allowed.Length; i++) {
                cachedAllowedKeys.Add((int)KeyCodes.Normalize((KeyCode)allowed[i]));
            }
            cachedAllowedSource = allowed;
            cachedAllowedLength = allowed.Length;
        }
        return cachedAllowedKeys.Contains((int)KeyCodes.Normalize(key));
    }
    public static bool IsMouseKey(KeyCode key) => KeyCodes.IsMouse(key);
    private static KeyCode NavTwinToNumpad(KeyCode key) => key switch {
        KeyCode.Insert => KeyCode.Keypad0,
        KeyCode.End => KeyCode.Keypad1,
        KeyCode.DownArrow => KeyCode.Keypad2,
        KeyCode.PageDown => KeyCode.Keypad3,
        KeyCode.LeftArrow => KeyCode.Keypad4,
        KeyCode.Clear => KeyCode.Keypad5,
        KeyCode.RightArrow => KeyCode.Keypad6,
        KeyCode.Home => KeyCode.Keypad7,
        KeyCode.UpArrow => KeyCode.Keypad8,
        KeyCode.PageUp => KeyCode.Keypad9,
        KeyCode.Delete => KeyCode.KeypadPeriod,
        _ => KeyCode.None,
    };
    public static bool ShouldBlockKey(KeyCode key) {
        if(!IsActive() || !InPlayerControl() || IsMouseKey(key)) return false;
        if(IsAllowedKey(key)) return false;
        KeyCode numpadOrigin = HookInput.IsMacOSRuntime ? KeyCode.None : NavTwinToNumpad(key);
        return numpadOrigin == KeyCode.None || !IsAllowedKey(numpadOrigin);
    }
    public static void ToggleAllowedKey(KeyCode key) {
        EnsureConf();
        key = KeyCodes.Normalize(key);
        if(key == KeyCode.None || IsMouseKey(key)) return;
        List<int> keys = [.. Conf.AllowedKeys];
        if(!keys.Remove((int)key)) keys.Add((int)key);
        Conf.AllowedKeys = [.. keys];
        PersistChange();
    }
    public static void SetAllowedKeys(int[] keys) {
        EnsureConf();
        Conf.AllowedKeys = keys ?? [];
        PersistChange();
    }
    public static IReadOnlyList<KeyLimiterProfile> Profiles {
        get { EnsureConf(); return Conf.Profiles; }
    }
    public static int ActiveProfileIndex {
        get { EnsureConf(); return Conf.ActiveProfile; }
    }
    public static void SwitchProfile(int index) {
        EnsureConf();
        if(index < 0 || index >= Conf.Profiles.Count || index == Conf.ActiveProfile) return;
        CancelCapture();
        Conf.ActiveProfile = index;
        PersistChange();
    }
    public static void AddProfile() {
        EnsureConf();
        Conf.Profiles.Add(new KeyLimiterProfile {
            Name = "Profile " + (Conf.Profiles.Count + 1),
            Keys = [],
        });
        Conf.ActiveProfile = Conf.Profiles.Count - 1;
        PersistChange();
    }
    public static void RemoveActiveProfile() {
        EnsureConf();
        if(Conf.Profiles.Count <= 1) return;
        CancelCapture();
        Conf.Profiles.RemoveAt(Conf.ActiveProfile);
        if(Conf.ActiveProfile >= Conf.Profiles.Count) Conf.ActiveProfile = Conf.Profiles.Count - 1;
        PersistChange();
    }
    public static void RenameActiveProfile(string name) {
        EnsureConf();
        Conf.ActiveProfileOrDefault().Name = name ?? "";
        PersistChange();
    }
    [Obsolete("Moved to Quartz.Utility.KeyCodes.Normalize.")]
    public static KeyCode NormalizeKey(KeyCode key) => KeyCodes.Normalize(key);
    [Obsolete("Moved to Quartz.Utility.KeyCodes.NormalizeNumeric.")]
    public static KeyCode NormalizeNumericKey(int numeric) => KeyCodes.NormalizeNumeric(numeric);
    public static bool IsMouseLabel(KeyLabel label) => HookInput.IsMouseLabel(label);
    public static bool ShouldBlockAsyncKeyFromHook(ushort key, KeyLabel label) {
        if(!IsActive() || !InPlayerControlCached() || IsMouseLabel(label)) return false;
        KeyCode unityKey = HookKeyToPhysicalUnityKey(key, label);
        if(IsMouseKey(unityKey)) return false;
        if(unityKey != KeyCode.None && IsAllowedKey(unityKey)) return false;
        KeyCode mappedKey = GameApi.HookKeyToUnityKey(label);
        if(mappedKey == KeyCode.None && IsAllowedGenericModifierVirtualKey(key)) return false;
        if(IsPreciseWindowsVirtualKey(key)) return true;
        return mappedKey == KeyCode.None || !IsAllowedKey(mappedKey);
    }
    private static bool IsPreciseWindowsVirtualKey(ushort key) =>
        HookInput.IsWindowsRuntime && key is not (0x10 or 0x11 or 0x12)
        && KeyCodes.WindowsVirtualKeyToUnityKey(key) != KeyCode.None;
    private static bool IsAllowedGenericModifierVirtualKey(ushort key) {
        switch(key) {
            case 0x10:
                return IsAllowedKey(KeyCode.LeftShift) || IsAllowedKey(KeyCode.RightShift);
            case 0x11:
                return IsAllowedKey(KeyCode.LeftControl) || IsAllowedKey(KeyCode.RightControl);
            case 0x12:
                return IsAllowedKey(KeyCode.LeftAlt) || IsAllowedKey(KeyCode.RightAlt)
                    || IsAllowedKey(KeyCode.AltGr);
            default:
                return false;
        }
    }
    // The hook feed itself now lives in Quartz.Game.HookInput (core), so the key
    // viewer no longer needs this module installed to see hook-only keys. These
    // stay as forwarders: they are public API and this module's own code, the
    // chatter blocker and third-party addons all still call them.
    public static bool IsHookTrackedKey(KeyCode key) => HookInput.IsHookTrackedKey(key);
    public static void NoteHookEvent(KeyCode key, bool pressed) => HookInput.NoteHookEvent(key, pressed);
    public static bool HookEverSaw(KeyCode key) => HookInput.HookEverSaw(key);
    public static bool HookKeyHeld(KeyCode key) => HookInput.HookKeyHeld(key);
    public static KeyCode HookKeyToPhysicalUnityKey(ushort key, KeyLabel label) =>
        HookInput.HookKeyToPhysicalUnityKey(key, label);
    public static bool IsMacOSRuntime() => HookInput.IsMacOSRuntime;
    public static bool TryMacPhysicalKeyHeld(KeyCode key, out bool held) =>
        HookInput.TryMacPhysicalKeyHeld(key, out held);
    public static bool IsCapturing { get; private set; }
    private static readonly object captureOwner = new();
    private static Action<KeyCode> captureOnKey;
    private static Action captureOnEnded;
    public static void StartCapture(Action<KeyCode> onKey, Action onEnded) {
        CancelCapture();
        IsCapturing = true;
        captureOnKey = onKey;
        captureOnEnded = onEnded;
        if(!KeyCaptureCoordinator.Claim(captureOwner, CancelCapture)) {
            if(IsCapturing) EndCapture(KeyCode.None);
            return;
        }
        Changed?.Invoke();
    }
    public static void CancelCapture() => EndCapture(KeyCode.None);
    private static void EndCapture(KeyCode key) {
        if(!IsCapturing) return;
        IsCapturing = false;
        KeyCaptureCoordinator.Release(captureOwner);
        Action<KeyCode> onKey = captureOnKey;
        Action onEnded = captureOnEnded;
        captureOnKey = null;
        captureOnEnded = null;
        if(key != KeyCode.None && key != KeyCode.Escape) onKey?.Invoke(key);
        onEnded?.Invoke();
        Changed?.Invoke();
    }
    public static void ClearAllowedKeys() {
        EnsureConf();
        Conf.AllowedKeys = [];
        PersistChange();
    }
    public static void ReplaceAllowedKey(KeyCode oldKey, KeyCode newKey) {
        EnsureConf();
        oldKey = KeyCodes.Normalize(oldKey);
        newKey = KeyCodes.Normalize(newKey);
        if(newKey == KeyCode.None || IsMouseKey(newKey)) return;
        List<int> keys = [.. Conf.AllowedKeys];
        int index = keys.IndexOf((int)oldKey);
        if(index < 0) {
            ToggleAllowedKey(newKey);
            return;
        }
        if(keys.Contains((int)newKey)) {
            keys.RemoveAt(index);
        } else {
            keys[index] = (int)newKey;
        }
        Conf.AllowedKeys = [.. keys];
        PersistChange();
    }
    private static void PersistChange() {
        Save();
        Changed?.Invoke();
    }
    private static Ticker ticker;
    private static void EnsureTicker() {
        if(ticker != null || MainCore.Root == null) return;
        ticker = MainCore.Root.AddComponent<Ticker>();
    }
    private static KeyCode[] captureCandidates;
    private static KeyCode[] CaptureCandidates {
        get {
            if(captureCandidates != null) return captureCandidates;
            List<KeyCode> list = [];
            foreach(KeyCode key in Enum.GetValues(typeof(KeyCode))) {
                if(key == KeyCode.None || IsMouseKey(key) || key >= KeyCode.JoystickButton0) continue;
                list.Add(key);
            }
            captureCandidates = [.. list];
            return captureCandidates;
        }
    }
}
