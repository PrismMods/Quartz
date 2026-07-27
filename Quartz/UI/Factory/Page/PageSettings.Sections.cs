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
    private static void CreateLanguageSection(RectTransform content, CoreSettings defSet) {
        var langLabelRow = GenerateUI.Row(content.transform);
        var langText = GenerateUI.AddTextH1(langLabelRow);
        var langTextTr = langText.gameObject.AddComponent<TextLocalization>().Init("LANGUAGE", "Language");
        string[] langs = [.. MainCore.Tr.GetLanguages().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        var langRow = GenerateUI.Row(content.transform);
        languageDropdown = GenerateUI.DropDown(
            langRow,
            null,
            MainCore.Tr.Language,
            langs,
            lang => {
                if(lang == Translator.FALLBACK_LANGUAGE) return "DEFAULT";
                string native = MainCore.Tr.GetForLanguage("0NATIVELANG", lang, lang);
                return $"{native} ({lang})";
            },
            value => {
                MainCore.Tr.Language = value;
                MainCore.Conf.Language = value;
                MainCore.ConfMgr.RequestSave();
                TextLocalization.RefreshAll();
            },
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
            _ = Task.Run(async () => {
                await LangUpdateService.FetchAsync(MainCore.Paths.LangPath);
                await MainCore.Tr.Load(MainCore.Paths.LangPath);
                MainThread.Enqueue(() => {
                    languageDropdown.SetBlocked(false);
                    langBtn.SetBlocked(false);
                    TextLocalization.RefreshAll();
                    RefreshUpdates();
                });
            });
        };
        {
            var br = langBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(114f, 50f);
            br.offsetMax = Vector2.zero;
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
                MainCore.ConfMgr.RequestSave();
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
        GTween scaleSeq = null;
        uiScale.OnComplete = value => {
            MainCore.Conf.UIScale = value;
            MainCore.ConfMgr.RequestSave();
            scaleSeq?.Kill();
            float scaleStart = UICore.PanelScale;
            Vector2 targetSize = UICore.Panel.sizeDelta * (scaleStart / value);
            targetSize = new Vector2(
                Mathf.Clamp(targetSize.x, ResizeHandle.MIN_WIDTH / value, Screen.width / value),
                Mathf.Clamp(targetSize.y, ResizeHandle.MIN_HEIGHT / value, Screen.height / value)
            );
            UICore.LastPanelSize = targetSize;
            MainCore.Conf.PanelWidth = targetSize.x;
            MainCore.Conf.PanelHeight = targetSize.y;
            scaleSeq = GTweenSequenceBuilder.New()
                .Append(
                    GTweenExtensions.Tween(
                        () => scaleStart,
                        x => UICore.PanelScale = x,
                        value,
                        0.4f
                    ).SetEasing(Easing.OutExpo)
                )
                .Join(
                    UICore.Panel.GTSizeDelta(targetSize, 0.4f)
                        .SetEasing(Easing.OutExpo)
                )
                .Build();
            MainCore.TC.Play(scaleSeq);
        };
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
        var outlineRow = GenerateUI.Row(content.transform);
        UISlider outlineWidth = GenerateUI.Slider(
            outlineRow,
            6.25f,
            0f,
            15f,
            MainCore.Conf.OutlineWidth,
            v => Mathf.Round(v * 4f) / 4f,
            v => { MainCore.Conf.OutlineWidth = v; UICore.SetOutlineWidth(v, false); },
            v => { MainCore.Conf.OutlineWidth = v; UICore.SetOutlineWidth(v, true); MainCore.ConfMgr.RequestSave(); },
            "Outline Width",
            "outline_width"
        );
        outlineWidth.Format = "0.## px";
        outlineWidth.Rect.AddToolTip(
            "DESC_OUTLINE_WIDTH",
            "Thickness of the settings window's white outlines — the border ring, the submenu column, the top rule and the bottom pane edge."
        );
        var outlineTr = outlineWidth.Label.gameObject.AddComponent<TextLocalization>().Init("OUTLINE_WIDTH", "Outline Width");
        var accentRow = GenerateUI.Row(content.transform);
        UIColorPicker accentPicker = GenerateUI.ColorPicker(
            accentRow,
            new Color(1f, 0.6f, 0.6f, 1f),
            MainCore.Conf.GetAccentColor(),
            c => UICore.SetAccentColor(c, false),
            c => UICore.SetAccentColor(c, true),
            "Accent Color",
            "accent_color",
            false
        );
        accentPicker.Rect.AddToolTip(
            "DESC_ACCENT_COLOR",
            "Recolors the whole Quartz UI. Middle-click to reset."
        );
        var accentTr = accentPicker.Label.gameObject.AddComponent<TextLocalization>().Init("ACCENT_COLOR", "Accent Color");
    }
}
