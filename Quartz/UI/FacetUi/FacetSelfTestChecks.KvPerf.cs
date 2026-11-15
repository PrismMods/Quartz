#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.FacetUi.KeyViewerEditor;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The key viewer editor's budgets on the 108-key preset: a drag of every element, a marquee sweep, a refresh pass and a
/// redraw with all keys selected, one step and its undo on a full document with a large image table, tab clicks' memory,
/// the header's reads over a full stale history, and the page's build with that layout shown. Runs inside KvGuard.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string KvPerfPage = KeyViewerPage + ": performance";
    private const double KvFrameMs = 16.0;

    private static IEnumerator KvPerfChecks(Action<string, bool, string> check, Action<string> info) =>
        OnPage("keyviewer", KeyViewerPage, () => { }, () => { }, () => KvGuard(check, () => KvPerfOnPage(check, info)));

    private static IEnumerator KvPerfOnPage(Action<string, bool, string> check, Action<string> info) {
        if(!KvEditorDoor.Usable || !KvEditorDoor.Live) {
            check($"{KvPerfPage}: the layout editor is on the page", false, $"usable={KvEditorDoor.Usable} live={KvEditorDoor.Live}");
            yield break;
        }
        KvDocument doc = KvStore.Current;
        string tab = doc.NewTabId();
        doc.EnsureTab(tab, "Self Test 108");
        KvMigration.GenerateStockTab(doc, tab, KvPresets.Style108);
        KvEditorPage.Model.Show(LayoutGroup.Hands, tab);
        global::Facet.History.Clear();
        KvEditorPage.Editor.Canvas.ResetView();
        KvEditorPage.Editor.Refresh();
        yield return KvBoxesSettle();
        yield return KvDragBudget(check, info);
        yield return KvMarqueeBudget(check, info);
        yield return KvRefreshBudget(check, info);
        yield return KvStepBudget(check, info, tab);
        yield return KvPageBuildBudget(check, info);   // last: it rebuilds the page
    }

    private static (double mean, double worst) KvStats(List<double> ms) => (ms.Count == 0 ? -1 : ms.Average(), ms.Count == 0 ? -1 : ms.Max());

    // Every element selected, then 60 moves of 2 px, each timed around its dispatch, all in one frame so the real pointer
    // cannot land between them.
    private static IEnumerator KvDragBudget(Action<string, bool, string> check, Action<string> info) {
        global::Facet.History.Clear();
        List<KvElement> els = KvEls();
        KvSelect([.. els]);
        yield return Frames(1);
        KvElement e = els.First(x => x.Kind == KvElementKind.Key);
        (float x0, float y0) = (e.X, e.Y);
        Vector2 a = KvPanel(e.Bounds.center);
        int before = KvLayoutModel.OverlayRebuildRequests;
        bool quiet = true;
        List<double> ms = [];
        KvSend<PointerDownEvent>(a, 0);
        for(int i = 1; i <= 60; i++) {
            var clock = Stopwatch.StartNew();
            KvSend<PointerMoveEvent>(a + new Vector2(2f * i, 0f), 0);
            ms.Add(clock.Elapsed.TotalMilliseconds);
            quiet &= KvLayoutModel.OverlayRebuildRequests == before;
        }
        KvSend<PointerUpEvent>(a + new Vector2(120f, 0f), 0);
        int after = KvLayoutModel.OverlayRebuildRequests;
        (double mean, double worst) = KvStats(ms);
        info($"[perf] key viewer drag 108 mean {mean:0.000} ms, worst {worst:0.000} ms ({els.Count} elements, 60 moves)");
        check($"{KvPerfPage}: a drag of all {els.Count} elements moves them", e.X != x0 || e.Y != y0, $"from=({x0},{y0}) to=({e.X},{e.Y})");
        check($"{KvPerfPage}: each move of a drag of all {els.Count} elements takes at most 2 ms on average and 6 ms at worst",
            els.Count >= 100 && mean >= 0 && mean <= 2.0 && worst <= 6.0, $"mean={mean:0.000} worst={worst:0.000}");
        check($"{KvPerfPage}: that drag asks for no overlay rebuild until release, then one",
            quiet && after == before + 1, $"before={before} during unchanged={quiet} after={after}");
        yield return Frames(1);
    }

    // A marquee from outside the layout to past its far corner over 60 frames; each frame's move and the refresh pass it
    // causes (the inspector rebuilds for each new selection) are timed together.
    private static IEnumerator KvMarqueeBudget(Action<string, bool, string> check, Action<string> info) {
        KvEditorPage.Editor.Canvas.Clear();
        QuartzShell.Surface.Refresh();
        yield return Frames(1);
        List<KvElement> els = KvEls();
        Vector2 lo = new(els.Min(x => x.Bounds.xMin) - 10f, els.Min(x => x.Bounds.yMin) - 10f);
        Vector2 hi = new(els.Max(x => x.Bounds.xMax) + 10f, els.Max(x => x.Bounds.yMax) + 10f);
        List<double> ms = [];
        KvSend<PointerDownEvent>(KvPanel(lo), 0);
        try {
            for(int i = 1; i <= 60; i++) {
                var clock = Stopwatch.StartNew();
                KvSend<PointerMoveEvent>(KvPanel(Vector2.Lerp(lo, hi, i / 60f)), 0);
                QuartzShell.Surface.Refresh();
                ms.Add(clock.Elapsed.TotalMilliseconds);
                yield return null;
            }
        } finally {
            KvSend<PointerUpEvent>(KvPanel(hi), 0);
        }
        List<KvElement> sel = KvSel();
        (double mean, double worst) = KvStats(ms);
        info($"[perf] key viewer marquee 108 mean {mean:0.000} ms, worst frame {worst:0.000} ms ({sel.Count} selected)");
        check($"{KvPerfPage}: a marquee over all {els.Count} elements costs at most {KvFrameMs} ms in its worst frame",
            sel.Count == els.Count && worst >= 0 && worst <= KvFrameMs, $"worst={worst:0.000} selected={sel.Count}/{els.Count}");
        // The inspector waits for the release: the pass after it shows the whole selection.
        QuartzShell.Surface.Refresh();
        string want = KvInspectorPages.Title(sel);
        bool titled = KvInspectorRoot().Query<TextElement>().ToList().Any(t => t.text == want);
        check($"{KvPerfPage}: the refresh pass after the marquee's release shows the selection in the inspector", titled, $"want '{want}'");
        // A pen holds the inspector's build too: the canvas captures whichever pointer pressed.
        KvEditorPage.Editor.Canvas.Clear();
        QuartzShell.Surface.Refresh();
        int pen = PointerId.penPointerIdBase;
        KvSend<PointerDownEvent>(KvPanel(lo), 0, pen);
        bool held;
        try {
            KvSend<PointerMoveEvent>(KvPanel(hi), 0, pen);
            QuartzShell.Surface.Refresh();
            held = !KvInspectorRoot().Query<TextElement>().ToList().Any(t => t.text == want);
        } finally {
            KvSend<PointerUpEvent>(KvPanel(hi), 0, pen);
        }
        check($"{KvPerfPage}: a pen marquee also keeps the inspector's build until its release", held && KvSel().Count == els.Count,
            $"held={held} selected={KvSel().Count}/{els.Count}");
        yield return Frames(1);
    }

    // With every key selected on the Style tab: one refresh pass that rebuilds nothing, and one redraw of every box.
    private static IEnumerator KvRefreshBudget(Action<string, bool, string> check, Action<string> info) {
        List<KvElement> keys = [.. KvEls().Where(x => x.Kind == KvElementKind.Key)];
        yield return KvInspect(1, [.. keys]);
        var clock = Stopwatch.StartNew();
        QuartzShell.Surface.Refresh();
        double pass = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        KvEditorPage.Editor.Refresh();
        double redraw = clock.Elapsed.TotalMilliseconds;
        info($"[perf] key viewer refresh pass, 108 selected (Style tab) {pass:0.00} ms; canvas redraw {redraw:0.00} ms ({KvEls().Count} elements)");
        check($"{KvPerfPage}: a refresh pass with {keys.Count} keys selected on the Style tab takes at most {KvFrameMs} ms",
            keys.Count >= 100 && pass <= KvFrameMs, $"ms={pass:0.00}");
        check($"{KvPerfPage}: redrawing every box of the 108-key layout takes at most {KvFrameMs} ms", redraw <= KvFrameMs, $"ms={redraw:0.00}");
        KvEditorPage.Editor.Canvas.Clear();
        yield return Frames(1);
    }

    // A 30-tab document with the 108-key tab and a 4 MB image table: one step and its undo, 100 tab clicks' retained memory,
    // then the header's six reads over 500 steps a foreign replace made stale.
    private static IEnumerator KvStepBudget(Action<string, bool, string> check, Action<string> info, string tab) {
        KvLayoutModel model = KvEditorPage.Model;
        var ed = KvEditorPage.Editor;
        KvDocument doc = KvStore.Current;
        for(int n = doc.Tabs.Count(); n < 30; n++) {
            string t = doc.NewTabId();
            doc.EnsureTab(t, "Self Test " + n);
            KvMigration.GenerateStockTab(doc, t, 4);
        }
        string other = model.Layouts(LayoutGroup.Hands).First(t => t != tab);
        const int imageBytes = 4 << 20;
        doc.Root["embeddedLocalImages"] = new JArray { new JObject { ["imageId"] = "selftest-perf", ["dataBase64"] = new string('A', imageBytes), ["extension"] = "png" } };
        static int TableLength() => (KvStore.Current.Root["embeddedLocalImages"] as JArray)?.FirstOrDefault()?["dataBase64"]?.ToString().Length ?? 0;
        global::Facet.History.Clear();
        ed.Refresh();
        yield return Frames(1);

        KvElement k = model.Elements().First(x => x.Kind == KvElementKind.Key);
        Rect r = model.GetRect(k);
        var clock = Stopwatch.StartNew();
        ed.Do(KeyLayoutText.AlignLeft, () => model.SetRect(k, new Rect(r.x + 5f, r.y, r.width, r.height)));
        double doMs = clock.Elapsed.TotalMilliseconds;
        yield return Frames(1);
        clock.Restart();
        bool undone = global::Facet.History.Undo();
        double undoMs = clock.Elapsed.TotalMilliseconds;
        int tabs = KvStore.Current.Tabs.Count();
        info($"[perf] key viewer step on {tabs} tabs with a 4 MB image table: do {doMs:0.0} ms, undo {undoMs:0.0} ms");
        check($"{KvPerfPage}: one step and its undo on {tabs} tabs, the 108-key tab and a 4 MB image table take at most 50 ms each, and the table stays",
            undone && tabs >= 30 && doMs <= 50 && undoMs <= 50 && TableLength() == imageBytes, $"do={doMs:0.0} undo={undoMs:0.0} undone={undone} table={TableLength()}");
        yield return Frames(1);

        var select = KvStripOptions(true).Select;
        global::Facet.History.Clear();
        long mem0 = GC.GetTotalMemory(true);
        for(int i = 0; i < 100; i++) {
            select(i % 2 == 0 ? other : tab);
            if(i % 10 == 9) yield return null;
        }
        long mem1 = GC.GetTotalMemory(true), grew = mem1 - mem0;
        // What the 100 steps themselves hold: other memory freed meanwhile (the step above's documents) can make grew negative,
        // and collector noise can swing it by megabytes either way, so grew is logged only and held decides.
        global::Facet.History.Clear();
        long held = mem1 - GC.GetTotalMemory(true);
        info($"[perf] key viewer 100 tab clicks: retained memory grew {grew / 1048576.0:0.0} MB, their steps hold {held / 1024} KB ({held / 100} bytes a click)");
        check($"{KvPerfPage}: a tab click keeps under 16 KB (its step records the tabs, not the document)",
            model.Editing == tab && held / 100 < 16 << 10, $"grew={grew} held={held} editing={model.Editing}");

        global::Facet.History.Clear();
        Func<bool> valid = global::Facet.History.BoundTo(model.UndoDocument);
        for(int i = 0; i < 500; i++)
            using(global::Facet.History.Step("Self Test", null, valid)) global::Facet.History.Record("Self Test", null, () => { }, () => { });
        KvStore.Replace(KvDocument.Parse(KvStore.Current.ToJson()));   // as a profile switch: every step is stale
        yield return Frames(1);
        clock.Restart();
        bool can = global::Facet.History.CanUndo;
        string next = global::Facet.History.NextUndo, nextText = global::Facet.History.NextUndoText;
        bool canRedo = global::Facet.History.CanRedo;
        string nextRedo = global::Facet.History.NextRedo, nextRedoText = global::Facet.History.NextRedoText;
        double headerMs = clock.Elapsed.TotalMilliseconds;
        info($"[perf] key viewer header refresh over 500 stale steps {headerMs:0.00} ms");
        check($"{KvPerfPage}: the header's reads over 500 stale steps take at most {KvFrameMs} ms and name no step",
            !can && next == null && nextText == null && !canRedo && nextRedo == null && nextRedoText == null && headerMs <= KvFrameMs,
            $"ms={headerMs:0.00} can={can} next='{next}'");
        global::Facet.History.Clear();
        ed.Refresh();
        yield return Frames(1);
    }

    // The page's build with the 108-key tab shown, from another category as a first open landing on it.
    private static IEnumerator KvPageBuildBudget(Action<string, bool, string> check, Action<string> info) {
        yield return KeyViewerBuild(info, ", 108-key tab shown");
        check($"{KvPerfPage}: the page builds with the 108-key tab shown in at most 50 ms",
            kvPageBuildMs >= 0 && kvPageBuildMs <= 50, $"ms={kvPageBuildMs:0.0}");
    }
}
#endif
