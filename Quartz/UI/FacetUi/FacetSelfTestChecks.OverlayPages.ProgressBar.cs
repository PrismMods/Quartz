#if DEBUG && !IL2CPP
using System.Collections;
using System.Text.RegularExpressions;
using Facet;
using Quartz.Core;
using Quartz.Features.ProgressBar;
using Quartz.Overlay;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Progress Bar in the new menu: each style's rows against the classic page, a style change that swaps them without
/// rebuilding the page, its two columns' edges, the pre-fill switch seen by the classic page, a gradient stop added, moved
/// past another and undone, and removed, and the preview pane in both styles.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string ProgressBarPage = "overlay.progressbar";
    private static ProgressBarSettings PbConf => ProgressBarOverlay.Conf;

    // The classic page's rows in each style, the gradient off; the other style's rows are hidden.
    private static readonly string[] PbBoth = ["PROGRESSBAR_STYLE_LABEL", "PROGRESSBAR_PREFILLSTART", "PROGRESSBAR_USEMAPTIME",
        "PROGRESSBAR_FILLGRADIENT", "PROGRESSBAR_FILLCOLOR", "PROGRESSBAR_BACKCOLOR"];
    private static readonly string[] PbLine = ["PROGRESSBAR_LINETHICKNESS", "PROGRESSBAR_LINEEDGE_LABEL"];
    private static readonly string[] PbModern = ["PROGRESSBAR_WIDTH", "PROGRESSBAR_HEIGHT", "PROGRESSBAR_TOPOFFSET", "PROGRESSBAR_ROUNDING",
        "PROGRESSBAR_OUTLINETHICKNESS", "PROGRESSBAR_OUTLINECOLOR", "PROGRESSBAR_ANCHOR_LABEL", "PROGRESSBAR_RESETPOS"];

    private static IEnumerator OverlayProgressBar(Action<string, bool, string> check, Action<string> info) {
        ProgressBarOverlay.EnsureConf();
        ProgressBarSettings c = PbConf;
        StatColor g = c.FillGradient;
        (ProgressBarStyle style, bool gradient, Color back, float width) = (c.Style, g.Enabled, c.GetBackColor(), c.Width);
        List<ColorPoint> stops = [.. g.Points];
        yield return OnPage("progressbar", ProgressBarPage, () => {
            g.Enabled = false;   // the single fill colour's row shows for the parity check
            ProgressBarOverlay.Apply();
        }, () => {
            (c.Style, g.Enabled, c.Width) = (style, gradient, width);
            g.Points.Clear();
            g.Points.AddRange(stops);
            c.SetBackColor(back);
            ProgressBarOverlay.Apply();
            ProgressBarOverlay.Save();
        }, () => ProgressBarChecks(check, info, back));
    }

    private static IEnumerator ProgressBarChecks(Action<string, bool, string> check, Action<string> info, Color back) {
        yield return PbStyle(check, ProgressBarStyle.Line, [.. PbBoth, .. PbLine], PbModern);
        yield return PbStyle(check, ProgressBarStyle.Modern, [.. PbBoth, .. PbModern], PbLine);
        yield return ColumnEdges(check, ProgressBarPage, ProgressBarOverlay.PreviewId);
        yield return Flips(check, ProgressBarPage, "PROGRESSBAR_PREFILLSTART", Bind.To(() => PbConf.PrefillStart, v => {
            PbConf.PrefillStart = v;
            ProgressBarOverlay.Save();
        }));
        yield return PbStops(check);
        // Through the row's own picker: the background's channels set to red (green where it is red already), read off the bar.
        Color want = SameRgb(back, Color.red) ? Color.green : Color.red;
        foreach(ProgressBarStyle style in new[] { ProgressBarStyle.Line, ProgressBarStyle.Modern }) {
            PbConf.Style = style;
            PbConf.SetBackColor(back);
            ProgressBarOverlay.Apply();
            info($"{ProgressBarPage}: the preview checks in the {style} style");
            yield return PreviewChecks(check, info, ProgressBarPage, ProgressBarOverlay.PreviewId, () => SetChannels("PROGRESSBAR_BACKCOLOR", want),
                () => OverlayPreview.ShownOf(ProgressBarOverlay.PreviewId)?.transform.Find("Back")?.GetComponent<UnityEngine.UI.Image>() is { } image
                    && SameRgb(image.color, want));
        }
        PbConf.Width = 300f;   // narrower than the Line style's frame
        ProgressBarOverlay.Apply();
        MotionBounds(check, ProgressBarPage, OverlayPreview.ShownOf(ProgressBarOverlay.PreviewId)?.transform as RectTransform);
    }

    // The style picked in its own dropdown swaps the rows within 3 frames: the page is not rebuilt, its preview keeps the same
    // live lease, which frames the bar (Modern) or the slice of the line (Line). Then the style's rows, as Parity reads them.
    private static IEnumerator PbStyle(Action<string, bool, string> check, ProgressBarStyle style, string[] rows, string[] hidden) {
        string tag = $"{ProgressBarPage}: {style} picked in the style row";
        int frames = 0;
        for(; frames < 10 && OverlayPreview.Current is not { Released: false }; frames++) yield return null;
        OverlayPreview.Lease lease = OverlayPreview.Current;
        VisualElement row = Body.Q(name: "PROGRESSBAR_PREFILLSTART");
        string other = style == ProgressBarStyle.Line ? "PROGRESSBAR_STYLE_MODERN" : "PROGRESSBAR_STYLE_LINE";
        if(PbConf.Style == style) yield return PickItem("PROGRESSBAR_STYLE_LABEL", QuartzHost.Host.Translate(other));   // from the other style
        yield return PickItem("PROGRESSBAR_STYLE_LABEL", QuartzHost.Host.Translate(style == ProgressBarStyle.Line ? "PROGRESSBAR_STYLE_LINE" : "PROGRESSBAR_STYLE_MODERN"));
        string framed = OverlayPreview.FrameOf(ProgressBarOverlay.PreviewId)?.FirstOrDefault()?.name;
        string want = style == ProgressBarStyle.Line ? "PreviewFrame" : "Bar";
        check($"{tag} swaps the rows in place: the page is not rebuilt and the preview keeps its lease, framing the {want}",
            PbConf.Style == style && row != null && ReferenceEquals(Body.Q(name: "PROGRESSBAR_PREFILLSTART"), row) && lease != null
            && ReferenceEquals(OverlayPreview.Current, lease) && !lease.Released && framed == want,
            $"style={PbConf.Style} row={row != null} same={ReferenceEquals(Body.Q(name: "PROGRESSBAR_PREFILLSTART"), row)} lease={lease != null} "
            + $"kept={ReferenceEquals(OverlayPreview.Current, lease)} released={lease?.Released} framed={framed}");
        yield return Parity(check, ProgressBarPage, rows, hidden);
    }

    private static readonly Regex StopRow = new("^PROGRESSBAR_STOP(COLOR|POS|REMOVE)_[0-9]+$");
    private static int StopRows() => Body.Query<VisualElement>().Where(e => e.name != null && StopRow.IsMatch(e.name)).ToList().Count;
    // A stop's colour and position, and its remove while there is more than one.
    private static int RowsFor(int stops) => 2 * stops + (stops > 1 ? stops : 0);

    // The gradient switched on through its row shows its stops; Add puts a stop in and its three rows show within 3 frames; its
    // remove takes it out again. The run has two stops of its own, 0 and 100 %, whatever the player's; the page's restore puts
    // the player's back.
    private static IEnumerator PbStops(Action<string, bool, string> check) {
        StatColor g = PbConf.FillGradient;
        g.Points.Clear();
        g.Points.AddRange([new ColorPoint(0f, Color.red), new ColorPoint(1f, Color.blue)]);
        ProgressBarOverlay.Apply();
        VisualElement toggle = Body.Q(name: "PROGRESSBAR_FILLGRADIENT-input");
        if(!g.Enabled && toggle != null) Click(toggle);
        int frames = 0, count = g.Points.Count;
        for(; frames < 3 && StopRows() != RowsFor(count); frames++) yield return null;
        int rows = StopRows();
        bool on = g.Enabled && rows == RowsFor(count) && Body.Q(name: "PROGRESSBAR_FILLCOLOR") == null;
        List<ColorPoint> before = [.. g.Points];
        Button add = Body.Q<Button>("PROGRESSBAR_STOPADD-input");
        if(on && add != null) Submit(add);
        for(frames = 0; frames < 3 && StopRows() == rows; frames++) yield return null;
        int added = StopRows();
        check($"{ProgressBarPage}: the gradient switched on shows its stops, and Add puts one in whose rows show within 3 frames",
            on && add != null && g.Points.Count == count + 1 && added == RowsFor(count + 1),
            $"on={g.Enabled} stops={count}->{g.Points.Count} rows={rows}->{added} want={RowsFor(count)}->{RowsFor(count + 1)} frames={frames}");
        yield return PbStopUndo(check, g);
        yield return PbStopDefault(check, g);
        yield return PbStopRelabel(check);
        int fresh = g.Points.FindIndex(p => !before.Contains(p));
        Button remove = fresh < 0 ? null : Body.Q<Button>($"PROGRESSBAR_STOPREMOVE_{fresh}-input");
        if(remove != null) Submit(remove);
        for(frames = 0; frames < 3 && StopRows() != RowsFor(count); frames++) yield return null;
        check($"{ProgressBarPage}: the new stop's remove takes it out, and its rows go within 3 frames",
            remove != null && g.Points.SequenceEqual(before) && StopRows() == RowsFor(count),
            $"remove={remove != null} at={fresh} stops={g.Points.Count} rows={StopRows()} want={RowsFor(count)} frames={frames}");
    }

    // The first stop let go halfway past the second, then undone as the title bar's Undo does: the undo sorts the stops as it writes
    // the position, and the stops and their rows are in order again within 3 frames. Undone only when the top step is that
    // move, so nothing else of the player's is undone; the stop's position is put back as found either way. Read by position,
    // not index: stops at one position may sort either way.
    private static IEnumerator PbStopUndo(Action<string, bool, string> check, StatColor g) {
        ColorPoint moved = g.Points.FirstOrDefault();
        float was = moved?.Pos ?? 0f, next = g.Points.Count > 1 ? g.Points[1].Pos : 1f, after = g.Points.Count > 2 ? g.Points[2].Pos : 1f;
        float to = Mathf.Round((next + after) * 50f);
        yield return SetSlider("PROGRESSBAR_STOPPOS_0", 0f, 100f, to);
        bool passed = moved != null && Mathf.Abs(moved.Pos - to * 0.01f) < 0.0001f && next < moved.Pos && InOrder(g.Points) && StopsFollow(g);
        string undid = History.NextUndo, label = string.Format(QuartzHost.Text("PROGRESSBAR_STOP_POS", "Stop {0} Position"), 1);
        bool undone = passed && undid == label && History.Undo();
        int frames = 0;
        for(; frames < 3 && !(InOrder(g.Points) && StopsFollow(g)); frames++) yield return null;
        check($"{ProgressBarPage}: a stop moved past another and undone is back in order, and so are its rows within 3 frames",
            moved != null && undone && Mathf.Abs(moved.Pos - was) < 0.0001f && InOrder(g.Points) && StopsFollow(g),
            $"passed={passed} undid={undid} undone={undone} pos={moved?.Pos}/{was} at={g.Points.IndexOf(moved)} "
            + $"order={string.Join(",", g.Points.Select(p => p.Pos))} rows={string.Join(",", Enumerable.Range(0, g.Points.Count).Select(StopShows))} frames={frames}");
        if(moved != null) moved.Pos = was;
        g.SortPoints();
        QuartzShell.Surface?.Refresh();   // the rows follow the stops put back
        yield return null;
    }

    // A stop's position row moved off, then middle-clicked: back where the stop sat when its rows were built.
    private static IEnumerator PbStopDefault(Action<string, bool, string> check, StatColor g) {
        ColorPoint first = g.Points.FirstOrDefault();
        float was = first?.Pos ?? 0f, next = g.Points.Count > 1 ? g.Points[1].Pos : 1f;
        yield return SetSlider("PROGRESSBAR_STOPPOS_0", 0f, 100f, Mathf.Round((was + next) * 50f));   // short of the next stop: no reorder
        bool moved = first != null && first.Pos > was;
        if(Body.Q(name: "PROGRESSBAR_STOPPOS_0") is { } row) Press(row, 2);
        yield return Frames(1);
        check($"{ProgressBarPage}: a middle-click on a stop's position puts it back where it sat when its rows were built",
            moved && Mathf.Abs(first!.Pos - was) < 0.0001f, $"moved={moved} pos={first?.Pos} want={was}");
        if(first != null) first.Pos = was;
    }

    // A language change relabels the stops: their labels are formatted text, rebuilt in the new language within 30 frames.
    private static IEnumerator PbStopRelabel(Action<string, bool, string> check) {
        string was = MainCore.Tr.Language, other = was == "ko-KR" ? "en-US" : "ko-KR";
        string Shown() => Body.Q(name: "PROGRESSBAR_STOPPOS_0")?.Q<Label>()?.text;
        string Want() => string.Format(QuartzHost.Text("PROGRESSBAR_STOP_POS", "Stop {0} Position"), 1);
        string before = Shown(), after, wanted;
        int frames = 0;
        try {
            MainCore.Tr.Language = other;
            for(; frames < 30 && Shown() != Want(); frames++) yield return null;
            (after, wanted) = (Shown(), Want());
        } finally {
            MainCore.Tr.Language = was;
        }
        for(int i = 0; i < 30 && Shown() != Want(); i++) yield return null;
        check($"{ProgressBarPage}: a language change relabels the stops within 30 frames, and changing back relabels them again",
            before != null && after == wanted && after != before && Shown() == before,
            $"{was}='{before}' {other}='{after}' want='{wanted}' frames={frames} back='{Shown()}'");
    }

    private static bool InOrder(List<ColorPoint> points) {
        for(int i = 1; i < points.Count; i++) if(points[i - 1].Pos > points[i].Pos) return false;
        return true;
    }

    // Stop i's position as its row shows it: the slider's track runs 0..1 over 0..100 %. NaN when there is no such row.
    private static float StopShows(int i) => Body.Q<Slider>($"PROGRESSBAR_STOPPOS_{i}-input")?.value ?? float.NaN;

    // Every stop's row, in page order, shows that stop's position, and there is no row beyond them.
    private static bool StopsFollow(StatColor g) {
        for(int i = 0; i < g.Points.Count; i++) if(!(Mathf.Abs(StopShows(i) - g.Points[i].Pos) < 0.001f)) return false;
        return float.IsNaN(StopShows(g.Points.Count));
    }
}
#endif
