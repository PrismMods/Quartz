using Quartz.Core;
using Quartz.Features;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static bool KeyHeld(KeyCode key) {
        if(key == KeyCode.None) return false;
        try {
            if(Input.GetKey(key)) return true;
            KeyCode twin = NumpadNavTwin(key);
            if(twin != KeyCode.None && Input.GetKey(twin)) return true;
        } catch {
            return false;
        }
        return IsHookFallbackKey(key) && KeyLimiter.KeyLimiter.HookKeyHeld(key);
    }
    private static bool IsHookFallbackKey(KeyCode key)
        => key is KeyCode.RightAlt or KeyCode.RightControl;
    private static KeyCode NumpadNavTwin(KeyCode key) => key switch {
        KeyCode.KeypadEnter => KeyCode.Return,
        KeyCode.Keypad0 => KeyCode.Insert,
        KeyCode.Keypad1 => KeyCode.End,
        KeyCode.Keypad2 => KeyCode.DownArrow,
        KeyCode.Keypad3 => KeyCode.PageDown,
        KeyCode.Keypad4 => KeyCode.LeftArrow,
        KeyCode.Keypad5 => KeyCode.Clear,
        KeyCode.Keypad6 => KeyCode.RightArrow,
        KeyCode.Keypad7 => KeyCode.Home,
        KeyCode.Keypad8 => KeyCode.UpArrow,
        KeyCode.Keypad9 => KeyCode.PageUp,
        KeyCode.KeypadPeriod => KeyCode.Delete,
        _ => KeyCode.None,
    };
    private static void ResetInputState(float now, bool clearTransientStats) {
        foreach(Box box in boxes) {
            bool wasPressed = box.Pressed;
            bool changed = box.Pressed || box.RawPressed || box.GhostPressed || box.DisplayTargetPressed
                || box.DelayedNotePending || box.LastRain != null || box.LastGhostRain != null;
            if(box.LastRain != null) {
                box.LastRain.EndTime = now;
                box.LastRain = null;
            }
            if(box.LastGhostRain != null) {
                box.LastGhostRain.EndTime = now;
                box.LastGhostRain = null;
            }
            box.Pressed = false;
            box.RawPressed = false;
            box.GhostPressed = false;
            box.DisplayTargetPressed = false;
            box.DisplayTargetTime = now;
            box.DelayedNotePending = false;
            box.DelayedReleasedBeforeStart = false;
            box.DelayedDownTime = 0f;
            box.DelayedStartTime = 0f;
            box.DelayedReleaseTime = -1f;
            if(changed) ApplyBoxColors(box);
            if(wasPressed) RaisePressChanged(box);
        }
        if(clearTransientStats) {
            pressLog.Clear();
            kpsMax = 0;
            kpsSum = 0;
            kpsSamples = 0;
            nextKpsSample = 0f;
        }
    }
    private static void PrimeInputState(float now) {
        foreach(Box box in boxes) {
            if(box.IsStat) continue;
            bool wasPressed = box.Pressed;
            bool pressed = KeyHeld(box.Key);
            bool ghostPressed = false;
            if(box.Dm != null) {
                int limiterMode = Mathf.Clamp(Conf.DmOutOfLimiterMode, 0, 2);
                bool blocked = box.Key != KeyCode.None && pressed && KeyLimiter.KeyLimiter.ShouldBlockKey(box.Key);
                bool hidden = blocked && limiterMode == 0;
                bool rainOnly = blocked && limiterMode == 1;
                bool physicalPressed = pressed && !hidden && !rainOnly;
                ghostPressed = (rainOnly || KeyHeld(box.Dm.GhostKeyCode)) && !hidden;
                box.RawPressed = physicalPressed;
                box.Pressed = physicalPressed;
                box.DisplayTargetPressed = physicalPressed;
                box.DisplayTargetTime = now;
                box.GhostPressed = ghostPressed;
                box.DelayedNotePending = false;
                box.DelayedReleasedBeforeStart = false;
                box.DelayedReleaseTime = -1f;
                ApplyBoxColors(box);
                if(wasPressed != box.Pressed) RaisePressChanged(box);
                continue;
            }
            if(Conf.RainEnabled && box.RainGroup != 0 && box.GhostKey != KeyCode.None)
                ghostPressed = KeyHeld(box.GhostKey);
            box.Pressed = pressed;
            box.GhostPressed = ghostPressed;
            ApplyBoxColors(box);
            if(wasPressed != box.Pressed) RaisePressChanged(box);
        }
    }
    private static bool InputReady(float now) {
        if(!inputWasActive) {
            inputWasActive = true;
            inputPrimed = false;
        }
        if(inputPrimed) return true;
        PrimeInputState(now);
        inputPrimed = true;
        return false;
    }
    private static void MarkInputInactive(float now, bool clearTransientStats) {
        if(inputWasActive || inputPrimed) ResetInputState(now, clearTransientStats);
        inputWasActive = false;
        inputPrimed = false;
    }
}
