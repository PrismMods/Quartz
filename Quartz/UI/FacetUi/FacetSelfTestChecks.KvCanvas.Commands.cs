#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Overlay;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The key viewer editor's commands, each one undo step that undo reverses exactly; the steps of a layout something else
/// changed going stale (and only those); press counts kept through undo; and an undo made with the page away still saved.
/// </summary>
internal static partial class FacetSelfTestChecks {
    // Runs command with history cleared first, then one undo: true when the document is exactly as before and nothing is left.
    private static IEnumerator KvOneStep(string json, bool[] back) {
        yield return Frames(1);   // a commit counts once a frame
        bool undid = global::Facet.History.Undo();
        back[0] = undid && KvJson() == json && !global::Facet.History.CanUndo;
        yield return Frames(1);
    }

    private static IEnumerator KvCommands(Action<string, bool, string> check) {
        var back = new bool[1];
        string json;
        List<KvElement> els;
        void Fresh() {
            global::Facet.History.Clear();
            KvEditorPage.Editor.Canvas.Clear();
            els = KvEls();
            json = KvJson();
        }

        Fresh();
        KvSelect(els[0], els[1]);
        KvRun(KeyLayoutText.Duplicate);
        List<KvElement> made = [.. KvEls().Except(els)], sel = KvSel();
        bool offset = made.Count == 2 && made.All(m => els.Take(2).Any(o => m.X == o.X + KvSnap.PasteOffset && m.Y == o.Y + KvSnap.PasteOffset));
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Duplicate of 2 makes 2 selected copies offset by 20, as one step undo reverses",
            offset && sel.Count == 2 && sel.All(made.Contains) && back[0], $"made={made.Count} offset={offset} selected={sel.Count} back={back[0]}");

        Fresh();
        KvSelect(els[0], els[1]);
        KvRun(KeyLayoutText.Cut);
        bool cut = KvEls().Count == els.Count - 2 && KvSel().Count == 0;
        string afterCut = KvJson();
        KvRun(KeyLayoutText.Paste);
        bool pasted = KvEls().Count == els.Count && KvSel().Count == 2;
        yield return Frames(1);
        bool undoPaste = global::Facet.History.Undo() && KvJson() == afterCut;
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Cut then Paste are a step each, undone exactly", cut && pasted && undoPaste && back[0],
            $"cut={cut} pasted={pasted} undoPaste={undoPaste} back={back[0]}");

        Fresh();
        KvSelect(els[2], els[3]);
        KvRun(KeyLayoutText.Delete);
        bool deleted = KvEls().Count == els.Count - 2 && KvSel().Count == 0;
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Delete removes the selection, leaves nothing selected, and undo reverses it", deleted && back[0],
            $"deleted={deleted} back={back[0]}");

        // The canvas's Delete key, then Edit ▸ Delete: the second finds nothing selected and removes nothing more.
        Fresh();
        KvSelect(els[2], els[3]);
        KvEditorPage.Editor.Canvas.DeleteSelection();
        bool cleared = KvSel().Count == 0;
        KvRun(KeyLayoutText.Delete);
        List<Rect> left = [.. KvEls().Select(e => e.Bounds)], want = [.. els.Where((_, i) => i != 2 && i != 3).Select(e => e.Bounds)];
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: two Delete presses (canvas key, then Edit ▸ Delete) remove exactly the 2 selected",
            cleared && left.SequenceEqual(want) && back[0], $"cleared={cleared} left={left.Count} want={want.Count} back={back[0]}");

        Fresh();
        KvSelect(els[0]);
        KvRun(KeyLayoutText.ToFront);
        bool front = KvEls()[^1] == els[0];
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Bring to Front puts the selection last in draw order, one step", front && back[0], $"front={front} back={back[0]}");

        Fresh();
        KvSelect(els[^1]);
        KvRun(KeyLayoutText.ToBack);
        bool rear = KvEls()[0] == els[^1];
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Send to Back puts the selection first in draw order, one step", rear && back[0], $"back={rear} undone={back[0]}");

        Fresh();
        KvElement[] three = [els[0], els[1], els[2]];
        float min = three.Min(e => e.X);
        KvSelect(three);
        KvRun(KeyLayoutText.AlignLeft);
        bool aligned = three.All(e => Mathf.Abs(e.X - min) < 0.01f);
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Align Left over 3 lines them up on the leftmost, one step", aligned && back[0], $"aligned={aligned} back={back[0]}");

        KvEls()[1].MoveTo(KvEls()[1].X + 7f, KvEls()[1].Y);   // stock keys are already evenly spaced
        Fresh();
        three = [els[0], els[1], els[2]];
        KvSelect(three);
        KvRun(KeyLayoutText.SpreadH);
        KvElement[] byX = [.. three.OrderBy(e => e.X)];
        float g1 = byX[1].X - (byX[0].X + byX[0].W), g2 = byX[2].X - (byX[1].X + byX[1].W);
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Spread Horizontally over 3 leaves equal gaps (±0.01), one step", Mathf.Abs(g1 - g2) <= 0.01f && back[0],
            $"gaps={g1:0.###},{g2:0.###} back={back[0]}");

        Fresh();
        KvSelect(els[0], els[1]);
        KvRun(KeyLayoutText.Hide);
        bool hid = els[0].Hidden && els[1].Hidden;
        string hiddenJson = KvJson();
        KvRun(KeyLayoutText.Show);
        bool shown = !els[0].Hidden && !els[1].Hidden;
        yield return Frames(1);
        bool undoShow = global::Facet.History.Undo() && KvJson() == hiddenJson;
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Hide then Show are a step each, undone exactly", hid && shown && undoShow && back[0],
            $"hid={hid} shown={shown} undoShow={undoShow} back={back[0]}");

        // Copy Style from A, Paste Style onto B: B looks like A, and keeps its place and binding.
        global::Facet.History.Clear();
        els = KvEls();
        KvElement a = els[0], b = els[1];
        a.Raw["borderRadius"] = 17;
        a.Raw["backgroundColor"] = "rgba(10, 20, 30, 0.5)";
        (Rect place, string bound) = (b.Bounds, b.GlobalKey);
        KvSelect(a);
        KvRun(KeyLayoutText.CopyStyle);
        Fresh();
        KvSelect(b);
        KvRun(KeyLayoutText.PasteStyle);
        bool styled = JToken.DeepEquals(a.CopyStyle(), b.CopyStyle()) && b.Bounds == place && b.GlobalKey == bound;
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Paste Style gives B A's style and keeps its place and binding, one step", styled && back[0],
            $"styled={styled} back={back[0]}");

        Fresh();
        KvSelect(els[0]);
        KvRun(KeyLayoutText.Duplicate);
        string next = global::Facet.History.NextUndoText;
        KvRun(KeyLayoutText.SelectAll);
        check($"{KvCanvasPage}: Select All selects every element and records no step",
            KvSel().Count == KvEls().Count && global::Facet.History.NextUndoText == next, $"selected={KvSel().Count}/{KvEls().Count} next='{next}'");
        yield return KvOneStep(json, back);

        // Ctrl+] is Bring to Front, not the canvas's one-step ].
        Fresh();
        KvSelect(els[0]);
        bool acted = KvCtrlBracket();
        int at = KvEls().IndexOf(els[0]);
        yield return KvOneStep(json, back);
        check($"{KvCanvasPage}: Ctrl+] brings the first element to the very front, one step", acted && at == els.Count - 1 && back[0],
            $"acted={acted} at={at}/{els.Count - 1} back={back[0]}");
        yield return KvBoxesSettle();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool KvCtrlBracket() =>
        KvEditorPage.Editor.Tick(k => k == KeyCode.RightBracket, k => k is KeyCode.LeftControl or KeyCode.RightBracket, true, false, false);

    // ---- stale steps

    private static IEnumerator KvEpoch(Action<string, bool, string> check) {
        yield return KvStale(check, "the classic editor adds an element", () => KvStore.Current.Add(kvHand, KvEls()[0].Clone()));
        yield return KvStale(check, "a foreign replace", () => KvStore.Replace(KvDocument.Parse(KvStore.Current.ToJson())));
        yield return KvStale(check, "a foreign delete and add (equal counts)", () => {
            KvDocument doc = KvStore.Current;
            KvElement k = doc.AllElements(kvHand)[0];
            doc.Remove(kvHand, k);
            doc.Add(kvHand, k.Clone());
        });

        // Undoing an unrelated step (which runs the after-apply and the commit) does not bring a stale Move back.
        global::Facet.History.Clear();
        yield return Frames(1);
        KvDragNow(KvEls()[0], new Vector2(25f, 0f));
        yield return Frames(1);
        KvStore.Replace(KvDocument.Parse(KvStore.Current.ToJson()));
        yield return Frames(2);
        global::Facet.History.Record("x", "x", () => { }, () => { });
        global::Facet.History.Undo();
        yield return Frames(1);
        string json = KvJson();
        bool undid = global::Facet.History.Undo();
        check($"{KvCanvasPage}: after a Move and a foreign replace, undoing an unrelated step leaves the Move stale",
            !undid && KvJson() == json && !global::Facet.History.CanUndo, $"undid={undid} same={KvJson() == json}");
        yield return KvBoxesSettle();

        // The editor's own structural edits keep every earlier step live.
        global::Facet.History.Clear();
        KvEditorPage.Editor.Canvas.Clear();
        json = KvJson();
        KvSelect(KvEls()[0]);
        KvRun(KeyLayoutText.Duplicate);
        yield return Frames(1);
        KvRun(KeyLayoutText.Delete);
        yield return Frames(1);
        bool u1 = global::Facet.History.Undo();
        yield return Frames(1);
        bool u2 = global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvCanvasPage}: after Duplicate then Delete, both undos apply and the layout is as before", u1 && u2 && KvJson() == json,
            $"undo1={u1} undo2={u2} same={KvJson() == json}");
        yield return KvBoxesSettle();

        // A foreign replace with no manual refresh: the next drag still lands in the live document.
        global::Facet.History.Clear();
        KvStore.Replace(KvDocument.Parse(KvStore.Current.ToJson()));
        yield return Frames(2);
        KvElement e = KvEls()[^1];   // the topmost: an earlier case left a copy under the first
        string key = KvEditorPage.Model.Key(e);
        float x0 = e.X;
        KvDragNow(e, new Vector2(40f, 0f));
        KvElement live = KvEditorPage.Model.Find(key);
        check($"{KvCanvasPage}: after a foreign replace, a drag moves the element in the live document",
            live != null && live.X > x0 + 30f, $"x={x0}->{live?.X}");
        yield return Frames(1);
        global::Facet.History.Undo();
        yield return KvBoxesSettle();
    }

    private static IEnumerator KvStale(Action<string, bool, string> check, string what, Action foreign) {
        global::Facet.History.Clear();
        KvEditorPage.Editor.Canvas.Clear();
        yield return Frames(1);
        KvDragNow(KvEls()[0], new Vector2(25f, 0f));
        bool moved = global::Facet.History.CanUndo;
        yield return Frames(1);
        foreign();
        yield return Frames(2);   // the editor's tick sees the change on the next frame
        Button undo = KvHeaderUndo();
        bool off = undo != null && !undo.enabledSelf;
        string json = KvJson();
        bool undid = global::Facet.History.Undo();
        check($"{KvCanvasPage}: after a Move, {what}: undo changes nothing and the header's undo is disabled",
            moved && off && !undid && KvJson() == json, $"moved={moved} header={undo?.enabledSelf} undid={undid} same={KvJson() == json}");
        yield return KvBoxesSettle();
    }

    // ---- counts through undo

    private static IEnumerator KvCounts(Action<string, bool, string> check) {
        OverlayPreview.Lease lease = OverlayPreview.Take(KeyViewerOverlay.FontKey);
        try {
            yield return KvCountsKept(check);
        } finally {
            lease?.Release();
        }
    }

    // Counts set on the elements directly (the flushed state of presses), then Insert a key and undo it.
    private static IEnumerator KvCountsKept(Action<string, bool, string> check) {
        for(int i = 0; i < 10 && !OverlayPreview.Is(KeyViewerOverlay.FontKey); i++) yield return null;
        KvLayoutModel model = KvEditorPage.Model;
        List<KvElement> keys = [.. KvEls().Where(e => e.Kind == KvElementKind.Key && e.GlobalKey.Length > 0).GroupBy(e => e.GlobalKey).Select(g => g.First())];
        string ka = model.Key(keys[0]), kb = model.Key(keys[1]);
        keys[0].Count = 41;
        keys[1].Count = 7;
        KeyViewerOverlay.Rebuild();
        global::Facet.History.Clear();
        yield return Frames(1);
        KvRun("KVI_ADD_KEY");
        KeyCaptureService.Cancel();
        yield return Frames(1);
        global::Facet.History.Undo();
        yield return Frames(1);
        KvElement a = model.Find(ka), b = model.Find(kb);
        check($"{KvCanvasPage}: counts of 41 and 7 survive inserting a key and undoing it", a?.Count == 41 && b?.Count == 7,
            $"a={a?.Count} b={b?.Count}");
        Type t = typeof(KeyViewerOverlay), boxType = t.GetNestedType("Box", BindingFlags.NonPublic);
        var boxes = (IList)t.GetField("boxes", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        FieldInfo source = boxType.GetField("Source"), count = boxType.GetField("Count");
        object box = boxes.Cast<object>().FirstOrDefault(x => ReferenceEquals(source.GetValue(x), a));
        check($"{KvCanvasPage}: the rebuilt overlay's box for that key shows 41", box != null && (int)count.GetValue(box) == 41,
            $"box={box != null} count={(box == null ? -1 : (int)count.GetValue(box))} shown={OverlayPreview.Is(KeyViewerOverlay.FontKey)}");
    }

    // ---- an undo made with the page away

    private static IEnumerator KvUndoAway(Action<string, bool, string> check) {
        global::Facet.History.Clear();
        KvEditorPage.Editor.Canvas.Clear();
        yield return Frames(1);
        KvElement e = KvEls()[0];
        string key = KvEditorPage.Model.Key(e);
        float x0 = e.X;
        KvDragNow(e, new Vector2(30f, 0f));
        yield return Frames(1);
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
        int r0 = KvLayoutModel.OverlayRebuildRequests;
        bool undid = global::Facet.History.Undo();
        int asked = KvLayoutModel.OverlayRebuildRequests - r0;
        yield return Frames(1);
        QuartzShell.Go(KeyViewerPage);
        yield return Frames(3);
        yield return KvBoxesSettle();
        KvElement now = KvEditorPage.Model.Find(key);
        bool drawn = now != null && KvBoxOf(now) != null;
        check($"{KvCanvasPage}: a Move undone from another page asks for one save and rebuild, and the canvas shows it undone on return",
            undid && asked == 1 && now?.X == x0 && drawn, $"undid={undid} asked={asked} x={now?.X} want={x0} drawn={drawn}");
    }
}
#endif
