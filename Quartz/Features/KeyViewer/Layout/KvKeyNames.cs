using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
internal static class KvKeyNames {
    private const string GlobalRightControl = "25";
    private const string GlobalRightAlt = "21";
    private const string GlobalLeftWindows = "91";
    private const string GlobalRightWindows = "92";
    private const string GlobalPause = "19";
    internal static string ToGlobalKey(KeyCode key) {
        if(key >= KeyCode.A && key <= KeyCode.Z) return ((char)('A' + (key - KeyCode.A))).ToString();
        if(key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((char)('0' + (key - KeyCode.Alpha0))).ToString();
        if(key >= KeyCode.F1 && key <= KeyCode.F12) return "F" + (key - KeyCode.F1 + 1);
        if(key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "NUMPAD " + (key - KeyCode.Keypad0);
        return key switch {
            KeyCode.KeypadMultiply => "NUMPAD MULTIPLY",
            KeyCode.KeypadPlus => "NUMPAD PLUS",
            KeyCode.KeypadMinus => "NUMPAD MINUS",
            KeyCode.KeypadPeriod => "NUMPAD DELETE",
            KeyCode.KeypadDivide => "NUMPAD DIVIDE",
            KeyCode.KeypadEnter => "NUMPAD RETURN",
            KeyCode.LeftShift => "LEFT SHIFT",
            KeyCode.RightShift => "RIGHT SHIFT",
            KeyCode.LeftControl => "LEFT CTRL",
            KeyCode.LeftAlt => "LEFT ALT",
            KeyCode.RightControl => GlobalRightControl,
            KeyCode.RightAlt => GlobalRightAlt,
            KeyCode.LeftWindows or KeyCode.LeftCommand => GlobalLeftWindows,
            KeyCode.RightWindows or KeyCode.RightCommand => GlobalRightWindows,
            KeyCode.Pause => GlobalPause,
            KeyCode.Space => "SPACE",
            KeyCode.Return => "RETURN",
            KeyCode.Tab => "TAB",
            KeyCode.Backspace => "BACKSPACE",
            KeyCode.CapsLock => "CAPS LOCK",
            KeyCode.Escape => "ESCAPE",
            KeyCode.UpArrow => "UP ARROW",
            KeyCode.DownArrow => "DOWN ARROW",
            KeyCode.LeftArrow => "LEFT ARROW",
            KeyCode.RightArrow => "RIGHT ARROW",
            KeyCode.Minus => "MINUS",
            KeyCode.Equals => "EQUALS",
            KeyCode.LeftBracket => "SQUARE BRACKET OPEN",
            KeyCode.RightBracket => "SQUARE BRACKET CLOSE",
            KeyCode.Semicolon => "SEMICOLON",
            KeyCode.Quote => "QUOTE",
            KeyCode.BackQuote => "SECTION",
            KeyCode.Backslash => "BACKSLASH",
            KeyCode.Comma => "COMMA",
            KeyCode.Period => "DOT",
            KeyCode.Slash => "FORWARD SLASH",
            KeyCode.Print => "PRINT SCREEN",
            KeyCode.ScrollLock => "SCROLL LOCK",
            KeyCode.Insert => "INS",
            KeyCode.Home => "HOME",
            KeyCode.PageUp => "PAGE UP",
            KeyCode.Delete => "DELETE",
            KeyCode.End => "END",
            KeyCode.PageDown => "PAGE DOWN",
            KeyCode.Menu => "CONTEXT MENU",
            KeyCode.Mouse0 => "MOUSE1",
            KeyCode.Mouse1 => "MOUSE2",
            KeyCode.Mouse2 => "MOUSE3",
            KeyCode.Mouse3 => "MOUSE4",
            KeyCode.Mouse4 => "MOUSE5",
            _ => "",
        };
    }
    internal static bool IsExportable(KeyCode key) => ToGlobalKey(key).Length > 0;
    internal static string ToGlobalKeyOrRaw(KeyCode key) {
        string name = ToGlobalKey(key);
        return name.Length > 0 ? name : key.ToString();
    }
}
