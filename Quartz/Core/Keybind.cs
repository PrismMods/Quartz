using System.Collections.Generic;
using UnityEngine;
namespace Quartz.Core;
public static class Keybind {
    public enum KeyModifier { None, Ctrl, Alt, Shift, Cmd }
    public static bool Capturing;
    public static bool IsMac =>
        Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
    private static readonly KeyCode[] CmdKeys = ResolveCmdKeys();
    private static KeyCode[] ResolveCmdKeys() {
        string[] names = {
            "LeftMeta", "RightMeta",
            "LeftCommand", "RightCommand",
            "LeftApple", "RightApple",
            "LeftWindows", "RightWindows",
        };
        List<KeyCode> keys = [];
        foreach(string name in names)
            if(System.Enum.TryParse(name, out KeyCode kc) && !keys.Contains(kc)) keys.Add(kc);
        return [.. keys];
    }
    private static bool AnyCmdHeld() {
        for(int i = 0; i < CmdKeys.Length; i++)
            if(Input.GetKey(CmdKeys[i])) return true;
        return false;
    }
    private static bool IsCmdKey(KeyCode key) {
        for(int i = 0; i < CmdKeys.Length; i++)
            if(CmdKeys[i] == key) return true;
        return false;
    }
    private static readonly bool[] UsableKeys = BuildUsableKeys();
    private static bool[] BuildUsableKeys() {
        KeyCode[] all = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));
        int max = 0;
        for(int i = 0; i < all.Length; i++)
            if((int)all[i] > max) max = (int)all[i];
        bool[] table = new bool[max + 1];
        for(int i = 0; i < all.Length; i++) table[(int)all[i]] = true;
        return table;
    }
    public static bool IsUsableKey(KeyCode key) {
        int index = (int)key;
        return index > 0 && index < UsableKeys.Length && UsableKeys[index];
    }
    public static bool IsModifier(KeyCode key) {
        switch(key) {
            case KeyCode.LeftControl or KeyCode.RightControl
                or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.AltGr
                or KeyCode.LeftShift or KeyCode.RightShift:
                return true;
            default:
                return IsCmdKey(key);
        }
    }
    public static bool ModifierHeld(KeyModifier mod) => mod switch {
        KeyModifier.None => true,
        KeyModifier.Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
        // On macOS also the window server's flags: the same clock KeyDown reads Option+letter from, so a chord pressed
        // in one frame (Unity can see Option a frame late) still lines up.
        KeyModifier.Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr)
            || MacHeld(KeyCode.LeftAlt) || MacHeld(KeyCode.RightAlt),
        KeyModifier.Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
        KeyModifier.Cmd => AnyCmdHeld(),
        _ => false,
    };
    // macOS: while Option is held Unity never reports a letter's keydown (Option+K composes ˚; seen in-game,
    // anyKeyDown only shows LeftAlt). Under Alt the key's edge comes from the window server instead. One source per
    // frame, so a key Unity does report is never counted twice. Callers must poll every frame to see the edge.
    private sealed class MacKey {
        public int Frame = -2;
        public bool Mapped, Held, Down;
    }
    private static readonly Dictionary<KeyCode, MacKey> macKeys = [];
    private static MacKey PollMac(KeyCode key) {
        if(!IsMac) return null;
        if(!macKeys.TryGetValue(key, out MacKey s)) macKeys[key] = s = new MacKey();
        int frame = Time.frameCount;
        if(s.Frame != frame) {
            s.Mapped = Quartz.Game.HookInput.TryMacKeyState(key, out bool held);
            // Global HID state: a press typed into another app must not reach the game.
            held &= s.Mapped && Application.isFocused;
            s.Down = held && !s.Held && s.Frame == frame - 1;
            s.Held = held;
            s.Frame = frame;
        }
        return s.Mapped ? s : null;
    }
    private static bool MacHeld(KeyCode key) =>
        IsMac && Application.isFocused && Quartz.Game.HookInput.TryMacKeyState(key, out bool held) && held;
    public static bool KeyDown(KeyCode key) {
        MacKey mac = PollMac(key);
        return mac != null && ModifierHeld(KeyModifier.Alt) ? mac.Down : Input.GetKeyDown(key);
    }
    public static bool KeyHeld(KeyCode key) {
        MacKey mac = PollMac(key);
        return Input.GetKey(key) || (mac != null && mac.Held);
    }
    public static KeyModifier HeldModifier() {
        if(ModifierHeld(KeyModifier.Ctrl)) return KeyModifier.Ctrl;
        if(ModifierHeld(KeyModifier.Cmd)) return KeyModifier.Cmd;
        if(ModifierHeld(KeyModifier.Alt)) return KeyModifier.Alt;
        if(ModifierHeld(KeyModifier.Shift)) return KeyModifier.Shift;
        return KeyModifier.None;
    }
    public static string ModifierName(KeyModifier mod) => mod switch {
        KeyModifier.Ctrl => "Ctrl",
        KeyModifier.Alt => IsMac ? "Option" : "Alt",
        KeyModifier.Shift => "Shift",
        KeyModifier.Cmd => IsMac ? "Cmd" : "Win",
        _ => "",
    };
    public static string KeyName(KeyCode key) {
        string name = key.ToString();
        if(name.Length == 6 && name.StartsWith("Alpha")) return name[5..];
        return key switch {
            KeyCode.BackQuote => "`",
            KeyCode.Return => "Enter",
            KeyCode.Escape => "Esc",
            KeyCode.Space => "Space",
            KeyCode.LeftShift => "LShift",
            KeyCode.RightShift => "RShift",
            KeyCode.LeftControl => "LCtrl",
            KeyCode.RightControl => "RCtrl",
            KeyCode.LeftAlt => "LAlt",
            KeyCode.RightAlt => "RAlt",
            KeyCode.LeftCommand => "LCmd",
            KeyCode.RightCommand => "RCmd",
            KeyCode.Backslash => "\\",
            KeyCode.Slash => "/",
            KeyCode.Minus => "-",
            KeyCode.Equals => "=",
            KeyCode.Comma => ",",
            KeyCode.Period => ".",
            KeyCode.Semicolon => ";",
            KeyCode.Quote => "'",
            KeyCode.LeftBracket => "[",
            KeyCode.RightBracket => "]",
            _ => name,
        };
    }
    public static string Format(KeyModifier mod, KeyCode key) {
        string k = KeyName(key);
        return mod == KeyModifier.None ? k : ModifierName(mod) + " + " + k;
    }
}
