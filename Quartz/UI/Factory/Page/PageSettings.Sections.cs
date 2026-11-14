using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Async;
using Quartz.Core;
using Quartz.Core.Service;
using Quartz.IO;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using Quartz.Utility;
using Quartz.Update;
using UnityEngine;
using UnityEngine.UI;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PageSettings {
    private static bool reopenAccentPicker;
    private static bool reopenAccentHsv;
    // The language actions, shared with the new menu's Settings page.
    internal static string[] Languages() => [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    internal static string LanguageName(string lang) {
        if(lang == Translator.FALLBACK_LANGUAGE) return "DEFAULT";
        string native = MainCore.Tr.GetForLanguage("0NATIVELANG", lang, lang);
        return $"{native} ({lang})";
    }
    internal static void ApplyLanguage(string lang) {
        MainCore.Tr.Language = lang;
        MainCore.Conf.Language = lang;
        MainCore.Conf.LanguageAutoDetected = true;
        MainCore.ConfMgr.RequestSave();
        TextLocalization.RefreshAll();
    }
    /// <summary>Reads the language files again; <paramref name="done"/> runs on the main thread before the labels refresh.</summary>
    internal static void ReloadLanguages(Action done) {
        _ = Task.Run(async () => {
            await MainCore.Tr.Load(MainCore.Paths.LangPath);
            MainThread.Enqueue(() => {
                done?.Invoke();
                TextLocalization.RefreshAll();
                RefreshUpdates();
            });
        });
    }
    private static void CreateLanguageSection(RectTransform content, CoreSettings defSet) {
        var langLabelRow = GenerateUI.Row(content.transform);
        var langText = GenerateUI.AddTextH1(langLabelRow);
        var langTextTr = langText.gameObject.AddComponent<TextLocalization>().Init("LANGUAGE", "Language");
        var langRow = GenerateUI.Row(content.transform);
        languageDropdown = GenerateUI.DropDown(
            langRow,
            null,
            MainCore.Tr.Language,
            Languages(),
            LanguageName,
            ApplyLanguage,
            "language_dropdown"
        );
        UIButton langBtn = GenerateUI.Button(
            langRow,
            () => { },
            "Reload",
            "language_reload"
        );
        langBtn.OnClick = async () => {
            languageDropdown.SetExpanded(false);
            languageDropdown.SetBlocked(true);
            langBtn.SetBlocked(true);
            langBtn.Label.text = "...";
            ReloadLanguages(() => {
                languageDropdown.SetBlocked(false);
                langBtn.SetBlocked(false);
            });
        };
        {
            var br = langBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(114f, 50f);
            br.anchoredPosition = Vector2.zero;
        }
        langBtn.Label.gameObject.AddComponent<TextLocalization>().Init("RELOAD", "Reload");
    }
    private static void CreateBehaviourSection(RectTransform content, CoreSettings defSet) {
        var overlayerText = GenerateUI.AddTextH1(GenerateUI.Row(content.transform));
        var overlayerTextTr = overlayerText.gameObject.AddComponent<TextLocalization>().Init("OVERLAYER", "Quartz");
        var startupRow = GenerateUI.Row(content.transform);
        UIToggle startupToggle = GenerateUI.Toggle(
            startupRow,
            defSet.ShowOnStartup,
            MainCore.Conf.ShowOnStartup,
            toggle => {
                MainCore.Conf.ShowOnStartup = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Show Quartz Settings at Startup",
            "show_on_startup"
        );
        var startupToggleTr = startupToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_OVERLAYER_PANEL_AT_STARTUP", "Show Quartz Settings at Startup");
        var blockInputsRow = GenerateUI.Row(content.transform);
        UIToggle blockInputsToggle = GenerateUI.Toggle(
            blockInputsRow,
            defSet.BlockInputsWhileMenuOpen,
            MainCore.Conf.BlockInputsWhileMenuOpen,
            toggle => {
                MainCore.Conf.BlockInputsWhileMenuOpen = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Block game inputs while menu is open",
            "block_inputs_while_menu_open"
        );
        var keybindRow = GenerateUI.Row(content.transform);
        var keybindLabel = GenerateUI.KeyBind(
            keybindRow,
            (Keybind.KeyModifier)MainCore.Conf.ToggleModifier,
            (KeyCode)MainCore.Conf.ToggleKey,
            (mod, key) => {
                MainCore.Conf.ToggleModifier = (int)mod;
                MainCore.Conf.ToggleKey = (int)key;
                MainCore.Conf.ClearRefusedSwitcherKey();
                MainCore.ConfMgr.RequestSave();
                Quartz.Prism.PrismBridge.RebindMenuKey();
            },
            "Toggle Menu Keybind",
            "toggle_keybind"
        );
        var keybindTr = keybindLabel.gameObject.AddComponent<TextLocalization>().Init("TOGGLE_KEYBIND", "Toggle Menu Keybind");
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(content.transform, 32f),
            "TOGGLE_BIND_HINT",
            "Right-click any toggle to give it its own hotkey. Press Esc while binding to unbind it."
        );
        var tooltipRow = GenerateUI.Row(content.transform);
        UIToggle tooltipToggle = GenerateUI.Toggle(
            tooltipRow,
            defSet.Tooltip,
            MainCore.Conf.Tooltip,
            toggle => {
                Tooltip.Hide();
                MainCore.Conf.Tooltip = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Show Tooltip",
            "show_tooltip"
        );
        tooltipToggle.Rect.AddToolTip(
            "DESC_SHOW_TOOLTIP",
            "Shows a description bubble when you hover over a setting. Turning this off hides all tooltips, including this one."
        );
        var tooltipToggleTr = tooltipToggle.Label.gameObject.AddComponent<TextLocalization>().Init("SHOW_TOOLTIP", "Show Tooltip");
        var middleClickRow = GenerateUI.Row(content.transform);
        UIToggle middleClickToggle = GenerateUI.Toggle(
            middleClickRow,
            defSet.MiddleClickToDefault,
            MainCore.Conf.MiddleClickToDefault,
            toggle => {
                MainCore.Conf.MiddleClickToDefault = toggle;
                MainCore.ConfMgr.RequestSave();
            },
            "Middle-click to set as default",
            "middle_click_default"
        );
        middleClickToggle.Rect.AddToolTip(
            "DESC_MIDDLE_CLICK_TO_SET_AS_DEFAULT",
            "Setting that restores an item to its default value when you middle-click on it.\nYou can identify it by a small dot at the top-left of the item"
        );
        var middleClickToggleTr = middleClickToggle.Label.gameObject.AddComponent<TextLocalization>().Init("MIDDLE_CLICK_TO_SET_AS_DEFAULT", "Middle-click to set as default");
        var pickerLayoutRow = GenerateUI.Row(content.transform);
        UIToggle pickerLayoutToggle = GenerateUI.Toggle(
            pickerLayoutRow,
            defSet.ColorPickerHorizontal,
            MainCore.Conf.ColorPickerHorizontal,
            toggle => {
                MainCore.Conf.ColorPickerHorizontal = toggle;
                MainCore.ConfMgr.RequestSave();
                UICore.Rebuild();
            },
            "Wide color picker",
            "color_picker_horizontal"
        );
        pickerLayoutToggle.Rect.AddToolTip(
            "DESC_COLOR_PICKER_HORIZONTAL",
            "Lays the color picker out side by side instead of stacked.\nUseful on short windows where the tall picker does not fit"
        );
        var pickerLayoutToggleTr = pickerLayoutToggle.Label.gameObject.AddComponent<TextLocalization>().Init("COLOR_PICKER_HORIZONTAL", "Wide color picker");
        // Only where the new menu can run; the same switch as Prism · Settings' row.
        if(Quartz.UI.FacetUi.FacetBridge.Available()) {
            UIToggle newMenuToggle = GenerateUI.Toggle(
                GenerateUI.Row(content.transform),
                defSet.ExperimentalNewMenu,
                MainCore.Conf.ExperimentalNewMenu,
                UICore.SetNewMenuSwitch,
                "Experimental: new menu",
                "prism_new_menu"
            );
            newMenuToggle.Rect.AddToolTip(
                "PRISM_NEW_MENU_TIP",
                "The Settings button the menu hotkey shows opens the new menu. It is still being built; the classic menu stays available"
            );
        }
    }
    private static void CreateAppearanceSection(RectTransform content, CoreSettings defSet) {
        static float uiScaleFilter(float v) {
            v = Mathf.Round(v * 100f) / 100f;
            return Mathf.Clamp(v, 0.8f, 1.6f);
        }
        var uiScaleRow = GenerateUI.Row(content.transform);
        UISlider uiScale = GenerateUI.Slider(
            uiScaleRow,
            1f,
            0.8f,
            1.6f,
            MainCore.Conf.UIScale,
            uiScaleFilter,
            null,
            null,
            "UI Scale",
            "ui_scale"
        );
        uiScale.Format = "0.00x";
        uiScale.OnChanged = value => MainCore.Conf.UIScale = value;
        uiScale.OnComplete = value => UICore.ApplyUIScale(value, true);
        var uiScaleTr = uiScale.Label.gameObject.AddComponent<TextLocalization>().Init("UI_SCALE", "UI Scale");
        var scrollRow = GenerateUI.Row(content.transform);
        UISlider scrollSpeed = GenerateUI.Slider(
            scrollRow,
            80f,
            20f,
            300f,
            MainCore.Conf.ScrollSpeed,
            Mathf.Round,
            v => MainCore.Conf.ScrollSpeed = v,
            v => { MainCore.Conf.ScrollSpeed = v; MainCore.ConfMgr.RequestSave(); },
            "Scroll Speed",
            "scroll_speed"
        );
        scrollSpeed.Format = "0 px";
        var scrollTr = scrollSpeed.Label.gameObject.AddComponent<TextLocalization>().Init("SCROLL_SPEED", "Scroll Speed");
        // PrismLib's setting, shared by every Prism window: only where PrismLib is.
        if(Quartz.Prism.PrismBridge.Available) {
            UIToggle smoothToggle = GenerateUI.Toggle(
                GenerateUI.Row(content.transform),
                true,
                Quartz.Prism.PrismBridge.SmoothScroll,
                v => Quartz.Prism.PrismBridge.SmoothScroll = v,
                "Smooth Scrolling",
                "smooth_scroll"
            );
            smoothToggle.Rect.AddToolTip(
                "DESC_SMOOTH_SCROLL",
                "Glides every Prism window to where the mouse wheel sends it instead of jumping. The same setting as in Prism · Settings."
            );
        }
        var opacityRow = GenerateUI.Row(content.transform);
        UISlider opacity = GenerateUI.Slider(
            opacityRow,
            100f,
            20f,
            100f,
            MainCore.Conf.PanelOpacity * 100f,
            Mathf.Round,
            v => UICore.SetPanelOpacity(v / 100f, false),
            v => UICore.SetPanelOpacity(v / 100f, true),
            "Window Opacity",
            "window_opacity"
        );
        opacity.Format = "0'%'";
        opacity.Rect.AddToolTip(
            "DESC_WINDOW_OPACITY",
            "Transparency of the settings window."
        );
        var opacityTr = opacity.Label.gameObject.AddComponent<TextLocalization>().Init("WINDOW_OPACITY", "Window Opacity");
        var accentRow = GenerateUI.Row(content.transform);
        UIColorPicker accentPicker = null;
        accentPicker = GenerateUI.ColorPicker(
            accentRow,
            new Color(1f, 0.6f, 0.6f, 1f),
            MainCore.Conf.GetAccentColor(),
            c => UICore.SetAccentColor(c, false),
            c => {
                // The save rebuilds the UI, which would close the picker mid-edit: reopen it after.
                reopenAccentPicker = accentPicker != null && accentPicker.Expanded;
                reopenAccentHsv = accentPicker != null && accentPicker.HsvMode;
                UICore.SetAccentColor(c, true);
            },
            "Accent Color",
            "accent_color",
            false
        );
        accentPicker.Rect.AddToolTip(
            "DESC_ACCENT_COLOR",
            "Recolors the whole Quartz UI. Middle-click to reset."
        );
        if(reopenAccentPicker) {
            reopenAccentPicker = false;
            accentPicker.SetMode(reopenAccentHsv);
            accentPicker.SetExpanded(true, true);
        }
        var accentTr = accentPicker.Label.gameObject.AddComponent<TextLocalization>().Init("ACCENT_COLOR", "Accent Color");
    }
}
