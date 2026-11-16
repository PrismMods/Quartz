#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Overlay;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// The panels in Overlay ▸ Panels: their drawing order as a list of names, dragged to reorder and clicked to pick, then the
/// picked panel's rows. Every row's Id is its Lang key and #n, n the panel's place; a move renumbers them, so the rows are
/// built again. Panel names are the player's: the list is read only while the
/// page is on a panel, so the search index's detached build has none.
/// </summary>
internal static class PanelsList {
    private static List<PanelConfig> Panels => PanelsOverlay.Conf.Panels;
    private static readonly PanelConfig Def = new();
    private static readonly IReadOnlyList<PanelConfig> NoPanels = [];
    private static readonly LabelSeparatorSpacing[] Spacings = [LabelSeparatorSpacing.Both, LabelSeparatorSpacing.After, LabelSeparatorSpacing.None];

    /// <summary>"No panels" while there are none, then the list (the one place a panel is picked), then the picked panel's rows.
    /// The picked panel is the one the preview shows.</summary>
    internal static void List(VisualElement card) {
        Controls.Info(card, "PANEL_NO_PANELS", () => "", (InfoOptions o) => {
            o.Id = "PANEL_NO_PANELS";
            o.VisibleWhen = () => Panels.Count == 0;
        });
        Controls.Reorder<PanelConfig>(card, o => {
            o.Id = "PANELS_LIST";
            o.Vertical = true;
            o.Tooltip = "DESC_PANEL_LAYER";
            o.Items = () => card.panel == null ? NoPanels : Panels;
            o.Key = p => ShellRows.ItemKey(p, Panels.IndexOf(p));
            o.Text = p => p.Name;
            o.Move = Move;
            o.Selected = p => p == Shown;
            o.Select = p => {
                Show(p);
                QuartzShell.Surface?.Refresh();
            };
        });
        // Built again when another panel is picked or a panel comes, goes or moves; detached (the search index's build), with no panel.
        Controls.Live(card, () => card.panel == null || Shown is not { } p ? "" : ShellRows.ItemKey(p, Panels.IndexOf(p)) + "|" + Panels.Count, box => {
            if(card.panel == null || Shown is not { } p) return;
            Build(box, p, Panels.IndexOf(p) + 1);
        });
    }

    /// <summary>The panel picked: the one last picked while it is still there, else the first; null with no panels.</summary>
    internal static PanelConfig Shown => Panels.Contains(PanelsOverlay.PreviewPanel) ? PanelsOverlay.PreviewPanel : Panels.Count > 0 ? Panels[0] : null;

    /// <summary>Picks panel: its rows show under the list and the preview shows it. A stat inspected on another panel is let go.</summary>
    internal static void Show(PanelConfig panel) {
        PanelsOverlay.PreviewPanel = panel;
        if(PanelsOverlay.PreviewStat != null && panel?.Stats.Contains(PanelsOverlay.PreviewStat) != true) PanelsOverlay.PreviewStat = null;
    }

    // Each panel's rows by their Lang keys (never a panel's name). The search index builds the page detached, where the list
    // has no panel, so these stand in for them: each finds this page.
    internal static readonly string[] RowKeys = ["PANEL_NAME", "PANEL_ADDSTAT", "PANEL_CLEARSTATS", "PANEL_SHOWSTAT", "PANEL_SHOWLABEL",
        "PANEL_ANCHOR_LABEL", "PANEL_PREFIX", "PANEL_SEPARATOR", "PANEL_SEPARATORSPACING_LABEL", "PANEL_FONTSIZE", "PANEL_LINESPACING",
        "PANEL_DECIMALS", "PANEL_TEXTCOLOR", "PANEL_BACKGROUND", "PANEL_BGCOLOR", "PANEL_LOCALIZESTATS", "PANEL_TEXTSHADOW", "PANEL_SHADOW_X",
        "PANEL_SHADOW_Y", "PANEL_SHADOW_SOFTNESS", "PANEL_SHADOW_COLOR", "PANEL_RESETPOS", "PANEL_DELETE"];

    /// <summary>The panels' rows as search entries on Overlay ▸ Panels; asked whenever the index builds.</summary>
    internal static IEnumerable<Facet.SearchIndex.Entry> SearchRows() {
        NavPage page = NavRegistry.ByKey(PageKey);
        if(page == null) yield break;
        string where = QuartzShell.Label(page.LocaleKey, page.Title);
        foreach(string key in RowKeys)
            yield return new Facet.SearchIndex.Entry {
                Label = key,
                Where = where,
                Tip = MainCore.Tr.GetForLanguage(key, "en-US", key),   // an English query still finds the row in another language
                Open = () => {
                    if(key is "PANEL_SHOWSTAT" or "PANEL_SHOWLABEL") PanelsStats.InspectFirst();   // a stat's row: the first stat's inspector
                    QuartzShell.Go(PageKey);
                },
            };
    }

    internal const string PageKey = "overlay.panels";

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
        FoldsFollow(Places(before));
        QuartzShell.Surface?.Refresh();
    }

    // The page's sections whose fold state is stored under an Id numbered by a panel's place; each opens until the reader
    // first folds it.
    private static readonly string[] PanelFolds = ["HEADING_STATS", "HEADING_APPEARANCE", "HEADING_SHADOW", "HEADING_ACTIONS"];

    /// <summary>Each panel's place and each stat's number on the page (1-based), as the row Ids number them.</summary>
    internal static Dictionary<object, int> Places(List<PanelConfig> panels) {
        var at = new Dictionary<object, int>();   // neither type overrides Equals: by reference
        int g = 0;
        for(int i = 0; i < panels.Count; i++) {
            at[panels[i]] = i + 1;
            foreach(StatEntry s in panels[i].Stats) at[s] = ++g;
        }
        return at;
    }

    /// <summary>After panels or stats changed places: each stored fold state follows its panel or stat to its new number, and a
    /// number left behind goes back to its default, so the next panel there does not open as the one that left.</summary>
    internal static void FoldsFollow(Dictionary<object, int> before) {
        NavPage page = NavRegistry.ByKey(PageKey);
        if(page == null) return;
        string prefix = "fold." + QuartzShell.Label(page.LocaleKey, page.Title) + "/";   // this page's folds only, as Facet keys them
        Dictionary<object, int> after = Places(Panels);
        var at = new Dictionary<(bool Stat, int N), object>();
        foreach(var kv in before) at[(kv.Key is StatEntry, kv.Value)] = kv.Key;
        var landed = new Dictionary<string, string>();
        var left = new List<string>();
        foreach(var kv in MainCore.Conf.FacetWindows) {
            if(!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            int slash = kv.Key.LastIndexOf('/'), hash = kv.Key.LastIndexOf('#');
            if(slash < 0 || hash < slash || !int.TryParse(kv.Key[(hash + 1)..], out int n)) continue;
            string id = kv.Key[(slash + 1)..hash];
            if(Array.IndexOf(PanelFolds, id) < 0) continue;
            left.Add(kv.Key);
            if(at.TryGetValue((false, n), out object o) && after.TryGetValue(o, out int now)) landed[kv.Key[..(hash + 1)] + now] = kv.Value;
        }
        foreach(string key in left)
            if(!landed.ContainsKey(key)) QuartzHost.Write(key, "1");
        foreach(var kv in landed) QuartzHost.Write(kv.Key, kv.Value);
    }

    /// <summary>Panel n's rows (n = its place + 1); every row's steps go stale with the panel.</summary>
    internal static void Build(VisualElement box, PanelConfig panel, int n) {
        string classic = "panel" + (n - 1);   // the classic rows' ids, which their toggles' hotkeys are keyed by
        Action save = PanelsOverlay.Save, apply = PanelsOverlay.Apply;
        Func<object> document = ShellRows.ItemDocument(panel, () => Panels);
        void Row(ControlOptions o, string key) {
            o.Id = key + "#" + n;
            o.UndoDocument = document;
        }
        VisualElement body = box;
        ShellRows.Text(body, "PANEL_NAME", Bind.To(() => panel.Name, v => panel.Name = string.IsNullOrWhiteSpace(v) ? "Panel" : v), 24, null, save, o => {
            Row(o, "PANEL_NAME");
            o.Tooltip = "DESC_PANEL_NAME";
        });

        PanelsStats.Build(ShellRows.Section(body, "HEADING_STATS", o => o.Id = "HEADING_STATS#" + n), panel, n);

        VisualElement look = ShellRows.Section(body, "HEADING_APPEARANCE", o => o.Id = "HEADING_APPEARANCE#" + n);
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

        VisualElement shadow = ShellRows.Section(body, "HEADING_SHADOW", o => o.Id = "HEADING_SHADOW#" + n);
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

        VisualElement actions = ShellRows.Section(body, "HEADING_ACTIONS", o => o.Id = "HEADING_ACTIONS#" + n);
        // Delete has no confirm, as the classic button.
        ShellRows.Buttons(actions, new ShellRows.Btn("PANEL_RESETPOS", () => PanelsOverlay.ResetPosition(panel), more: o => o.Id = "PANEL_RESETPOS#" + n),
            new ShellRows.Btn("PANEL_DELETE", () => {
                List<PanelConfig> before = [.. Panels];
                if(Panels.Remove(panel)) Moved(before);
            }, more: o => o.Id = "PANEL_DELETE#" + n));
    }
}
#endif
