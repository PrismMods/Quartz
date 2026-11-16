#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.ProgressBar;
using Quartz.Overlay;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Progress Bar in the new menu, row for row as PageProgressBar, beside a live preview of the bar. Both styles'
/// rows are built and each shows while its style is chosen, so a style change swaps rows without rebuilding the page.
/// </summary>
internal static class ProgressBar {
    // Read on every call: a profile switch may replace what ProgressBarOverlay.Conf returns.
    private static ProgressBarSettings C => ProgressBarOverlay.Conf;
    private static readonly ProgressBarSettings Def = new();
    private const int MaxStops = 8;

    internal static void Page(VisualElement p) {
        ProgressBarOverlay.EnsureConf();
        ShellPage.Feature(p, "progressbar", "SECTION_PROGRESS_BAR", "PROGRESSBAR_DESC", ProgressBarOverlay.PreviewId, Cards);
    }

    private static void Cards(VisualElement p) {
        Action apply = ProgressBarOverlay.Apply, save = ProgressBarOverlay.Save;
        Func<bool> line = () => C.Style == ProgressBarStyle.Line, modern = () => C.Style != ProgressBarStyle.Line;
        // Facet re-reads the rows every 100 ms; a change that swaps rows has them re-read at once. That may rebuild the stops,
        // the clicked button's row among them: its click is done by then, and Facet's pass reads its rows by index.
        Action swap = () => QuartzShell.Surface?.Refresh();
        VisualElement bar = Controls.Card(p, "SECTION_PROGRESS_BAR");
        // The classic page is built per style, so it rebuilds; this one shows the style's rows and keeps its preview.
        Controls.Dropdown(bar, "PROGRESSBAR_STYLE_LABEL", QuartzShell.Writes(() => (int)C.Style, v => {
            C.Style = (ProgressBarStyle)v;
            apply();
            save();
            swap();
        }), o => {
            o.Id = "PROGRESSBAR_STYLE_LABEL";
            o.Items = ["PROGRESSBAR_STYLE_MODERN", "PROGRESSBAR_STYLE_LINE"];
            o.Default = (int)Def.Style;
            o.Tooltip = "DESC_PROGRESSBAR_STYLE";
        });
        ShellRows.Toggle(bar, "PROGRESSBAR_PREFILLSTART", "progressbar_prefillstart", Bind.To(() => C.PrefillStart, v => C.PrefillStart = v), null, save,
            o => {
                o.Default = Def.PrefillStart;
                o.Tooltip = "DESC_PROGRESSBAR_PREFILLSTART";
            });
        ShellRows.Toggle(bar, "PROGRESSBAR_USEMAPTIME", "progressbar_usemaptime", Bind.To(() => C.UseMapTime, v => C.UseMapTime = v), apply, save, o => {
            o.Default = Def.UseMapTime;
            o.Tooltip = "DESC_PROGRESSBAR_USEMAPTIME";
        });

        VisualElement size = Controls.Card(p, "HEADING_SIZE");
        ShellRows.Slider(size, "PROGRESSBAR_LINETHICKNESS", Bind.To(() => C.LineThickness, v => C.LineThickness = v), 1f, 40f, 1f, "0 px", apply, save,
            o => { o.Default = Def.LineThickness; o.VisibleWhen = line; });
        Controls.Dropdown(size, "PROGRESSBAR_LINEEDGE_LABEL", QuartzShell.Writes(() => C.LineAtBottom ? 1 : 0, v => {
            C.LineAtBottom = v == 1;
            apply();
            save();
        }), o => {
            o.Id = "PROGRESSBAR_LINEEDGE_LABEL";
            o.Items = ["PROGRESSBAR_LINEEDGE_TOP", "PROGRESSBAR_LINEEDGE_BOTTOM"];
            o.Default = Def.LineAtBottom ? 1 : 0;
            o.Tooltip = "DESC_PROGRESSBAR_LINEEDGE";
            o.VisibleWhen = line;
        });
        ShellRows.Slider(size, "PROGRESSBAR_WIDTH", Bind.To(() => C.Width, v => C.Width = v), 200f, 1800f, 1f, "0 px", apply, save,
            o => { o.Default = Def.Width; o.VisibleWhen = modern; });
        ShellRows.Slider(size, "PROGRESSBAR_HEIGHT", Bind.To(() => C.Height, v => C.Height = v), 2f, 60f, 1f, "0 px", apply, save,
            o => { o.Default = Def.Height; o.VisibleWhen = modern; });
        ShellRows.Slider(size, "PROGRESSBAR_ROUNDING", Bind.To(() => C.Rounding, v => C.Rounding = v), 0f, 30f, 1f, "0 px", apply, save,
            o => { o.Default = Def.Rounding; o.VisibleWhen = modern; });
        VisualElement more = Controls.Advanced(size);
        ShellRows.Slider(more, "PROGRESSBAR_TOPOFFSET", Bind.To(() => C.TopOffset, v => C.TopOffset = v), 0f, 200f, 1f, "0 px", apply, save,
            o => { o.Default = Def.TopOffset; o.VisibleWhen = modern; });
        ShellRows.Slider(more, "PROGRESSBAR_OUTLINETHICKNESS", Bind.To(() => C.OutlineThickness, v => C.OutlineThickness = v), 0f, 8f, 0.25f,
            "0.## px", apply, save, o => { o.Default = Def.OutlineThickness; o.VisibleWhen = modern; });

        VisualElement colour = Controls.Card(p, "HEADING_COLOR");
        VisualElement fill = null;
        var built = new Built();
        ShellRows.Toggle(colour, "PROGRESSBAR_FILLGRADIENT", "progressbar_fillgradient",
            Bind.To(() => C.FillGradient.Enabled, v => C.FillGradient.Enabled = v), apply + swap, save, o => {
                o.Default = Def.FillGradient.Enabled;
                o.Tooltip = "DESC_PROGRESSBAR_FILLGRADIENT";
                o.VisibleWhen = () => { Follow(fill, built, swap); return true; };   // asked on every refresh: the fill rows follow the gradient
            });
        fill = Ui.Box(colour);
        Follow(fill, built, swap);
        ShellRows.Colour(colour, "PROGRESSBAR_BACKCOLOR", () => C.GetBackColor(), c => C.SetBackColor(c), apply, save,
            configure: o => o.Default = Def.GetBackColor());
        ShellRows.Colour(colour, "PROGRESSBAR_OUTLINECOLOR", () => C.GetOutlineColor(), c => C.SetOutlineColor(c), apply, save,
            configure: o => { o.Default = Def.GetOutlineColor(); o.VisibleWhen = modern; });

        // The line spans the screen edge and has no position of its own.
        VisualElement layout = Controls.Card(p, "MENU_LAYOUT", o => o.VisibleWhen = modern);
        ShellRows.Anchor(layout, "PROGRESSBAR_ANCHOR_LABEL", () => (int)OverlayAnchors.Parse(C.Anchor), () => C.AnchorClosest,
            a => ProgressBarOverlay.SetAnchor((OverlayAnchor)a), v => {
                C.AnchorClosest = v;
                save();
            });
        ShellRows.ResetPosition(layout, "PROGRESSBAR_RESETPOS", ProgressBarOverlay.ResetPosition);
    }

    // What the fill rows were built for: the gradient, whether it was on, its stops in order, and the language their labels
    // were formatted in (Facet relabels keys, and a stop's label is formatted text).
    private sealed class Built {
        internal StatColor Gradient;
        internal bool On;
        internal string Language;
        internal readonly List<ColorPoint> Stops = [];

        internal bool Shows(StatColor g) {
            if(!ReferenceEquals(g, Gradient) || g.Enabled != On) return false;
            if(!On) return true;
            if(Language != MainCore.Tr.Language) return false;
            if(g.Points.Count != Stops.Count) return false;
            for(int i = 0; i < Stops.Count; i++) if(!ReferenceEquals(g.Points[i], Stops[i])) return false;   // by index: asked every refresh
            return true;
        }
    }

    // The fill colour, or the gradient's stops and Add, rebuilt when the gradient is switched, replaced, or a stop is added,
    // removed or moved past another, or the language changes; nothing else on the page is.
    private static void Follow(VisualElement fill, Built b, Action swap) {
        if(fill?.parent == null) return;   // asked once while the switch is built, before the rows' box is placed
        StatColor g = C.FillGradient;
        if(b.Shows(g)) return;
        (b.Gradient, b.On, b.Language) = (g, g.Enabled, MainCore.Tr.Language);
        b.Stops.Clear();
        b.Stops.AddRange(g.Points);
        fill.Clear();
        Action apply = ProgressBarOverlay.Apply, save = ProgressBarOverlay.Save;
        if(!g.Enabled) {
            ShellRows.Colour(fill, "PROGRESSBAR_FILLCOLOR", () => C.GetFillColor(), c => C.SetFillColor(c), apply, save,
                configure: o => o.Default = Def.GetFillColor());
            return;
        }
        // A stop added, removed or let go: the stops stay in order, and the classic page, which lists them as built, is stale.
        void Edit(Action change) {
            change();
            g.SortPoints();
            apply();
            save();
            swap();
        }
        for(int i = 0; i < g.Points.Count; i++) {
            ColorPoint point = g.Points[i];
            ShellRows.Colour(fill, Stop("PROGRESSBAR_STOP_COLOR", "Stop {0} Color", i), point.GetColor, point.SetColor, apply, save, configure: o => {
                o.Id = "PROGRESSBAR_STOPCOLOR_" + i;
                o.Default = point.GetColor();
            });
            Control<float> pos = ShellRows.Slider(fill, Stop("PROGRESSBAR_STOP_POS", "Stop {0} Position", i), Bind.To(() => point.Pos * 100f, v => {
                point.Pos = v * 0.01f;
                // An undo or redo sorts here, as it raises the row's commit only while the row is on a panel; the refresh after it re-reads the rows.
                if(History.Applying) g.SortPoints();
            }), 0f, 100f, 1f, "0' %'", apply, save, o => {
                o.Id = "PROGRESSBAR_STOPPOS_" + i;
                o.Default = point.Pos * 100f;   // where the stop sat when its rows were built, as the classic slider's default
            });
            pos.Committed += _ => { if(!History.Applying) Edit(() => { }); };   // in order once let go: the rows follow; an undo sorted, applied and saved as it wrote
            if(g.Points.Count > 1)
                Controls.Action(fill, "", Stop("PROGRESSBAR_STOP_REMOVE", "Remove Stop {0}", i), () => Edit(() => g.Points.Remove(point)),
                    o => o.Id = "PROGRESSBAR_STOPREMOVE_" + i);
        }
        if(g.Points.Count < MaxStops)
            Controls.Action(fill, "", "PROGRESSBAR_STOPADD", () => Edit(() => g.Points.Add(new ColorPoint(0.5f, g.Evaluate(0.5f)))),
                o => o.Id = "PROGRESSBAR_STOPADD");
    }

    // A stop's label: its number in a format key, translated as built. The row's Id carries the index instead.
    private static string Stop(string key, string english, int i) => string.Format(QuartzHost.Text(key, english), i + 1);
}
#endif
