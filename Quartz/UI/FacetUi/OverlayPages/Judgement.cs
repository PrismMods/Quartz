#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.InGameOverlay;
using Quartz.Features.Interop;
using Quartz.Features.Judgement;
using Quartz.IO;
using Quartz.Overlay;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Judgement in the new menu, row for row as PageJudgement, beside a live preview of the counts. The popup font
/// rows set the game's own judgement popup, kept in Quartz's core settings, as the classic section does.
/// </summary>
internal static class Judgement {
    // Read on every call: a profile switch may replace what JudgementOverlay.Conf returns.
    private static JudgementSettings C => JudgementOverlay.Conf;
    private static readonly JudgementSettings Def = new();
    private static readonly CoreSettings CoreDef = new();

    internal static void Page(VisualElement p) {
        JudgementOverlay.EnsureConf();
        ShellPage.Feature(p, "judgement", "SECTION_JUDGEMENT", "JUDGEMENT_DESC", JudgementOverlay.FontKey, Cards);
    }

    private static void Cards(VisualElement p) {
        Action apply = JudgementOverlay.Apply, save = JudgementOverlay.Save;
        VisualElement counts = Controls.Card(p, "SECTION_JUDGEMENT");
        if(XPerfectBridge.Installed)
            ShellRows.Toggle(counts, "JUDGEMENT_XPERFECT", "judgement_xperfect", Bind.To(() => C.ShowXPerfect, v => C.ShowXPerfect = v), apply, save, o => {
                o.Default = Def.ShowXPerfect;
                o.Tooltip = "DESC_JUDGEMENT_XPERFECT";
            });
        ShellRows.Font(counts, JudgementOverlay.FontKey);
        ShellRows.Slider(counts, "JUDGEMENT_SIZE", Bind.To(() => C.Size, v => C.Size = v), 0.3f, 3f, 0.01f, "0.00 x", apply, save,
            o => o.Default = Def.Size);
        ShellRows.Slider(counts, "JUDGEMENT_SPACING", Bind.To(() => C.Spacing, v => C.Spacing = v), -20f, 80f, 1f, "0 px", apply, save,
            o => o.Default = Def.Spacing);
        ShellRows.Shadow(counts, "JUDGEMENT_SHADOW_ENABLED", "JUDGEMENT_SHADOW_X", "JUDGEMENT_SHADOW_Y", "JUDGEMENT_SHADOW_SOFTNESS",
            "JUDGEMENT_SHADOW_COLOR", Bind.To(() => C.TextShadowEnabled, v => C.TextShadowEnabled = v),
            Bind.To(() => C.TextShadowX, v => C.TextShadowX = v), Bind.To(() => C.TextShadowY, v => C.TextShadowY = v),
            Bind.To(() => C.TextShadowSoftness, v => C.TextShadowSoftness = v), () => C.GetTextShadowColor(), c => C.SetTextShadowColor(c),
            (Def.TextShadowEnabled, Def.TextShadowX, Def.TextShadowY, Def.TextShadowSoftness, Def.GetTextShadowColor()), 20f, apply, save);

        // The classic section's header switch: the group's own, with no hotkey there either.
        VisualElement popup = Controls.ToggleGroup(Controls.Card(p, "SECTION_JUDGEMENT_POPUP_FONT"), "SECTION_JUDGEMENT_POPUP_FONT",
            QuartzShell.Writes(() => MainCore.Conf.FontJudgement, v => {
                MainCore.Conf.FontJudgement = v;
                MainCore.ConfMgr.RequestSave();
                InGameOverlayFont.Refresh();
            }), o => {
                o.Id = "SECTION_JUDGEMENT_POPUP_FONT";
                o.Default = CoreDef.FontJudgement;
                o.Tooltip = "DESC_FONT_JUDGEMENT";
            });
        ShellRows.Slider(popup, "FONT_JUDGEMENT_POPUP_SIZE", Bind.To(() => MainCore.Conf.FontJudgementSize, v => MainCore.Conf.FontJudgementSize = v),
            0.25f, 3f, 0.01f, "0.00 x", () => InGameOverlayFont.RefreshSizeOnly(InGameOverlayFont.Category.Judgement), () => MainCore.ConfMgr.RequestSave(),
            o => o.Default = CoreDef.FontJudgementSize);

        VisualElement layout = Controls.Card(p, "MENU_LAYOUT");
        ShellRows.Anchor(layout, "JUDGEMENT_ANCHOR_LABEL", () => (int)OverlayAnchors.Parse(C.Anchor), () => C.AnchorClosest,
            a => JudgementOverlay.SetAnchor((OverlayAnchor)a), v => {
                C.AnchorClosest = v;
                save();
            });
        ShellRows.ResetPosition(layout, "JUDGEMENT_RESETPOS", JudgementOverlay.ResetPosition);
    }
}
#endif
