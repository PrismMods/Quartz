#if !IL2CPP
using Facet;
using Quartz.Features.Overlay;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ General in the new menu, row for row as PageOverlayGeneral: the switch for every overlay, and how they slide
/// in and out. No preview: the motion draws nothing of its own.
/// </summary>
internal static class General {
    // Read on every call: a profile switch may replace what OverlaySwitch.Conf returns.
    private static OverlaySettings C => OverlaySwitch.Conf;
    private static readonly OverlaySettings Def = new();
    private static readonly OverlayPopEdge[] Edges = [OverlayPopEdge.Nearest, OverlayPopEdge.Left, OverlayPopEdge.Right, OverlayPopEdge.Top, OverlayPopEdge.Bottom];

    internal static void Page(VisualElement p) {
        OverlaySwitch.EnsureConf();
        ShellPage.Feature(p, "overlay", "MODULE_OVERLAY_CORE", "OVERLAY_GENERAL_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        // Its setter saves when the value changes.
        ShellRows.Toggle(Controls.Card(p, "OVERLAY_GENERAL"), "OVERLAY_ENABLED", "overlay_enabled",
            Bind.To(() => OverlaySwitch.Enabled, v => OverlaySwitch.Enabled = v), null, null, o => {
                o.Default = Def.Enabled;
                o.Tooltip = "DESC_OVERLAY_ENABLED";
            });
        // The classic section's header switch: the group's own, with no hotkey there either.
        VisualElement pop = Controls.ToggleGroup(Controls.Card(p, "SECTION_POP_IN_ANIMATION"), "SECTION_POP_IN_ANIMATION",
            QuartzShell.Writes(() => C.PopEnabled, v => {
                C.PopEnabled = v;
                OverlaySwitch.Save();
            }), o => {
                o.Id = "SECTION_POP_IN_ANIMATION";
                o.Default = Def.PopEnabled;
                o.Tooltip = "DESC_OVERLAY_POP";
            });
        Controls.Dropdown(pop, "OVERLAY_POP_EDGE_LABEL", QuartzShell.Writes(() => Array.IndexOf(Edges, C.PopEdge), v => {
            C.PopEdge = Edges[v];
            OverlaySwitch.Save();
        }), o => {
            o.Id = "OVERLAY_POP_EDGE_LABEL";
            o.Items = ["OVERLAY_POP_EDGE_NEAREST", "OVERLAY_POP_EDGE_LEFT", "OVERLAY_POP_EDGE_RIGHT", "OVERLAY_POP_EDGE_TOP", "OVERLAY_POP_EDGE_BOTTOM"];
            o.Default = Array.IndexOf(Edges, Def.PopEdge);
        });
        // The easings' own names, untranslated, as the classic list shows them.
        Controls.Dropdown(pop, "OVERLAY_POP_EASE_LABEL", QuartzShell.Writes(() => Array.IndexOf(OverlayMotion.Eases, C.PopEase), v => {
            C.PopEase = OverlayMotion.Eases[v];
            OverlaySwitch.Save();
        }), o => {
            o.Id = "OVERLAY_POP_EASE_LABEL";
            o.Items = [.. OverlayMotion.Eases.Select(e => e.ToString())];
            o.Default = Array.IndexOf(OverlayMotion.Eases, Def.PopEase);
        });
        ShellRows.Slider(pop, "OVERLAY_POP_SECONDS", Bind.To(() => C.PopSeconds, v => C.PopSeconds = v), OverlayMotion.MinSeconds, 2f, 0.05f, "0.00 s",
            null, OverlaySwitch.Save, o => o.Default = Def.PopSeconds);
        ShellRows.Toggle(pop, "OVERLAY_POP_EXIT", "overlay_pop_exit", Bind.To(() => C.PopExit, v => C.PopExit = v), null, OverlaySwitch.Save, o => {
            o.Default = Def.PopExit;
            o.Tooltip = "DESC_OVERLAY_POP_EXIT";
        });
    }
}
#endif
