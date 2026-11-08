#if !IL2CPP
using Facet;
using Quartz.Features.SongTitle;
using Quartz.Overlay;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Song Title in the new menu, row for row as PageSongTitle, beside a live preview of the title. The format and
/// strip rows only save: the overlay reads them every frame.
/// </summary>
internal static class SongTitle {
    // Read on every call: a profile switch may replace what SongTitleOverlay.Conf returns.
    private static SongTitleSettings C => SongTitleOverlay.Conf;
    private static readonly SongTitleSettings Def = new();

    internal static void Page(VisualElement p) {
        SongTitleOverlay.EnsureConf();
        ShellPage.Feature(p, "songtitle", "SECTION_SONG_TITLE", "SONGTITLE_DESC", SongTitleOverlay.FontKey, Cards);
    }

    private static void Cards(VisualElement p) {
        Action apply = SongTitleOverlay.Apply, save = SongTitleOverlay.Save;
        VisualElement title = Controls.Card(p, "SECTION_SONG_TITLE");
        ShellRows.Text(title, "SONGTITLE_FORMAT", Bind.To(() => C.Format, v => C.Format = v), 80, null, save, o => {
            o.Default = Def.Format;
            o.Width = 260f;
        });
        ShellRows.Toggle(title, "SONGTITLE_STRIP", "songtitle_strip", Bind.To(() => C.StripFormatting, v => C.StripFormatting = v), null, save, o => {
            o.Default = Def.StripFormatting;
            o.Tooltip = "DESC_SONGTITLE_STRIP";
        });

        VisualElement text = Controls.Card(p, "HEADING_TEXT");
        ShellRows.Font(text, SongTitleOverlay.FontKey);
        ShellRows.Slider(text, "SONGTITLE_FONTSIZE", Bind.To(() => C.FontSize, v => C.FontSize = v), 12f, 120f, 1f, "0 px", apply, save,
            o => o.Default = Def.FontSize);
        ShellRows.Slider(text, "SONGTITLE_MASTER", Bind.To(() => C.MasterSize, v => C.MasterSize = v), 0.25f, 3f, 0.01f, "0.00 x", apply, save,
            o => o.Default = Def.MasterSize);
        ShellRows.Colour(text, "SONGTITLE_COLOR", () => C.GetColor(), c => C.SetColor(c), apply, save, configure: o => o.Default = Def.GetColor());
        ShellRows.Shadow(text, "SONGTITLE_SHADOW", "SONGTITLE_SHADOW_X", "SONGTITLE_SHADOW_Y", "SONGTITLE_SHADOW_SOFT", "SONGTITLE_SHADOW_COLOR",
            Bind.To(() => C.ShadowEnabled, v => C.ShadowEnabled = v), Bind.To(() => C.ShadowX, v => C.ShadowX = v),
            Bind.To(() => C.ShadowY, v => C.ShadowY = v), Bind.To(() => C.ShadowSoftness, v => C.ShadowSoftness = v),
            () => C.GetShadowColor(), c => C.SetShadowColor(c), (Def.ShadowEnabled, Def.ShadowX, Def.ShadowY, Def.ShadowSoftness, Def.GetShadowColor()),
            10f, SongTitleOverlay.ApplyShadow, save);

        VisualElement layout = Controls.Card(p, "MENU_LAYOUT");
        ShellRows.Anchor(layout, "SONGTITLE_ANCHOR_LABEL", () => (int)OverlayAnchors.Parse(C.Anchor), () => C.AnchorClosest,
            a => SongTitleOverlay.SetAnchor((OverlayAnchor)a), v => {
                C.AnchorClosest = v;
                save();
            });
        ShellRows.ResetPosition(layout, "SONGTITLE_RESETPOS", SongTitleOverlay.ResetPosition);
    }
}
#endif
