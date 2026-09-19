using System.Collections.Concurrent;
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
        return key is KeyCode.Tab or KeyCode.Backslash
            or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftShift or KeyCode.RightShift
            or KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftCommand or KeyCode.RightCommand
            && TryMacKeyState(key, out held);
    }
    /// <summary>
    /// Physical held state for gameplay polling. Unity's macOS input folds the
    /// right-hand modifiers into their left twins (RAlt reads as LeftAlt), so on
    /// macOS those keys come from the window server instead.
    /// </summary>
    public static bool PhysicalKeyHeld(KeyCode key) {
        if(MacRuntimeCached && TryMacPhysicalKeyHeld(key, out bool held)) return held;
        return Input.GetKey(key);
    }
    /// <summary>
    /// macOS only: window-server key state for any keyboard key. SkyHook's macOS
    /// backend reads raw HID values, so keyboards whose extra rollover the OS
    /// understands but SkyHook's HID matching does not stop reporting after six
    /// keys; this state has no such cap.
    /// </summary>
    public static bool TryMacKeyState(KeyCode key, out bool held) {
        held = false;
        if(!MacRuntimeCached) return false;
        ushort vk = MacVirtualKey(key);
        if(vk == ushort.MaxValue) return false;
        try {
            held = CGEventSourceKeyState(KCGEventSourceStateHidSystemState, vk);
            return true;
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    private static ushort MacVirtualKey(KeyCode key) => key switch {
        KeyCode.A => 0x00, KeyCode.S => 0x01, KeyCode.D => 0x02, KeyCode.F => 0x03, KeyCode.H => 0x04,
        KeyCode.G => 0x05, KeyCode.Z => 0x06, KeyCode.X => 0x07, KeyCode.C => 0x08, KeyCode.V => 0x09,
        KeyCode.B => 0x0B, KeyCode.Q => 0x0C, KeyCode.W => 0x0D, KeyCode.E => 0x0E, KeyCode.R => 0x0F,
        KeyCode.Y => 0x10, KeyCode.T => 0x11, KeyCode.Alpha1 => 0x12, KeyCode.Alpha2 => 0x13,
        KeyCode.Alpha3 => 0x14, KeyCode.Alpha4 => 0x15, KeyCode.Alpha6 => 0x16, KeyCode.Alpha5 => 0x17,
        KeyCode.Equals => 0x18, KeyCode.Alpha9 => 0x19, KeyCode.Alpha7 => 0x1A, KeyCode.Minus => 0x1B,
        KeyCode.Alpha8 => 0x1C, KeyCode.Alpha0 => 0x1D, KeyCode.RightBracket => 0x1E, KeyCode.O => 0x1F,
        KeyCode.U => 0x20, KeyCode.LeftBracket => 0x21, KeyCode.I => 0x22, KeyCode.P => 0x23,
        KeyCode.Return => 0x24, KeyCode.L => 0x25, KeyCode.J => 0x26, KeyCode.Quote => 0x27, KeyCode.K => 0x28,
        KeyCode.Semicolon => 0x29, KeyCode.Backslash => 0x2A, KeyCode.Comma => 0x2B, KeyCode.Slash => 0x2C,
        KeyCode.N => 0x2D, KeyCode.M => 0x2E, KeyCode.Period => 0x2F, KeyCode.Tab => 0x30, KeyCode.Space => 0x31,
        KeyCode.BackQuote => 0x32, KeyCode.Backspace => 0x33, KeyCode.Escape => 0x35,
        KeyCode.RightCommand => 0x36, KeyCode.LeftCommand => 0x37, KeyCode.LeftShift => 0x38,
        KeyCode.CapsLock => 0x39, KeyCode.LeftAlt => 0x3A, KeyCode.LeftControl => 0x3B,
        KeyCode.RightShift => 0x3C, KeyCode.RightAlt => 0x3D, KeyCode.RightControl => 0x3E,
        KeyCode.KeypadPeriod => 0x41, KeyCode.KeypadMultiply => 0x43, KeyCode.KeypadPlus => 0x45,
        KeyCode.KeypadDivide => 0x4B, KeyCode.KeypadEnter => 0x4C, KeyCode.KeypadMinus => 0x4E,
        KeyCode.KeypadEquals => 0x51, KeyCode.Keypad0 => 0x52, KeyCode.Keypad1 => 0x53, KeyCode.Keypad2 => 0x54,
        KeyCode.Keypad3 => 0x55, KeyCode.Keypad4 => 0x56, KeyCode.Keypad5 => 0x57, KeyCode.Keypad6 => 0x58,
        KeyCode.Keypad7 => 0x59, KeyCode.Keypad8 => 0x5B, KeyCode.Keypad9 => 0x5C,
        KeyCode.F1 => 0x7A, KeyCode.F2 => 0x78, KeyCode.F3 => 0x63, KeyCode.F4 => 0x76, KeyCode.F5 => 0x60,
        KeyCode.F6 => 0x61, KeyCode.F7 => 0x62, KeyCode.F8 => 0x64, KeyCode.F9 => 0x65, KeyCode.F10 => 0x6D,
        KeyCode.F11 => 0x67, KeyCode.F12 => 0x6F, KeyCode.F13 => 0x69, KeyCode.F14 => 0x6B, KeyCode.F15 => 0x71,
        KeyCode.Insert => 0x72, KeyCode.Home => 0x73, KeyCode.PageUp => 0x74, KeyCode.Delete => 0x75,
        KeyCode.End => 0x77, KeyCode.PageDown => 0x79, KeyCode.LeftArrow => 0x7B, KeyCode.RightArrow => 0x7C,
        KeyCode.DownArrow => 0x7D, KeyCode.UpArrow => 0x7E,
        _ => ushort.MaxValue,
    };
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
        private static bool Prefix(ref SkyHookEvent __0) {
            try {
                if(IsMouseLabel(__0.Label)) return true;
                bool down = __0.Type == SkyHook.EventType.KeyPressed;
                if(MacRuntimeCached && down && !FixMacRightModifier(ref __0)) return false;
                KeyCode key = HookKeyToPhysicalUnityKey(__0.Key, __0.Label);
                NoteHookEvent(key, down);
                HookKeys.RaiseKeyEvent(key, down);
            } catch(Exception e) { Diag.Ignore(e); }
            return true;
        }
    }
    /// <summary>
    /// macOS SkyHook reports a right-hand modifier press as its left twin (RAlt
    /// down arrives as LAlt) while the release arrives correctly as the right key.
    /// The left key then sticks held and the press counts twice. A left press whose
    /// key the window server says is up, while the right twin is down, is the right
    /// key: relabel it, or drop it when the right key is already held.
    /// </summary>
    private static bool FixMacRightModifier(ref SkyHookEvent ev) {
        KeyLabel right;
        ushort leftVk, rightVk;
        switch(ev.Label) {
            case KeyLabel.LAlt: right = KeyLabel.RAlt; leftVk = 0x3A; rightVk = 0x3D; break;
            case KeyLabel.LShift: right = KeyLabel.RShift; leftVk = 0x38; rightVk = 0x3C; break;
            case KeyLabel.LControl: right = KeyLabel.RControl; leftVk = 0x3B; rightVk = 0x3E; break;
            default: return true;
        }
        bool leftHeld, rightHeld;
        try {
            leftHeld = CGEventSourceKeyState(KCGEventSourceStateHidSystemState, leftVk);
            rightHeld = CGEventSourceKeyState(KCGEventSourceStateHidSystemState, rightVk);
        } catch(Exception e) {
            Diag.Ignore(e);
            return true;
        }
        if(leftHeld || !rightHeld) return true;
        if(HookKeyHeld(AsyncLabelToPhysicalUnityKey(right))) return false;
        SetLabel(ref ev, right);
        return true;
    }
    private static readonly int LabelOffset =
        (int)System.Runtime.InteropServices.Marshal.OffsetOf(typeof(SkyHookEvent), nameof(SkyHookEvent.Label));
    private static unsafe void SetLabel(ref SkyHookEvent ev, KeyLabel label) {
        fixed(SkyHookEvent* p = &ev) *(KeyLabel*)((byte*)p + LabelOffset) = label;
    }
}
