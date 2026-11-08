#if !IL2CPP
using Facet;
using Quartz.Features.Panels;
using Quartz.UI.Nav;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Panels in the new menu: the panels' font, a button that adds one and how many there are, beside a live preview
/// of every panel. The panels themselves (their stats, looks and order) stay in the classic menu, which a card opens.
/// </summary>
internal static class Panels {
    internal static void Page(VisualElement p) {
        PanelsOverlay.EnsureConf();
        ShellPage.Feature(p, "panels", "SECTION_PANELS", "PANELS_DESC", PanelsOverlay.FontKey, Cards, EmptyKey);
    }

    // Why the preview is empty: no panels to show, or, with the panels not built (Quartz off), the general reason.
    private static string EmptyKey() => PanelsOverlay.Built && PanelsOverlay.Conf.Panels.Count == 0 ? "PANEL_NO_PANELS" : "MENU_PREVIEW_IN_LEVEL";

    private static void Cards(VisualElement p) {
        VisualElement panels = Controls.Card(p, "SECTION_PANELS");
        ShellRows.Font(panels, PanelsOverlay.FontKey);
        Controls.Action(panels, "", "PANELS_CREATE", () => {
            UICore.ClassicStale = true;   // the classic page lists the panels: it rebuilds before it next shows
            PanelsOverlay.CreatePanel();
            QuartzShell.Surface?.Refresh();   // the count follows at once
        }, o => {
            o.Id = "PANELS_CREATE";
            o.Tooltip = "DESC_PANELS_CREATE";
        });
        ShellRows.Info(panels, "PANEL_COUNT", () => PanelsOverlay.Conf.Panels.Count, CountText);

        int state = NavRegistry.StateFor("overlay.panels");
        Controls.Action(Controls.Card(p, null), "MENU_PANELS_EDITOR", "MENU_CLASSIC_OPEN", () => QuartzShell.OpenClassic(state), o => o.Id = "MENU_PANELS_EDITOR");
    }

    /// <summary>How many panels there are; past the soft cap, a warning that so many may slow the game, as the classic page says.</summary>
    internal static string CountText(int n) => string.Format(n > PanelsOverlay.PanelSoftCap
        ? QuartzHost.Text("PANEL_TOO_MANY", "{0} panels is a lot. The game may slow down.")
        : QuartzHost.Text("PANEL_COUNT", "Panels: {0}"), n);
}
#endif
