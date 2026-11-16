#if !IL2CPP
using Facet;
using Quartz.Features.Panels;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Panels in the new menu: the panels' font, a button that adds one, how many there are and the panels themselves
/// (their order, picked from that list, then the picked panel's stats and looks), beside a live preview of the picked panel.
/// A stat clicked in its list or on the preview is inspected in the settings' place.
/// </summary>
internal static class Panels {
    internal static void Page(VisualElement p) {
        PanelsOverlay.EnsureConf();
        ShellPage.Feature(p, "panels", "SECTION_PANELS", "PANELS_DESC", PanelsOverlay.FontKey, Cards, EmptyKey, PanelsStats.ClickPreview);
    }

    // Why the preview is empty: no panels to show, or, with the panels not built (Quartz off), the general reason.
    private static string EmptyKey() => PanelsOverlay.Built && PanelsOverlay.Conf.Panels.Count == 0 ? "PANEL_NO_PANELS" : "MENU_PREVIEW_IN_LEVEL";

    // While a stat is inspected its card takes the settings' place, as the Key Viewer's inspector does.
    private static void Cards(VisualElement p) {
        var (settings, inspector) = ShellRows.Swap(p, "panel-inspector-column", "PANEL_BACK", PanelsStats.Inspecting, PanelsStats.Deselect);
        VisualElement panels = Controls.Card(settings, "SECTION_PANELS");
        ShellRows.Font(panels, PanelsOverlay.FontKey);
        Controls.Action(panels, "", "PANELS_CREATE", () => {
            UICore.ClassicStale = true;   // the classic page lists the panels: it rebuilds before it next shows
            PanelsOverlay.CreatePanel();
            PanelsList.Show(PanelsOverlay.Conf.Panels[^1]);   // picked: its rows and the preview show it
            QuartzShell.Surface?.Refresh();   // the count follows at once
        }, o => {
            o.Id = "PANELS_CREATE";
            o.Tooltip = "DESC_PANELS_CREATE";
        });
        ShellRows.Info(panels, "PANEL_COUNT", () => PanelsOverlay.Conf.Panels.Count, CountText);
        PanelsList.List(panels);
        PanelsStats.InspectorColumn(inspector);
    }

    /// <summary>How many panels there are; past the soft cap, a warning that so many may slow the game, as the classic page says.</summary>
    internal static string CountText(int n) => string.Format(n > PanelsOverlay.PanelSoftCap
        ? QuartzHost.Text("PANEL_TOO_MANY", "{0} panels is a lot. The game may slow down.")
        : QuartzHost.Text("PANEL_COUNT", "Panels: {0}"), n);
}
#endif
