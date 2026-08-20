#nullable enable
using UnityEngine;
using VoltstroStudios.UnityWebBrowser.Shared;
namespace Quartz.Features.Minecraft;
internal static class McKeyboard {
    // CEF wants Windows virtual-key codes, so Unity's KeyCode has to be translated.
    // Covers what Minecraft Classic actually reads: movement, jump, sneak, the hotbar
    // digits, inventory, chat and the usual editing keys.
    private static readonly (KeyCode Unity, WindowsKey Windows)[] Map = BuildMap();
    private static (KeyCode, WindowsKey)[] BuildMap() {
        List<(KeyCode, WindowsKey)> map = [];
        for(int i = 0; i < 26; i++)
            map.Add(((KeyCode)((int)KeyCode.A + i), (WindowsKey)((int)WindowsKey.A + i)));
        for(int i = 0; i < 10; i++)
            map.Add(((KeyCode)((int)KeyCode.Alpha0 + i), (WindowsKey)((int)WindowsKey.D0 + i)));
        for(int i = 0; i < 12; i++)
            map.Add(((KeyCode)((int)KeyCode.F1 + i), (WindowsKey)((int)WindowsKey.F1 + i)));
        map.AddRange([
            (KeyCode.Space, WindowsKey.Space),
            (KeyCode.LeftShift, WindowsKey.LShiftKey),
            (KeyCode.RightShift, WindowsKey.RShiftKey),
            (KeyCode.LeftControl, WindowsKey.LControlKey),
            (KeyCode.RightControl, WindowsKey.RControlKey),
            (KeyCode.LeftAlt, WindowsKey.LMenu),
            (KeyCode.RightAlt, WindowsKey.RMenu),
            (KeyCode.Tab, WindowsKey.Tab),
            (KeyCode.Return, WindowsKey.Return),
            (KeyCode.KeypadEnter, WindowsKey.Return),
            (KeyCode.Backspace, WindowsKey.Back),
            (KeyCode.Delete, WindowsKey.Delete),
            (KeyCode.Escape, WindowsKey.Escape),
            (KeyCode.UpArrow, WindowsKey.Up),
            (KeyCode.DownArrow, WindowsKey.Down),
            (KeyCode.LeftArrow, WindowsKey.Left),
            (KeyCode.RightArrow, WindowsKey.Right),
            (KeyCode.Home, WindowsKey.Home),
            (KeyCode.End, WindowsKey.End),
            (KeyCode.PageUp, WindowsKey.Prior),
            (KeyCode.PageDown, WindowsKey.Next),
            (KeyCode.Insert, WindowsKey.Insert),
            (KeyCode.Minus, WindowsKey.OemMinus),
            (KeyCode.Equals, WindowsKey.Oemplus),
            (KeyCode.Comma, WindowsKey.Oemcomma),
            (KeyCode.Period, WindowsKey.OemPeriod),
            (KeyCode.Slash, WindowsKey.OemQuestion),
            (KeyCode.Semicolon, WindowsKey.OemSemicolon),
            (KeyCode.Quote, WindowsKey.OemQuotes),
            (KeyCode.LeftBracket, WindowsKey.OemOpenBrackets),
            (KeyCode.RightBracket, WindowsKey.Oem6),
            (KeyCode.Backslash, WindowsKey.OemPipe),
            (KeyCode.BackQuote, WindowsKey.Oemtilde),
        ]);
        return [.. map];
    }
    public static bool Collect(List<WindowsKey> down, List<WindowsKey> up) {
        down.Clear();
        up.Clear();
        for(int i = 0; i < Map.Length; i++) {
            (KeyCode unity, WindowsKey windows) = Map[i];
            if(Input.GetKeyDown(unity)) down.Add(windows);
            if(Input.GetKeyUp(unity)) up.Add(windows);
        }
        return down.Count > 0 || up.Count > 0;
    }
}
