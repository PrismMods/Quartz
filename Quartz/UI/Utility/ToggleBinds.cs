using Quartz.Core;
#if QUARTZ_CLASSIC_UI
using Quartz.UI.Objects.Impl;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Quartz.UI.Utility;
public static class ToggleBinds {
    private static readonly List<string> pending = [];
#if QUARTZ_CLASSIC_UI
    private static readonly Dictionary<string, List<UIToggle>> live = [];
    public static void Register(string id, UIToggle toggle) {
        if(string.IsNullOrEmpty(id) || toggle == null) return;
        if(!live.TryGetValue(id, out List<UIToggle> list)) live[id] = list = [];
        list.Add(toggle);
    }
    public static void ClearLive() => live.Clear();
#else
    // No classic toggles here: a hotkey flips the new menu's row through its own bind, which applies and saves.
    // The latest build of a row wins; a bind holds no UI, so one from a detached build (the search index) works too.
    private static readonly Dictionary<string, Action> flips = [];
    internal static void Register(string id, Action flip) {
        if(!string.IsNullOrEmpty(id) && flip != null) flips[id] = flip;
    }
#endif
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
#if QUARTZ_CLASSIC_UI
    /// <summary>A value written outside the classic menu: its toggles show it without acting on it, so a hotkey flips from it.</summary>
    public static void Show(string id, bool value) {
        if(string.IsNullOrEmpty(id) || !live.TryGetValue(id, out List<UIToggle> list)) return;
        foreach(UIToggle toggle in list)
            if(toggle != null && toggle.Rect != null) toggle.Set(value, invoke: false);
    }
#endif
    public static void HandleUpdate() {
        var binds = MainCore.Conf.ToggleKeybinds;
        if(binds.Count == 0 || Keybind.Capturing || KeyCaptureCoordinator.ReleasedThisFrame || !MainCore.IsModEnabled) return;
        foreach(var kvp in binds) {
            KeyCode key = (KeyCode)kvp.Value.Key;
            if(key == KeyCode.None || !Keybind.KeyDown(key)) continue;
            if(!Keybind.ModifierHeld((Keybind.KeyModifier)kvp.Value.Modifier)) continue;
            pending.Add(kvp.Key);
        }
        if(pending.Count == 0) return;
        if(!IsTyping())
            for(int i = 0; i < pending.Count; i++) Fire(pending[i]);
        pending.Clear();
    }
    private static bool IsTyping() => Quartz.UI.Objects.UIInputBlocker.IsEditing;
#if !QUARTZ_CLASSIC_UI
    private static int builtAt = -1;
    private static void Fire(string id) {
        // A feature's own switch needs no page.
        if(id.StartsWith("feature_", StringComparison.Ordinal)) {
            string feature = id["feature_".Length..];
            if(Quartz.Features.FeatureService.Find(feature) != null)
                Quartz.Features.FeatureService.SetEnabled(feature, !Quartz.Features.FeatureService.IsEnabled(feature));
            return;
        }
        // Once per page set: a bind left for a row that is gone does not rebuild every page on each press.
        if(!flips.ContainsKey(id) && builtAt != Quartz.UI.Nav.NavRegistry.Version) {
            builtAt = Quartz.UI.Nav.NavRegistry.Version;
            Quartz.UI.FacetUi.FacetBridge.BuildAllPages();
        }
        if(flips.TryGetValue(id, out Action flip)) flip();
    }
#else
    private static void Fire(string id) {
        if(!live.TryGetValue(id, out List<UIToggle> list)) {
            // A feature's switch lives on the Features page: build that one, not every classic page.
            if(id.StartsWith("feature_", StringComparison.Ordinal)
               && Quartz.UI.Nav.NavRegistry.ByKey(Quartz.UI.Nav.CorePages.FeaturesPageKey) is { } features)
                Quartz.UI.Factory.PageFactory.EnsureBuilt(features.State);
            if(!live.TryGetValue(id, out list)) Quartz.UI.Factory.PageFactory.EnsureAllBuilt();
            if(!live.TryGetValue(id, out list)) return;
        }
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
#endif
}
