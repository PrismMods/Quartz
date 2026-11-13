#if !IL2CPP
using Facet;
using Quartz.Async;
using Quartz.Core;
using Quartz.IO;
using Quartz.Prism;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu's Settings page: the classic page's settings regrouped into cards, each row writing the field the classic
/// row writes. Its updates and fonts stay on the classic page, one click away.
/// </summary>
internal static class ShellSettings {
    private static CoreSettings C => MainCore.Conf;
    private static readonly string[] Credits = ["CREDITS_TEAM", "CREDITS_TESTERS", "CREDITS_PORTS", "CREDITS_DONORS"];

    internal static void Build(VisualElement p) {
        Controls.PageHeader(p, "SETTINGS", null, o => o.Id = "menu-header");
        Menu(Card(p, "PRISM_GROUP_MENU"));
        Appearance(Card(p, "MENU_GROUP_APPEARANCE"));
        Animation(Card(p, "HEADING_ANIMATION", o => o.VisibleWhen = () => PrismBridge.Available));   // its switch is a PrismLib setting
        Behaviour(Card(p, "MENU_GROUP_BEHAVIOUR"));
        Language(Card(p, "LANGUAGE"));
        Maintenance(Card(p, "MENU_GROUP_MAINTENANCE"));
        VisualElement experimental = Card(p, "PRISM_GROUP_EXPERIMENTAL");
        Switch(experimental, "PRISM_NEW_MENU", () => C.ExperimentalNewMenu, UICore.SetNewMenuSwitch, "PRISM_NEW_MENU_TIP");
        Controls.Action(experimental, "MENU_CLASSIC_MENU", "MENU_CLASSIC_OPEN", () => QuartzShell.OpenClassic(-1), o => o.Id = "MENU_CLASSIC_MENU");
        Controls.Action(Controls.Card(p, null, o => o.Id = "menu-settings-classic"), "MENU_SETTINGS_CLASSIC", "MENU_CLASSIC_OPEN",
            () => QuartzShell.OpenClassic(NavRegistry.StateFor(CorePages.SettingsPageKey)), o => o.Id = "MENU_SETTINGS_CLASSIC");
        VisualElement credits = Card(p, "CREDITS", o => o.Note = "CREDITS_LICENSE");
        foreach(string key in Credits) {
            string body = key + "_BODY";
            Controls.Info(credits, key, () => QuartzHost.Translate(body), o => o.Id = key);
        }
    }

    private static VisualElement Card(VisualElement p, string title, Action<CardOptions> more = null) =>
        Controls.Card(p, title, o => {
            o.Id = "menu-settings-" + title;
            more?.Invoke(o);
        });

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
            UICore.SetAccentColor(c, false);
            Save();
        }), o => {
            o.Id = "ACCENT_COLOR";
            o.Tooltip = "DESC_ACCENT_COLOR";
        });
        Control<float> scale = Controls.Slider(card, "UI_SCALE", QuartzShell.Writes(() => C.UIScale, v => {
            C.UIScale = v;
            if(History.Applying) ApplyScale(v);   // an undo or redo is no commit of the slider's
        }), o => {
            o.Id = "UI_SCALE";
            o.Min = 0.8f;
            o.Max = 1.6f;
            o.Step = 0.01f;
            o.Format = "0.00x";
        });
        // At the end of a change, as the classic slider does: rescaling every step of a drag would move the track under the pointer.
        scale.Committed += ApplyScale;
        Controls.Slider(card, "WINDOW_OPACITY", QuartzShell.Writes(() => C.PanelOpacity * 100f, v => {
            UICore.SetPanelOpacity(v / 100f, true);
            QuartzShell.ApplyLook();
        }), o => {
            o.Id = "WINDOW_OPACITY";
            o.Min = 20f;
            o.Max = 100f;
            o.Step = 1f;
            o.Format = "0'%'";
            o.Tooltip = "DESC_WINDOW_OPACITY";
        });
        // The new menu's own: the classic menu keeps its own rail.
        Controls.Dropdown(card, "MENU_SIDEBAR", Bind.To(() => QuartzShell.SidebarMode, v => QuartzShell.SidebarMode = v), o => {
            o.Id = "MENU_SIDEBAR";
            o.Items = ["MENU_SIDEBAR_TOGGLE", "MENU_SIDEBAR_SHOWN", "MENU_SIDEBAR_HIDDEN"];
            o.Tooltip = "MENU_SIDEBAR_TIP";
        });
        Switch(card, "COLOR_PICKER_HORIZONTAL", () => C.ColorPickerHorizontal, v => {
            C.ColorPickerHorizontal = v;
            Save();
            UICore.Rebuild();
        }, "DESC_COLOR_PICKER_HORIZONTAL");
    }

    // PrismLib's Smooth scrolling, shared by every Prism window, then the animation duration Prism · Settings holds.
    private static void Animation(VisualElement card) {
        Switch(card, "SMOOTH_SCROLL", () => PrismBridge.SmoothScroll, v => PrismBridge.SmoothScroll = v, "DESC_SMOOTH_SCROLL");
        Controls.Action(card, "MENU_ANIM", "MENU_OPEN_PRISM", PrismBridge.OpenPrismSettings, o => {
            o.Id = "MENU_ANIM";
            o.VisibleWhen = () => PrismBridge.Available && PrismBridge.UiAvailable;   // Prism · Settings needs PrismLib.dll as well
        });
    }

    // Saved, and both menus rescaled.
    private static void ApplyScale(float v) {
        UICore.ApplyUIScale(v, false);
        QuartzShell.ApplyLook();
    }

    // Tooltips, middle-click and the scroll speed act on the classic menu, as they always have.
    private static void Behaviour(VisualElement card) {
        Switch(card, "SHOW_TOOLTIP", () => C.Tooltip, v => {
            Tooltip.Hide();
            C.Tooltip = v;
            Save();
        }, "DESC_SHOW_TOOLTIP");
        Switch(card, "MIDDLE_CLICK_TO_SET_AS_DEFAULT", () => C.MiddleClickToDefault, v => {
            C.MiddleClickToDefault = v;
            Save();
        }, "DESC_MIDDLE_CLICK_TO_SET_AS_DEFAULT");
        Controls.Slider(card, "SCROLL_SPEED", QuartzShell.Writes(() => C.ScrollSpeed, v => {
            C.ScrollSpeed = v;
            Save();
        }), o => {
            o.Id = "SCROLL_SPEED";
            o.Min = 20f;
            o.Max = 300f;
            o.Step = 1f;
            o.Format = "0 px";
        });
    }

    private static void Language(VisualElement card) {
        string[] langs = PageSettings.Languages();
        Controls.Dropdown(card, "LANGUAGE", QuartzShell.Writes(() => Array.IndexOf(langs, MainCore.Tr.Language), i => {
            if(i >= 0 && i < langs.Length) PageSettings.ApplyLanguage(langs[i]);
        }), o => {
            o.Id = "LANGUAGE";
            o.Items = [.. langs.Select(PageSettings.LanguageName)];
        });
    }

    private static void Maintenance(VisualElement card) {
        Controls.Danger(card, "HOME_RESET_ALL", "HOME_RESET", () => Reset(false), o => {
            o.Id = "HOME_RESET_ALL";
            o.Help = "HOME_RESET_DESC";
        });
        Controls.Danger(card, "HOME_RESET_CLEAN", "HOME_RESET_CLEAN", () => Reset(true), o => o.Id = "HOME_RESET_CLEAN");
        Controls.Action(card, "MENU_RELOAD_LANG", "RELOAD", () => PageSettings.ReloadLanguages(null), o => o.Id = "MENU_RELOAD_LANG");
    }

    // As the classic Home's buttons: a pump later, and the classic menu rebuilds on what the reset left. This window, open
    // over it, takes the reset scale, opacity and input block too.
    private static void Reset(bool clean) => MainThread.Enqueue(() => {
        if(!ProfileManager.ResetLive(clean)) {
            MainCore.Log.Wrn($"[Menu] {(clean ? "clean" : "reset")} of the settings failed; see the lines above");
            return;
        }
        UICore.Rebuild(true);
        QuartzShell.ApplyLook();
        if(QuartzShell.Surface != null) QuartzShell.Surface.BlocksGameInput = C.BlockInputsWhileMenuOpen;
    });
}
#endif
