using System.Collections.Concurrent;
using System.Threading;
using HarmonyLib;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Utility;
using SkyHook;
using UnityEngine;
namespace Quartz.Game;
/// <summary>
/// The raw keyboard-hook feed: SkyHook events mapped to physical
/// <see cref="KeyCode"/>s, the held/seen bit state behind them, and the platform
/// probes that decide which keys Unity's own <c>Input</c> cannot see.
/// </summary>
/// <remarks>
/// This used to live inside the key-limiter module, with the key viewer reaching
/// across for it. That made the key viewer silently dependent on an unrelated
/// feature: with the key limiter uninstalled nothing patched
/// <see cref="SkyHookManager"/>, so no hook events were raised at all and every
/// hook-only key (RightAlt, RightControl, the non-Windows modifiers, the Mac
/// numpad) went dark. Core owns the feed now — <see cref="HookKeys"/> was already
/// the seam for it — and both modules read from here.
/// </remarks>
public static class HookInput {
    // 11 words * 64 bits covers the whole KeyCode keyboard range.
    private const int HookBitWords = 11;
    private const int HookBitCapacity = HookBitWords << 6;
    private static readonly long[] hookHeldBits = new long[HookBitWords];
    private static readonly long[] hookSeenBits = new long[HookBitWords];
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
    public static bool IsWindowsRuntime => WinRuntimeCached;
    public static bool IsMacOSRuntime => MacRuntimeCached;
    public static bool IsMouseLabel(KeyLabel label) => label is
        KeyLabel.MouseLeft or KeyLabel.MouseRight or KeyLabel.MouseMiddle or KeyLabel.MouseX1 or KeyLabel.MouseX2;
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
    /// <summary>Keys Unity's own input cannot report on this platform.</summary>
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
    /// <summary>Records a hook edge. Called from the hook thread.</summary>
    public static void NoteHookEvent(KeyCode key, bool pressed) {
        if(key == KeyCode.None) return;
        if(!HookBitSlot(key, out int word, out long mask)) return;
        HookBitSet(ref hookSeenBits[word], mask);
        if(!IsHookTrackedKey(key)) return;
        if(pressed) HookBitSet(ref hookHeldBits[word], mask);
        else HookBitClear(ref hookHeldBits[word], mask);
    }
    public static bool HookEverSaw(KeyCode key) =>
        HookBitSlot(key, out int word, out long mask)
        && (Volatile.Read(ref hookSeenBits[word]) & mask) != 0L;
    public static bool HookKeyHeld(KeyCode key) {
        if(key == KeyCode.None) return false;
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
    [System.Runtime.InteropServices.DllImport(
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool CGEventSourceKeyState(int sourceStateID, ushort keyCode);
    private const int KCGEventSourceStateHidSystemState = 1;
    /// <summary>
    /// macOS only: reads the physical key state the window server sees, for keys
    /// Unity reports unreliably under a grabbed keyboard.
    /// </summary>
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
    /// <summary>
    /// The one subscription to the game's keyboard hook. Core-owned so the feed
    /// exists whenever Quartz does, not only when the key limiter is installed.
    /// Runs on the hook thread — keep it allocation-free and non-throwing.
    /// </summary>
    [HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
    private static class HookCallbackPatch {
        private static void Prefix(SkyHookEvent __0) {
            try {
                SkyHookEvent ev = __0;
                if(IsMouseLabel(ev.Label)) return;
                KeyCode key = HookKeyToPhysicalUnityKey(ev.Key, ev.Label);
                bool down = ev.Type == SkyHook.EventType.KeyPressed;
                NoteHookEvent(key, down);
                HookKeys.RaiseKeyEvent(key, down);
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
}
