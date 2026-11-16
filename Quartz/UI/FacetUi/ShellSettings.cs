#if !IL2CPP
using Facet;
using Quartz.Async;
using Quartz.Core;
using Quartz.IO;
using Quartz.Prism;
using Quartz.Localization;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The menu's Settings page: Quartz's own settings in cards.
/// </summary>
internal static partial class ShellSettings {
    private static CoreSettings C => MainCore.Conf;
    private static readonly string[] Credits = ["CREDITS_TEAM", "CREDITS_TESTERS", "CREDITS_PORTS", "CREDITS_DONORS"];

    internal static void Build(VisualElement p) {
        Controls.PageHeader(p, "SETTINGS", null, o => o.Id = "menu-header");
        Language(Card(p, "LANGUAGE"));   // first: a player who cannot read the rest finds it at once
        Menu(Card(p, "PRISM_GROUP_MENU"));
        Appearance(Card(p, "MENU_GROUP_APPEARANCE"));
        FontCards(p);
        Animation(Card(p, "HEADING_ANIMATION", o => o.VisibleWhen = () => PrismBridge.Available));   // its switch is a PrismLib setting
        Behaviour(Card(p, "MENU_GROUP_BEHAVIOUR"));
        Maintenance(Card(p, "MENU_GROUP_MAINTENANCE"));
        UpdatesCard(p);
        VisualElement credits = Card(p, "CREDITS", o => o.Note = "CREDITS_LICENSE");
        foreach(string key in Credits) {
            string body = key + "_BODY";
            Controls.Info(credits, key, () => QuartzHost.Translate(body), o => o.Id = key);
        }
    }

    private static VisualElement Card(VisualElement p, string title, Action<CardOptions> more = null) => QuartzShell.SettingsCard(p, title, more);

    private static void Save() => MainCore.ConfMgr.RequestSave();

    private static void Switch(VisualElement card, string key, Func<bool> get, Action<bool> set, string tooltip = null) =>
        Controls.Toggle(card, key, QuartzShell.Writes(get, set), o => {
            o.Id = key;
            o.Tooltip = tooltip;
        });

    private static void Menu(VisualElement card) {
        Controls.Key(card, "PRISM_MENU_HOTKEY", QuartzShell.Writes(() => ((KeyCode)C.ToggleKey).ToString(), SetKey), o => {
            o.Id = "PRISM_MENU_HOTKEY";
            o.Capture = done => KeyCaptureService.Start(k => done(k == KeyCode.None ? null : k.ToString()), null);
            o.CancelCapture = KeyCaptureService.Cancel;
        });
        Controls.Dropdown(card, "PRISM_MENU_MODIFIER", QuartzShell.Writes(() => C.ToggleModifier, m => UICore.TrySetMenuHotkey(m, C.ToggleKey)), o => {
            o.Id = "PRISM_MENU_MODIFIER";
            o.Items = ["PRISM_MODIFIER_NONE", .. new[] { Keybind.KeyModifier.Ctrl, Keybind.KeyModifier.Alt, Keybind.KeyModifier.Shift, Keybind.KeyModifier.Cmd }
                .Select(Keybind.ModifierName)];
        });
        Switch(card, "SHOW_OVERLAYER_PANEL_AT_STARTUP", () => C.ShowOnStartup, v => {
            C.ShowOnStartup = v;
            Save();
        });
        Switch(card, "BLOCK_INPUTS_WHILE_MENU_OPEN", () => C.BlockInputsWhileMenuOpen, v => {
            C.BlockInputsWhileMenuOpen = v;
            Save();
            if(QuartzShell.Surface != null) QuartzShell.Surface.BlocksGameInput = v;   // this window too, now
        });
    }

    // A capture cancelled (Escape, or another row took it) hands back no name: nothing to write.
    private static void SetKey(string name) {
        if(string.IsNullOrEmpty(name)) return;
        UICore.TrySetMenuHotkey(C.ToggleModifier, Enum.TryParse(name, out KeyCode key) ? (int)key : 0);
    }

    private static void Appearance(VisualElement card) {
        Controls.Colour(card, "ACCENT_COLOR", QuartzShell.Writes(() => C.GetAccentColor(), c => {
            C.SetAccentColor(c);
            UIColors.ApplyAccent(C.GetAccentColor());   // every Facet and Prism window recolours
            Save();
        }), o => {
            o.Id = "ACCENT_COLOR";
            o.Tooltip = "DESC_ACCENT_COLOR";
        });
        Control<float> scale = Controls.Slider(card, "UI_SCALE", QuartzShell.Writes(() => C.UIScale, v => {
            C.UIScale = v;
            // An undo or redo applies here: it raises the slider's commit only while the row is on a panel, not with its page switched away.
            if(History.Applying) ApplyScale(v);
        }), o => {
            o.Id = "UI_SCALE";
            o.Min = 0.8f;
            o.Max = 1.6f;
            o.Step = 0.01f;
            o.Format = "0.00x";
        });
        // At the end of a change, as the classic slider does: rescaling every step of a drag would move the track under the pointer.
        scale.Committed += v => { if(!History.Applying) ApplyScale(v); };   // an undo applied it as it wrote
        Controls.Slider(card, "WINDOW_OPACITY", QuartzShell.Writes(() => C.PanelOpacity * 100f, v => {
            C.PanelOpacity = Mathf.Clamp01(v / 100f);
            Save();
            QuartzShell.ApplyLook();
        }), o => {
            o.Id = "WINDOW_OPACITY";
            o.Min = 20f;
            o.Max = 100f;
            o.Step = 1f;
            o.Format = "0'%'";
            o.Tooltip = "DESC_WINDOW_OPACITY";
        });
        QuartzShell.SidebarRow(card);
    }

    // PrismLib's Smooth scrolling, shared by every Prism window, then the animation duration Prism · Settings holds.
    private static void Animation(VisualElement card) {
        QuartzShell.SmoothRow(card, QuartzShell.Writes(() => PrismBridge.SmoothScroll, v => PrismBridge.SmoothScroll = v));
        // Prism · Settings needs PrismLib.dll as well
        QuartzShell.PrismLinkRow(card, PrismBridge.OpenPrismSettings, () => PrismBridge.Available && PrismBridge.UiAvailable);
    }

    // Saved, and the window rescaled.
    private static void ApplyScale(float v) {
        C.UIScale = v;
        Save();
        QuartzShell.ApplyLook();
    }

    private static void Behaviour(VisualElement card) {
        QuartzShell.ScrollSpeedRow(card, QuartzShell.Writes(() => C.ScrollSpeed, v => {
            C.ScrollSpeed = v;
            Save();
        }));
    }

    private static void Language(VisualElement card) {
        string[] langs = LanguageActions.Languages();
        Controls.Dropdown(card, "LANGUAGE", QuartzShell.Writes(() => Array.IndexOf(langs, MainCore.Tr.Language), i => {
            if(i >= 0 && i < langs.Length) LanguageActions.Apply(langs[i]);
        }), o => {
            o.Id = "LANGUAGE";
            o.Items = [.. langs.Select(LanguageActions.LanguageName)];
        });
    }

    private static void Maintenance(VisualElement card) {
        Button reset = Controls.Danger(card, "HOME_RESET_ALL", "HOME_RESET", () => Reset(false), o => o.Id = "HOME_RESET_ALL");
        ShellRows.Describe(reset.parent.parent, "HOME_RESET_DESC");
        Controls.Danger(card, "HOME_RESET_CLEAN", "HOME_RESET_CLEAN", () => Reset(true), o => o.Id = "HOME_RESET_CLEAN");
        Controls.Action(card, "MENU_RELOAD_LANG", "RELOAD", () => LanguageActions.Reload(null), o => o.Id = "MENU_RELOAD_LANG");
    }

    // A pump later; this window takes the reset scale, opacity and input block.
    private static void Reset(bool clean) => MainThread.Enqueue(() => {
        if(!ProfileManager.ResetLive(clean)) {
            MainCore.Log.Wrn($"[Menu] {(clean ? "clean" : "reset")} of the settings failed; see the lines above");
            return;
        }
        QuartzShell.ApplyLook();
        if(QuartzShell.Surface != null) QuartzShell.Surface.BlocksGameInput = C.BlockInputsWhileMenuOpen;
    });
}
#endif
