#if !IL2CPP
using Facet;
using Quartz.Features.Combo;
using Quartz.Features.Interop;
using Quartz.Overlay;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Combo in the new menu, row for row as PageCombo, beside a live preview of the counter. Rows the overlay reads
/// every frame (the pulse, the colours) only save; the rest apply as the classic rows do.
/// </summary>
internal static class Combo {
    // Read on every call: a profile switch may replace what ComboOverlay.Conf returns.
    private static ComboSettings C => ComboOverlay.Conf;
    private static readonly ComboSettings Def = new();

    internal static void Page(VisualElement p) {
        ComboOverlay.EnsureConf();
        ShellPage.Feature(p, "combo", "SECTION_COMBO", "COMBO_DESC", ComboOverlay.FontKey, Cards);
    }

    // One card, its groups on tabs as the Key Viewer editor's inspector has them: one group shows at a time.
    private static void Cards(VisualElement p) {
        ShellRows.CardTabs(Controls.Card(p, null), new List<NavItem> {
            new("SECTION_COMBO", General), new("HEADING_LABEL", Label), new("HEADING_COUNT", Count),
            new("HEADING_ANIMATION", Animation), new("HEADING_COLOR", Colour), new("MENU_LAYOUT", Layout),
        });
        // Custom break: the judgement bar over the judgements that keep the combo; the rest break it.
        VisualElement keep = JudgementBar.Build(p, "COMBO_KEEP_TILES", Bind.To(() => C.KeepMask, v => C.KeepMask = v), ComboSettings.DefaultKeepMask,
            VisualsPages.HideJudgementsPage.Bar(XPerfectBridge.Shown, XPerfectBridge.Native), VisualsPages.HideJudgementsPage.Extra,
            ComboOverlay.Save).Root.parent;
        void Show() => keep.style.display = C.CustomBreak ? DisplayStyle.Flex : DisplayStyle.None;
        Show();
        Ui.Every(p, 100, Show);
    }

    private static void General(VisualElement combo) {
        Action apply = ComboOverlay.Apply, save = ComboOverlay.Save;
        ShellRows.Toggle(combo, "COMBO_SHOW_COUNTER", "combo_show_counter", Bind.To(() => C.ShowCounter, v => C.ShowCounter = v), apply, save, o => {
            o.Default = Def.ShowCounter;
            o.Tooltip = "DESC_COMBO_SHOW_COUNTER";
        });
        ShellRows.Toggle(combo, "COMBO_CUSTOM_BREAK", "combo_custom_break", Bind.To(() => C.CustomBreak, v => C.CustomBreak = v), null, save, o => {
            o.Default = Def.CustomBreak;
            o.Tooltip = "DESC_COMBO_CUSTOM_BREAK";
        });
        Func<bool> classic = () => !C.CustomBreak;
        ShellRows.Toggle(combo, "COMBO_AUTO", "combo_auto", Bind.To(() => C.CountAuto, v => C.CountAuto = v), null, save, o => {
            o.Default = Def.CountAuto;
            o.Tooltip = "DESC_COMBO_AUTO";
            o.VisibleWhen = classic;
        });
        if(XPerfectBridge.Installed)
            ShellRows.Toggle(combo, "COMBO_XPERFECT", "combo_xperfect", Bind.To(() => C.XPerfectComboEnabled, v => C.XPerfectComboEnabled = v), apply, save, o => {
                o.Default = Def.XPerfectComboEnabled;
                o.Tooltip = "DESC_COMBO_XPERFECT";
                o.VisibleWhen = classic;
            });
        ShellRows.Slider(combo, "COMBO_FONTSIZE", Bind.To(() => C.FontSize, v => C.FontSize = v), 24f, 120f, 1f, "0 px", apply, save,
            o => o.Default = Def.FontSize);
        ShellRows.Slider(combo, "COMBO_MASTER_SIZE", Bind.To(() => C.MasterSize, v => C.MasterSize = v), 0.25f, 3f, 0.01f, "0.00 x", apply, save,
            o => o.Default = Def.MasterSize);
        ShellRows.Font(combo, ComboOverlay.FontKey);
    }

    private static void Label(VisualElement label) {
        Action apply = ComboOverlay.Apply, save = ComboOverlay.Save;
        ShellRows.Toggle(label, "COMBO_CAPTION", "combo_caption", Bind.To(() => C.ShowCaption, v => C.ShowCaption = v), apply, save,
            o => o.Default = Def.ShowCaption);
        Func<bool> caption = () => C.ShowCaption;
        ShellRows.Text(label, "COMBO_CAPTIONTEXT", Bind.To(() => C.CaptionText, v => C.CaptionText = v), 24, apply, save, o => {
            o.Default = Def.CaptionText;
            o.VisibleWhen = caption;
        });
        ShellRows.Slider(label, "COMBO_CAPTION_SIZE", Bind.To(() => C.CaptionScale, v => C.CaptionScale = v), 0.1f, 1.5f, 0.01f, "0.00 x", apply, save,
            o => { o.Default = Def.CaptionScale; o.VisibleWhen = caption; });
        ShellRows.Slider(label, "COMBO_CAPTIONOFFSET", Bind.To(() => C.CaptionOffsetY, v => C.CaptionOffsetY = v), -200f, 200f, 1f, "0 px", apply, save,
            o => { o.Default = Def.CaptionOffsetY; o.VisibleWhen = caption; });
        ShellRows.Shadow(label, "COMBO_CAPTION_SHADOW_ENABLED", "COMBO_CAPTION_SHADOW_X", "COMBO_CAPTION_SHADOW_Y", "COMBO_CAPTION_SHADOW_SOFTNESS",
            "COMBO_CAPTION_SHADOW_COLOR", Bind.To(() => C.CaptionShadowEnabled, v => C.CaptionShadowEnabled = v),
            Bind.To(() => C.CaptionShadowX, v => C.CaptionShadowX = v), Bind.To(() => C.CaptionShadowY, v => C.CaptionShadowY = v),
            Bind.To(() => C.CaptionShadowSoftness, v => C.CaptionShadowSoftness = v), () => C.GetCaptionShadowColor(), c => C.SetCaptionShadowColor(c),
            (Def.CaptionShadowEnabled, Def.CaptionShadowX, Def.CaptionShadowY, Def.CaptionShadowSoftness, Def.GetCaptionShadowColor()), 10f,
            ComboOverlay.ApplyCaptionShadow, save);
    }

    private static void Count(VisualElement count) {
        Action apply = ComboOverlay.Apply, save = ComboOverlay.Save;
        ShellRows.Slider(count, "COMBO_COUNT_THICKNESS", Bind.To(() => C.CountThickness, v => C.CountThickness = v), -0.5f, 0.5f, 0.01f, "0.00", apply, save,
            o => o.Default = Def.CountThickness);
        ShellRows.Shadow(count, "COMBO_COUNT_SHADOW_ENABLED", "COMBO_COUNT_SHADOW_X", "COMBO_COUNT_SHADOW_Y", "COMBO_COUNT_SHADOW_SOFTNESS",
            "COMBO_COUNT_SHADOW_COLOR", Bind.To(() => C.CountShadowEnabled, v => C.CountShadowEnabled = v),
            Bind.To(() => C.CountShadowX, v => C.CountShadowX = v), Bind.To(() => C.CountShadowY, v => C.CountShadowY = v),
            Bind.To(() => C.CountShadowSoftness, v => C.CountShadowSoftness = v), () => C.GetCountShadowColor(), c => C.SetCountShadowColor(c),
            (Def.CountShadowEnabled, Def.CountShadowX, Def.CountShadowY, Def.CountShadowSoftness, Def.GetCountShadowColor()), 10f,
            ComboOverlay.ApplyCountShadow, save);
    }

    private static void Animation(VisualElement anim) {
        Action save = ComboOverlay.Save;
        ShellRows.Toggle(anim, "COMBO_NOPOP", "combo_nopop", Bind.To(() => C.NoPopAnim, v => C.NoPopAnim = v), null, save, o => o.Default = Def.NoPopAnim);
        VisualElement pulse = Controls.Advanced(anim);
        Func<bool> pops = () => !C.NoPopAnim;
        ShellRows.Slider(pulse, "COMBO_PULSE_DURATION", Bind.To(() => C.PulseDuration, v => C.PulseDuration = v), 0f, 1f, 0.01f, "0.00 s", null, save,
            o => { o.Default = Def.PulseDuration; o.VisibleWhen = pops; });
        ShellRows.Slider(pulse, "COMBO_PULSE_COUNT_SCALE", Bind.To(() => C.CountPulseScale, v => C.CountPulseScale = v), 0f, 1f, 0.01f, "0.00 x", null, save,
            o => { o.Default = Def.CountPulseScale; o.VisibleWhen = pops; });
        ShellRows.Slider(pulse, "COMBO_PULSE_LABEL_OFFSET", Bind.To(() => C.LabelPulseOffsetY, v => C.LabelPulseOffsetY = v), 0f, 60f, 1f, "0 px", null, save,
            o => { o.Default = Def.LabelPulseOffsetY; o.VisibleWhen = pops; });
    }

    private static void Colour(VisualElement colour) {
        Action save = ComboOverlay.Save;
        ShellRows.Toggle(colour, "COMBO_SOLIDCOLOR", "combo_solidcolor", Bind.To(() => C.SolidColor, v => C.SolidColor = v), null, save, o => {
            o.Default = Def.SolidColor;
            o.Tooltip = "DESC_COMBO_SOLIDCOLOR";
        });
        ShellRows.Toggle(colour, "COMBO_PERFECTCOLOR_ENABLED", "combo_perfectcolor_enabled",
            Bind.To(() => C.PerfectColorEnabled, v => C.PerfectColorEnabled = v), null, save, o => {
                o.Default = Def.PerfectColorEnabled;
                o.Tooltip = "DESC_COMBO_PERFECTCOLOR_ENABLED";
            });
        ShellRows.Colour(colour, "COMBO_COLORLOW", () => C.GetColorLow(), c => C.SetColorLow(c), null, save, configure: o => o.Default = Def.GetColorLow());
        ShellRows.Colour(colour, "COMBO_COLORHIGH", () => C.GetColorHigh(), c => C.SetColorHigh(c), null, save, configure: o => {
            o.Default = Def.GetColorHigh();
            o.VisibleWhen = () => !C.SolidColor;
        });
        ShellRows.Colour(colour, "COMBO_PERFECTCOLOR", () => C.GetPerfectColor(), c => C.SetPerfectColor(c), null, save, configure: o => {
            o.Default = Def.GetPerfectColor();
            o.VisibleWhen = () => C.PerfectColorEnabled;
        });
        ShellRows.Fit(Controls.IntSlider(colour, "COMBO_COLORMAX", QuartzShell.Writes(() => C.ColorMax, v => {
            C.ColorMax = v;
            save();
        }), o => {
            o.Id = "COMBO_COLORMAX";
            o.Min = 1;
            o.Max = 5000;
            o.Default = Def.ColorMax;
        }));
    }

    private static void Layout(VisualElement layout) {
        Action save = ComboOverlay.Save;
        ShellRows.Anchor(layout, "COMBO_ANCHOR_LABEL", () => (int)OverlayAnchors.Parse(C.Anchor), () => C.AnchorClosest,
            a => ComboOverlay.SetAnchor((OverlayAnchor)a), v => {
                C.AnchorClosest = v;
                save();
            });
        ShellRows.ResetPosition(layout, "COMBO_RESETPOS", ComboOverlay.ResetPosition);
    }
}
#endif
