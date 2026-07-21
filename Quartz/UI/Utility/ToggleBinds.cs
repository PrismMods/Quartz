using Quartz.Core;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Quartz.UI.Utility;
internal static class ToggleBinds {
    private static readonly Dictionary<string, List<UIToggle>> live = [];
    private static readonly List<string> pending = [];
    public static void Register(string id, UIToggle toggle) {
        if(string.IsNullOrEmpty(id) || toggle == null) return;
        if(!live.TryGetValue(id, out List<UIToggle> list)) live[id] = list = [];
        list.Add(toggle);
    }
    public static void ClearLive() => live.Clear();
    public static bool TryGet(string id, out Keybind.KeyModifier modifier, out KeyCode key) {
        modifier = Keybind.KeyModifier.None;
        key = KeyCode.None;
        if(string.IsNullOrEmpty(id)) return false;
        if(!MainCore.Conf.ToggleKeybinds.TryGetValue(id, out (int Modifier, int Key) bind)) return false;
        modifier = (Keybind.KeyModifier)bind.Modifier;
        key = (KeyCode)bind.Key;
        return key != KeyCode.None;
    }
    public static void Set(string id, Keybind.KeyModifier modifier, KeyCode key) {
        if(string.IsNullOrEmpty(id)) return;
        MainCore.Conf.ToggleKeybinds[id] = ((int)modifier, (int)key);
        MainCore.ConfMgr.RequestSave();
    }
    public static void Clear(string id) {
        if(string.IsNullOrEmpty(id)) return;
        if(!MainCore.Conf.ToggleKeybinds.Remove(id)) return;
        MainCore.ConfMgr.RequestSave();
    }
    public static void HandleUpdate() {
        var binds = MainCore.Conf.ToggleKeybinds;
        if(binds.Count == 0 || Keybind.Capturing) return;
        foreach(var kvp in binds) {
            KeyCode key = (KeyCode)kvp.Value.Key;
            if(key == KeyCode.None || !Input.GetKeyDown(key)) continue;
            if(!Keybind.ModifierHeld((Keybind.KeyModifier)kvp.Value.Modifier)) continue;
            pending.Add(kvp.Key);
        }
        if(pending.Count == 0) return;
        if(!IsTyping())
            for(int i = 0; i < pending.Count; i++) Fire(pending[i]);
        pending.Clear();
    }
    private static bool IsTyping() {
        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if(selected == null) return false;
        TMP_InputField field = selected.GetComponent<TMP_InputField>();
        return field != null && field.isFocused;
    }
    private static void Fire(string id) {
        if(!live.TryGetValue(id, out List<UIToggle> list)) return;
        UIToggle primary = null;
        for(int i = list.Count - 1; i >= 0; i--) {
            UIToggle toggle = list[i];
            if(toggle == null || toggle.Rect == null) {
                list.RemoveAt(i);
                continue;
            }
            primary = toggle;
        }
        if(primary == null) return;
        primary.Toggle();
        for(int i = 0; i < list.Count; i++)
            if(!ReferenceEquals(list[i], primary)) list[i].Set(primary.Value, false);
    }
}
