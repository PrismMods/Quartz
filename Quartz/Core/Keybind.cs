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
        KeyModifier.Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr),
        KeyModifier.Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
        KeyModifier.Cmd => AnyCmdHeld(),
        _ => false,
    };
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
