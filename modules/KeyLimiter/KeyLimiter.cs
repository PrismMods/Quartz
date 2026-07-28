using Quartz.Core;
using Quartz.IO;
using MonsterLove.StateMachine;
using SkyHook;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using Quartz.Compat.Game;
using Quartz.Utility;
namespace Quartz.Features.KeyLimiter;
public static partial class KeyLimiter {
    public static SettingsFile<KeyLimiterSettings> ConfMgr { get; private set; }
    public static KeyLimiterSettings Conf => ConfMgr?.Data;
    public static event Action Changed;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = SettingsFile<KeyLimiterSettings>.Loaded("KeyLimiter.json");
        Quartz.Game.HookKeys.Register("keylimiter", HookKeyHeld, IsHookTrackedKey);
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
        KeyCode numpadOrigin = MacRuntimeCached ? KeyCode.None : NavTwinToNumpad(key);
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
    public static bool IsMouseLabel(KeyLabel label) => label is
        KeyLabel.MouseLeft or KeyLabel.MouseRight or KeyLabel.MouseMiddle or KeyLabel.MouseX1 or KeyLabel.MouseX2;
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
        WinRuntimeCached && key is not (0x10 or 0x11 or 0x12)
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
    private const int HookBitWords = 11;
    private const int HookBitCapacity = HookBitWords << 6;
    private static readonly long[] hookHeldBits = new long[HookBitWords];
    private static readonly long[] hookSeenBits = new long[HookBitWords];
    private static volatile bool hookActive;
    private static bool IsHookOnlyKey(KeyCode key) {
        if(key is KeyCode.RightAlt or KeyCode.RightControl) return true;
        return !WinRuntimeCached && key is
            KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl or KeyCode.LeftAlt;
    }
    private static bool IsNumpadHookKey(KeyCode key) => key is
        KeyCode.Keypad0 or KeyCode.Keypad1 or KeyCode.Keypad2 or KeyCode.Keypad3 or KeyCode.Keypad4 or
        KeyCode.Keypad5 or KeyCode.Keypad6 or KeyCode.Keypad7 or KeyCode.Keypad8 or KeyCode.Keypad9 or
        KeyCode.KeypadPeriod or KeyCode.KeypadDivide or KeyCode.KeypadMultiply or
        KeyCode.KeypadMinus or KeyCode.KeypadPlus;
    public static bool IsHookTrackedKey(KeyCode key) =>
        IsHookOnlyKey(key) || (MacRuntimeCached && IsNumpadHookKey(key));
    private static bool HookBitSlot(KeyCode key, out int word, out long mask) {
        int raw = (int)key;
        if(raw <= 0 || raw >= HookBitCapacity) {
            word = 0;
            mask = 0L;
            return false;
        }
        word = raw >> 6;
        mask = 1L << (raw & 63);
        return true;
    }
    private static void HookBitSet(ref long slot, long mask) {
        long seen = Volatile.Read(ref slot);
        while((seen & mask) == 0L) {
            long prior = Interlocked.CompareExchange(ref slot, seen | mask, seen);
            if(prior == seen) return;
            seen = prior;
        }
    }
    private static void HookBitClear(ref long slot, long mask) {
        long seen = Volatile.Read(ref slot);
        while((seen & mask) != 0L) {
            long prior = Interlocked.CompareExchange(ref slot, seen & ~mask, seen);
            if(prior == seen) return;
            seen = prior;
        }
    }
    public static void NoteHookEvent(KeyCode key, bool pressed) {
        if(key == KeyCode.None) return;
        if(!HookBitSlot(key, out int word, out long mask)) return;
        HookBitSet(ref hookSeenBits[word], mask);
        if(!IsHookTrackedKey(key)) return;
        if(pressed) HookBitSet(ref hookHeldBits[word], mask);
        else HookBitClear(ref hookHeldBits[word], mask);
        bool anyHeld = false;
        for(int i = 0; i < HookBitWords; i++) {
            if(Volatile.Read(ref hookHeldBits[i]) == 0L) continue;
            anyHeld = true;
            break;
        }
        hookActive = anyHeld;
    }
    public static bool HookEverSaw(KeyCode key) =>
        HookBitSlot(key, out int word, out long mask)
        && (Volatile.Read(ref hookSeenBits[word]) & mask) != 0L;
    public static bool HookKeyHeld(KeyCode key) {
        if(!hookActive || key == KeyCode.None) return false;
        return HookBitSlot(key, out int word, out long mask)
            && (Volatile.Read(ref hookHeldBits[word]) & mask) != 0L;
    }
    public static KeyCode HookKeyToPhysicalUnityKey(ushort key, KeyLabel label) {
        KeyCode labelKey = GameApi.HookKeyToUnityKey(label);
        if(IsNumpadOrArrowKey(labelKey)) return labelKey;
        if(WinRuntimeCached) {
            KeyCode hookKey = KeyCodes.WindowsVirtualKeyToUnityKey(key);
            if(hookKey != KeyCode.None) return hookKey;
        }
        KeyCode mapped = AsyncLabelToPhysicalUnityKey(label);
        if(mapped != KeyCode.None) return mapped;
        return KeyCode.None;
    }
    private static bool IsNumpadOrArrowKey(KeyCode key) => key is
        KeyCode.UpArrow or KeyCode.DownArrow or KeyCode.LeftArrow or KeyCode.RightArrow or
        KeyCode.Keypad0 or KeyCode.Keypad1 or KeyCode.Keypad2 or KeyCode.Keypad3 or KeyCode.Keypad4 or
        KeyCode.Keypad5 or KeyCode.Keypad6 or KeyCode.Keypad7 or KeyCode.Keypad8 or KeyCode.Keypad9 or
        KeyCode.KeypadPeriod or KeyCode.KeypadDivide or KeyCode.KeypadMultiply or KeyCode.KeypadMinus or
        KeyCode.KeypadPlus or KeyCode.KeypadEnter;
    private static readonly bool WinRuntimeCached =
        ResolvePlatform(RuntimePlatform.WindowsPlayer, RuntimePlatform.WindowsEditor);
    private static readonly bool MacRuntimeCached =
        ResolvePlatform(RuntimePlatform.OSXPlayer, RuntimePlatform.OSXEditor);
    private static bool ResolvePlatform(RuntimePlatform player, RuntimePlatform editor) {
        try {
            RuntimePlatform platform = Application.platform;
            return platform == player || platform == editor;
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    public static bool IsMacOSRuntime() => MacRuntimeCached;
    [System.Runtime.InteropServices.DllImport(
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool CGEventSourceKeyState(int sourceStateID, ushort keyCode);
    private const int KCGEventSourceStateHidSystemState = 1;
    public static bool TryMacPhysicalKeyHeld(KeyCode key, out bool held) {
        held = false;
        if(!MacRuntimeCached) return false;
        ushort vk = key switch {
            KeyCode.Tab => 0x30,
            _ => ushort.MaxValue,
        };
        if(vk == ushort.MaxValue) return false;
        try {
            held = CGEventSourceKeyState(KCGEventSourceStateHidSystemState, vk);
            return true;
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    private static readonly ConcurrentDictionary<KeyLabel, KeyCode> asyncLabelCache = new();
    private static KeyCode AsyncLabelToPhysicalUnityKey(KeyLabel label) {
        if(asyncLabelCache.TryGetValue(label, out KeyCode cached)) return cached;
        KeyCode resolved = ResolveAsyncLabelToPhysicalUnityKey(label);
        asyncLabelCache[label] = resolved;
        return resolved;
    }
    private static KeyCode ResolveAsyncLabelToPhysicalUnityKey(KeyLabel label) {
        string name = label.ToString();
        if(name.Length == 1 && name[0] >= 'A' && name[0] <= 'Z')
            return (KeyCode)((int)KeyCode.A + (name[0] - 'A'));
        if(name.Length == 6 && name.StartsWith("Alpha") && name[5] >= '0' && name[5] <= '9')
            return (KeyCode)((int)KeyCode.Alpha0 + (name[5] - '0'));
        if(name.Length >= 2 && name[0] == 'F'
            && int.TryParse(name[1..], out int functionKey) && functionKey >= 1 && functionKey <= 15)
            return (KeyCode)((int)KeyCode.F1 + (functionKey - 1));
        if(name.Length == 7 && name.StartsWith("Keypad") && name[6] >= '0' && name[6] <= '9')
            return (KeyCode)((int)KeyCode.Keypad0 + (name[6] - '0'));
        return name switch {
            "Escape" => KeyCode.Escape,
            "Grave" => KeyCode.BackQuote,
            "Minus" => KeyCode.Minus,
            "Equal" => KeyCode.Equals,
            "Backspace" => KeyCode.Backspace,
            "Tab" => KeyCode.Tab,
            "LeftBrace" => KeyCode.LeftBracket,
            "RightBrace" => KeyCode.RightBracket,
            "BackSlash" => KeyCode.Backslash,
            "CapsLock" => KeyCode.CapsLock,
            "Semicolon" => KeyCode.Semicolon,
            "Apostrophe" => KeyCode.Quote,
            "Enter" => KeyCode.Return,
            "LShift" or "LeftShift" => KeyCode.LeftShift,
            "RShift" or "RightShift" => KeyCode.RightShift,
            "Comma" => KeyCode.Comma,
            "Dot" => KeyCode.Period,
            "Slash" => KeyCode.Slash,
            "LControl" or "LCtrl" or "LeftControl" or "LeftCtrl" => KeyCode.LeftControl,
            "RControl" or "RCtrl" or "RightControl" or "RightCtrl" or "Hanja" => KeyCode.RightControl,
            "Super" => KeyCode.LeftCommand,
            "LWin" or "LeftWin" or "LeftWindows" => KeyCode.LeftWindows,
            "RWin" or "RightWin" or "RightWindows" => KeyCode.RightWindows,
            "LAlt" => KeyCode.LeftAlt,
            "RAlt" or "AltGr" or "Hangul" => KeyCode.RightAlt,
            "Space" => KeyCode.Space,
            "PrintScreen" => KeyCode.Print,
            "ScrollLock" => KeyCode.ScrollLock,
            "PauseBreak" => KeyCode.Pause,
            "Insert" => KeyCode.Insert,
            "Home" => KeyCode.Home,
            "PageUp" => KeyCode.PageUp,
            "Delete" => KeyCode.Delete,
            "End" => KeyCode.End,
            "PageDown" => KeyCode.PageDown,
            "ArrowUp" => KeyCode.UpArrow,
            "ArrowLeft" => KeyCode.LeftArrow,
            "ArrowDown" => KeyCode.DownArrow,
            "ArrowRight" => KeyCode.RightArrow,
            "NumLock" => KeyCode.Numlock,
            "KeypadSlash" => KeyCode.KeypadDivide,
            "KeypadAsterisk" => KeyCode.KeypadMultiply,
            "KeypadMinus" => KeyCode.KeypadMinus,
            "KeypadDot" => KeyCode.KeypadPeriod,
            "KeypadPlus" => KeyCode.KeypadPlus,
            "KeypadEnter" => KeyCode.KeypadEnter,
            "Application" or "Apps" or "Menu" => KeyCode.Menu,
            "MouseLeft" => KeyCode.Mouse0,
            "MouseRight" => KeyCode.Mouse1,
            "MouseMiddle" => KeyCode.Mouse2,
            "MouseX1" => KeyCode.Mouse3,
            "MouseX2" => KeyCode.Mouse4,
            _ => GameApi.HookKeyToUnityKey(label),
        };
    }
    public static bool IsCapturing { get; private set; }
    private static Action<KeyCode> captureOnKey;
    private static Action captureOnEnded;
    public static void StartCapture(Action<KeyCode> onKey, Action onEnded) {
        CancelCapture();
        IsCapturing = true;
        captureOnKey = onKey;
        captureOnEnded = onEnded;
        Keybind.Capturing = true;
        Changed?.Invoke();
    }
    public static void CancelCapture() => EndCapture(KeyCode.None);
    private static void EndCapture(KeyCode key) {
        if(!IsCapturing) return;
        IsCapturing = false;
        Keybind.Capturing = false;
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
