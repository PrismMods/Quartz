#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Overlay;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// The panels in Overlay ▸ Panels, in their drawing order: a list dragged by a panel's name, each panel's rows in a shut
/// section under it, as the classic page's sections. Every row's Id is its Lang key and #n, n the panel's place; a move
/// renumbers them, so the moved panels' rows are built again. Panel names are the player's: the list is read only while the
/// page is on a panel, so the search index's detached build has none.
/// </summary>
internal static class PanelsList {
    private static List<PanelConfig> Panels => PanelsOverlay.Conf.Panels;
    private static readonly PanelConfig Def = new();
    private static readonly IReadOnlyList<PanelConfig> NoPanels = [];
    private static readonly LabelSeparatorSpacing[] Spacings = [LabelSeparatorSpacing.Both, LabelSeparatorSpacing.After, LabelSeparatorSpacing.None];

    /// <summary>"No panels" while there are none, then the list.</summary>
    internal static void List(VisualElement card) {
        Controls.Info(card, "PANEL_NO_PANELS", () => "", (InfoOptions o) => {
            o.Id = "PANEL_NO_PANELS";
            o.VisibleWhen = () => Panels.Count == 0;
        });
        Controls.Reorder<PanelConfig>(card, o => {
            o.Id = "PANELS_LIST";
            o.Vertical = true;
            o.DragFromText = true;
            o.Tooltip = "DESC_PANEL_LAYER";
            o.Items = () => card.panel == null ? NoPanels : Panels;
            o.Key = p => ShellRows.ItemKey(p, Panels.IndexOf(p));
            o.Text = p => p.Name;
            o.Move = Move;
            o.Build = (e, p) => Build(e, p, Panels.IndexOf(p) + 1);
        });
    }

    /// <summary>A drop: the panel at from goes to to (remove at from, insert at to), as the classic page's drag commits.</summary>
    internal static void Move(int from, int to) {
        List<PanelConfig> before = [.. Panels];
        PanelConfig p = Panels[from];
        Panels.RemoveAt(from);
        Panels.Insert(to, p);
        Moved(before);
    }

    // As the classic page: each panel's hotkeys follow it from its place in before to its place now (a deleted panel's go with
    // it), then the list saves and the overlay is built again in the new order.
    private static void Moved(List<PanelConfig> before) {
        if(PanelBinds.Moved(MainCore.Conf.ToggleKeybinds, [.. before.Select(p => Panels.IndexOf(p))])) MainCore.ConfMgr.RequestSave();
        PanelsOverlay.Save();
        PanelsOverlay.Rebuild();
        UICore.ClassicStale = true;   // the classic page lists the panels
        QuartzShell.Surface?.Refresh();
    }

    /// <summary>Panel n's rows (n = its place + 1), in a shut section titled with its name; every row's steps go stale with the
    /// panel.</summary>
    internal static void Build(VisualElement box, PanelConfig panel, int n) {
        string classic = "panel" + (n - 1);   // the classic rows' ids, which their toggles' hotkeys are keyed by
        Action save = PanelsOverlay.Save, apply = PanelsOverlay.Apply;
        Func<object> document = ShellRows.ItemDocument(panel, () => Panels);
        void Row(ControlOptions o, string key) {
            o.Id = key + "#" + n;
            o.UndoDocument = document;
        }
        VisualElement body = Controls.Section(box, panel.Name, o => {
            o.Open = false;
            o.Id = "PANEL#" + n;
        });
        Label title = Literal(box.Q(name: "PANEL#" + n), panel.Name);
        ShellRows.Text(body, "PANEL_NAME", Bind.To(() => panel.Name, v => {
            panel.Name = string.IsNullOrWhiteSpace(v) ? "Panel" : v;
            if(title != null) title.text = panel.Name;
        }), 24, null, save, o => {
            Row(o, "PANEL_NAME");
            o.Tooltip = "DESC_PANEL_NAME";
        });

        PanelsStats.Build(Controls.Section(body, "HEADING_STATS", o => o.Id = "HEADING_STATS#" + n), panel, n);

        VisualElement look = Controls.Section(body, "HEADING_APPEARANCE", o => o.Id = "HEADING_APPEARANCE#" + n);
        ShellRows.Anchor(look, "PANEL_ANCHOR_LABEL", () => (int)OverlayAnchors.Parse(panel.Anchor), () => panel.AnchorClosest,
            a => PanelsOverlay.SetAnchor(panel, (PanelAnchor)a), v => {
                panel.AnchorClosest = v;
                save();
            }, o => Row(o, "PANEL_ANCHOR_LABEL"));
        ShellRows.Text(look, "PANEL_PREFIX", Bind.To(() => panel.Prefix, v => panel.Prefix = v), 32, null, save, o => {
            Row(o, "PANEL_PREFIX");
            o.Default = Def.Prefix;
            o.Tooltip = "DESC_PANEL_PREFIX";
        });
        ShellRows.Text(look, "PANEL_SEPARATOR", Bind.To(() => panel.LabelSeparator, v => panel.LabelSeparator = v), 8, null, save, o => {
            Row(o, "PANEL_SEPARATOR");
            o.Default = Def.LabelSeparator;
        });
        Controls.Dropdown(look, "PANEL_SEPARATORSPACING_LABEL", QuartzShell.Writes(() => panel.LabelSeparatorSpacing, v => {
            panel.LabelSeparatorSpacing = v;
            save();
        }), Spacings, s => s switch {
            LabelSeparatorSpacing.After => "PANEL_SEPARATORSPACING_AFTER",
            LabelSeparatorSpacing.None => "PANEL_SEPARATORSPACING_NONE",
            _ => "PANEL_SEPARATORSPACING_BOTH",
        }, o => {
            Row(o, "PANEL_SEPARATORSPACING_LABEL");
            o.Default = Def.LabelSeparatorSpacing;
            o.Tooltip = "DESC_PANEL_SEPARATORSPACING";
        });
        ShellRows.ReleaseSlider(look, "PANEL_FONTSIZE", Bind.To(() => panel.FontSize, v => panel.FontSize = Mathf.Clamp(Mathf.Round(v), 12f, 48f)),
            12f, 48f, 1f, "0 px", apply, save, o => {
                Row(o, "PANEL_FONTSIZE");
                o.Default = Def.FontSize;
            });
        ShellRows.ReleaseSlider(look, "PANEL_LINESPACING", Bind.To(() => panel.LineSpacing, v => panel.LineSpacing = Mathf.Clamp(Mathf.Round(v * 2f) * 0.5f, -50f, 50f)),
            -50f, 50f, 0.5f, "0.#", apply, save, o => {
                Row(o, "PANEL_LINESPACING");
                o.Default = Def.LineSpacing;
            });
        // Written every frame without an apply, saved on release, as the classic slider: the overlay reads it as it draws.
        ShellRows.ReleaseSlider(look, "PANEL_DECIMALS", Bind.To(() => (float)panel.Decimals, v => panel.Decimals = (int)Mathf.Round(v)),
            0f, 6f, 1f, "0", null, save, o => {
                Row(o, "PANEL_DECIMALS");
                o.Default = Def.Decimals;
            });
        ShellRows.Colour(look, "PANEL_TEXTCOLOR", panel.GetTextColor, panel.SetTextColor, apply, save, configure: o => {
            Row(o, "PANEL_TEXTCOLOR");
            o.Default = Def.GetTextColor();
        });
        ShellRows.Toggle(look, "PANEL_BACKGROUND", classic + "_background", Bind.To(() => panel.BackgroundEnabled, v => panel.BackgroundEnabled = v), apply, save, o => {
            Row(o, "PANEL_BACKGROUND");
            o.Default = Def.BackgroundEnabled;
        });
        ShellRows.Colour(look, "PANEL_BGCOLOR", panel.GetBackgroundColor, panel.SetBackgroundColor, apply, save, configure: o => {
            Row(o, "PANEL_BGCOLOR");
            o.Default = Def.GetBackgroundColor();
            o.Tooltip = "DESC_PANEL_BGCOLOR";
        });
        ShellRows.Toggle(look, "PANEL_LOCALIZESTATS", classic + "_localizestats", Bind.To(() => panel.LocalizeStatLabels, v => panel.LocalizeStatLabels = v), apply, save,
            o => {
                Row(o, "PANEL_LOCALIZESTATS");
                o.Default = Def.LocalizeStatLabels;
                o.Tooltip = "DESC_PANEL_LOCALIZESTATS";
            });

        VisualElement shadow = Controls.Section(body, "HEADING_SHADOW", o => o.Id = "HEADING_SHADOW#" + n);
        ShellRows.Toggle(shadow, "PANEL_TEXTSHADOW", classic + "_textshadow", Bind.To(() => panel.TextShadowEnabled, v => panel.TextShadowEnabled = v), apply, save, o => {
            Row(o, "PANEL_TEXTSHADOW");
            o.Default = Def.TextShadowEnabled;
        });
        ShellRows.ReleaseSlider(shadow, "PANEL_SHADOW_X", Bind.To(() => panel.TextShadowX, v => panel.TextShadowX = v), -20f, 20f, 0.1f, "0.0 px", apply, save, o => {
            Row(o, "PANEL_SHADOW_X");
            o.Default = Def.TextShadowX;
        });
        ShellRows.ReleaseSlider(shadow, "PANEL_SHADOW_Y", Bind.To(() => panel.TextShadowY, v => panel.TextShadowY = v), -20f, 20f, 0.1f, "0.0 px", apply, save, o => {
            Row(o, "PANEL_SHADOW_Y");
            o.Default = Def.TextShadowY;
        });
        ShellRows.ReleaseSlider(shadow, "PANEL_SHADOW_SOFTNESS", Bind.To(() => panel.TextShadowSoftness, v => panel.TextShadowSoftness = v), 0f, 20f, 0.1f, "0.0 px",
            apply, save, o => {
                Row(o, "PANEL_SHADOW_SOFTNESS");
                o.Default = Def.TextShadowSoftness;
            });
        ShellRows.Colour(shadow, "PANEL_SHADOW_COLOR", panel.GetTextShadowColor, panel.SetTextShadowColor, apply, save, configure: o => {
            Row(o, "PANEL_SHADOW_COLOR");
            o.Default = Def.GetTextShadowColor();
        });

        VisualElement actions = Controls.Section(body, "HEADING_ACTIONS", o => o.Id = "HEADING_ACTIONS#" + n);
        Controls.Action(actions, "", "PANEL_RESETPOS", () => PanelsOverlay.ResetPosition(panel), o => o.Id = "PANEL_RESETPOS#" + n);
        // No confirm, as the classic button.
        Controls.Action(actions, "", "PANEL_DELETE", () => {
            List<PanelConfig> before = [.. Panels];
            if(Panels.Remove(panel)) Moved(before);
        }, o => o.Id = "PANEL_DELETE#" + n);
    }

    /// <summary>The section header's title, drawn as the player typed it: not looked up as a Lang key, and left alone by a
    /// relabel.</summary>
    private static Label Literal(VisualElement head, string text) {
        if(head == null || head.childCount < 2 || head[1] is not Label title) return null;
        Words.Label(title, "");
        title.text = text;
        return title;
    }
}
#endif
