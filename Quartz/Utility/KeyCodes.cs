using UnityEngine;
namespace Quartz.Utility;
public static class KeyCodes {
    private const int LegacyAsyncKeyOffset = 0x1000;
    private const int LegacyAsyncKeyMax = LegacyAsyncKeyOffset + 0xFF;
    public static bool IsMouse(KeyCode key) => key is >= KeyCode.Mouse0 and <= KeyCode.Mouse6;
    public static KeyCode Normalize(KeyCode key) {
        key = NormalizeLegacyAsync(key);
        if(key == KeyCode.AltGr) return KeyCode.RightAlt;
        if(key == KeyCode.KeypadEnter) return KeyCode.Return;
        return key;
    }
    public static KeyCode NormalizeNumeric(int numeric) {
        if(numeric >= 0 && numeric <= 0xFF) {
            KeyCode vk = WindowsVirtualKeyToUnityKey((ushort)numeric);
            if(vk != KeyCode.None) return vk;
        }
        return Normalize((KeyCode)numeric);
    }
    private static KeyCode NormalizeLegacyAsync(KeyCode key) {
        int raw = (int)key;
        if(raw < LegacyAsyncKeyOffset || raw > LegacyAsyncKeyMax) return key;
        KeyCode mapped = WindowsVirtualKeyToUnityKey((ushort)(raw - LegacyAsyncKeyOffset));
        return mapped == KeyCode.None ? key : mapped;
    }
    public static KeyCode WindowsVirtualKeyToUnityKey(ushort key) => key switch {
        0x15 or 0xA5 => KeyCode.RightAlt,
        0x19 or 0xA3 => KeyCode.RightControl,
        0x10 or 0xA0 => KeyCode.LeftShift,
        0x11 or 0xA2 => KeyCode.LeftControl,
        0x12 or 0xA4 => KeyCode.LeftAlt,
        >= 0x30 and <= 0x39 => (KeyCode)((int)KeyCode.Alpha0 + (key - 0x30)),
        >= 0x41 and <= 0x5A => (KeyCode)((int)KeyCode.A + (key - 0x41)),
        >= 0x60 and <= 0x69 => (KeyCode)((int)KeyCode.Keypad0 + (key - 0x60)),
        >= 0x70 and <= 0x7E => (KeyCode)((int)KeyCode.F1 + (key - 0x70)),
        0x5D => KeyCode.Menu,
        0x08 => KeyCode.Backspace,
        0x09 => KeyCode.Tab,
        0x0D => KeyCode.Return,
        0x13 => KeyCode.Pause,
        0x14 => KeyCode.CapsLock,
        0x1B => KeyCode.Escape,
        0x20 => KeyCode.Space,
        0x21 => KeyCode.PageUp,
        0x22 => KeyCode.PageDown,
        0x23 => KeyCode.End,
        0x24 => KeyCode.Home,
        0x25 => KeyCode.LeftArrow,
        0x26 => KeyCode.UpArrow,
        0x27 => KeyCode.RightArrow,
        0x28 => KeyCode.DownArrow,
        0x2C => KeyCode.Print,
        0x2D => KeyCode.Insert,
        0x2E => KeyCode.Delete,
        0x5B => KeyCode.LeftWindows,
        0x5C => KeyCode.RightWindows,
        0x6A => KeyCode.KeypadMultiply,
        0x6B => KeyCode.KeypadPlus,
        0x6D => KeyCode.KeypadMinus,
        0x6E => KeyCode.KeypadPeriod,
        0x6F => KeyCode.KeypadDivide,
        0x90 => KeyCode.Numlock,
        0x91 => KeyCode.ScrollLock,
        0xA1 => KeyCode.RightShift,
        0xBA => KeyCode.Semicolon,
        0xBB => KeyCode.Equals,
        0xBC => KeyCode.Comma,
        0xBD => KeyCode.Minus,
        0xBE => KeyCode.Period,
        0xBF => KeyCode.Slash,
        0xC0 => KeyCode.BackQuote,
        0xDB => KeyCode.LeftBracket,
        0xDC => KeyCode.Backslash,
        0xDD => KeyCode.RightBracket,
        0xDE => KeyCode.Quote,
        _ => KeyCode.None,
    };
}
