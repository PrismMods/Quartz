#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Editor;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Overlay;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The live inspector's rows on the scratch hand tab: Mixed, a drag that redraws per frame and commits once, steps found
/// by key after a replace, Reset Count across a restore, the Key row's capture, and Rebind Keys in Order.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static List<KvElement> KvKeys() => [.. KvEls().Where(e => e.Kind == KvElementKind.Key)];

    // ---- Mixed: a slider over two different sizes, and a choice row over two different fits

    private static IEnumerator KvMixedRow(Action<string, bool, string> check) {
        List<KvElement> keys = KvKeys();
        keys[0].Raw["fontSize"] = 18;
        keys[1].Raw["fontSize"] = 30;
        keys[0].Raw["imageFit"] = "contain";
        keys[1].Raw.Remove("imageFit");
        global::Facet.History.Clear();
        yield return KvInspect(1, keys[0], keys[1]);
        string shown = KvInspectorRoot().Q("KVI_FONT_SIZE")?.Q<TextField>()?.value ?? "";
        check($"{KvInspPage}: two keys of sizes 18 and 30 show the size row as Mixed", shown.Length > 0 && !shown.Any(char.IsDigit), $"shown='{shown}'");
        int picked = KvPickedSegments("kv-image-fit");
        check($"{KvInspPage}: two keys of different image fits select no fit", picked == 0, $"selected={picked}");
        KvSetSlider("KVI_FONT_SIZE", 24f, 1f, 200f);
        yield return Frames(1);
        int a = KvProps.Int(keys[0].Raw, "fontSize", 0), b = KvProps.Int(keys[1].Raw, "fontSize", 0);
        check($"{KvInspPage}: setting the Mixed size writes both keys", a == 24 && b == 24, $"a={a} b={b}");
        global::Facet.History.Undo();
        yield return Frames(1);
        a = KvProps.Int(keys[0].Raw, "fontSize", 0);
        b = KvProps.Int(keys[1].Raw, "fontSize", 0);
        check($"{KvInspPage}: one undo puts both sizes back", a == 18 && b == 30 && !global::Facet.History.CanUndo, $"a={a} b={b} canUndo={(global::Facet.History.CanUndo)}");
        yield return KvInspect(1, keys[0]);
        picked = KvPickedSegments("kv-image-fit");
        check($"{KvInspPage}: one key selects its own fit", picked == 1, $"selected={picked}");

        // The rain and counter choice rows over two keys that differ in each.
        keys[0].Raw["noteBorderSide"] = "vertical";
        keys[0].Raw["noteAlignment"] = "left";
        keys[0].Raw["counter"] = new JObject { ["placement"] = "outside", ["align"] = "bottom", ["alignMode"] = "between" };
        keys[1].Raw["noteBorderSide"] = "horizontal";
        keys[1].Raw["noteAlignment"] = "right";
        keys[1].Raw["counter"] = new JObject { ["placement"] = "inside", ["align"] = "left", ["alignMode"] = "center" };
        foreach((int tab, string[] ids) in new[] { (2, new[] { "kv-note-border-side", "kv-note-align" }),
                     (3, new[] { "kv-counter-placement", "kv-counter-align", "kv-counter-mode" }) }) {
            yield return KvInspect(tab, keys[0], keys[1]);
            foreach(string id in ids) {
                picked = KvPickedSegments(id);
                check($"{KvInspPage}: two keys that differ select no segment in {id}", picked == 0, $"selected={picked}");
            }
        }
        foreach(KvElement k in keys.Take(2)) {
            k.Raw.Remove("noteBorderSide");
            k.Raw.Remove("noteAlignment");
            k.Raw.Remove("counter");
        }
    }

    private static int KvPickedSegments(string id) {
        VisualElement row = KvInspectorRoot().Q(id);
        if(row == null) return -1;
        Type state = typeof(global::Facet.Paint).GetNestedType("State", BindingFlags.NonPublic);
        FieldInfo selected = state.GetField("Selected");
        return row.Query<Button>().ToList().Count(b => KvFacetData(b, state) is { } s && (bool)selected.GetValue(s));
    }

    // ---- a 10-frame Corner Radius drag: the box follows from the first frame; one overlay rebuild, at release

    private static IEnumerator KvRowDrag(Action<string, bool, string> check) {
        KvElement e = KvKeys()[2];
        global::Facet.History.Clear();
        yield return KvInspect(1, e);
        Slider s = KvRow<Slider>("KVI_RADIUS");
        int r0 = KvLayoutModel.OverlayRebuildRequests;
        KvPointerTo<PointerDownEvent>(s);
        float first = -1f;
        for(int i = 1; i <= 10; i++) {
            s.value = (20f + i) / 100f;
            yield return Frames(1);
            if(i == 1) first = KvBoxOf(e)?.style.borderTopLeftRadius.value.value ?? -1f;
        }
        int during = KvLayoutModel.OverlayRebuildRequests - r0;
        KvPointerTo<PointerUpEvent>(s);
        yield return Frames(1);
        int after = KvLayoutModel.OverlayRebuildRequests - r0;
        check($"{KvInspPage}: a Corner Radius drag redraws the box on its first frame", Mathf.Abs(first - 21f) < 0.01f, $"radius={first}");
        check($"{KvInspPage}: the drag asks no overlay rebuild until release, then one", during == 0 && after == 1 && Mathf.Abs(KvRadius(e) - 30f) < 0.01f,
            $"during={during} after={after} radius={KvRadius(e)}");
    }

    private static void KvPointerTo<T>(VisualElement target) where T : PointerEventBase<T>, new() {
        using T ev = Pooled<T>();
        typeof(PointerEventBase<T>).GetProperty("button").SetValue(ev, 0);
        ev.target = target;
        target.SendEvent(ev);
    }

    // ---- a row's step after a replace finds its element by key; a row edit after a foreign replace lands in the live document

    private static IEnumerator KvRowKeys(Action<string, bool, string> check) {
        KvLayoutModel model = KvEditorPage.Model;
        KvElement e = KvKeys()[3];
        string key = model.Key(e);
        float r0 = KvRadius(e);
        global::Facet.History.Clear();
        yield return KvInspect(1, e);
        KvSetSlider("KVI_RADIUS", 33f, 0f, 100f);
        yield return Frames(1);
        model.Restore(model.Capture());
        yield return Frames(2);
        bool undid = global::Facet.History.Undo();
        yield return Frames(1);
        KvElement now = model.Find(key);
        check($"{KvInspPage}: undoing a row's step after a restore reaches the restored element by key",
            undid && now != null && !ReferenceEquals(now, e) && Mathf.Abs(KvRadius(now) - r0) < 0.01f, $"undid={undid} found={now != null} radius={(now == null ? -1 : KvRadius(now))} was={r0}");

        KvStore.Replace(KvDocument.Parse(KvStore.Current.ToJson()));   // foreign: no Refresh by hand
        yield return Frames(2);
        KvSetSlider("KVI_RADIUS", 44f, 0f, 100f);
        yield return Frames(1);
        now = model.Find(key);
        check($"{KvInspPage}: after a foreign replace a row edit lands in the live document", now != null && Mathf.Abs(KvRadius(now) - 44f) < 0.01f,
            $"radius={(now == null ? -1 : KvRadius(now))}");
    }

    // ---- Reset Count: undone after an Insert and its undo (both restores), the counts come back on the elements and the overlay

    private static IEnumerator KvResetCount(Action<string, bool, string> check) {
        OverlayPreview.Lease lease = OverlayPreview.Take(KeyViewerOverlay.FontKey);
        try {
            yield return KvResetCountKept(check);
        } finally {
            lease?.Release();
        }
    }

    private static IEnumerator KvResetCountKept(Action<string, bool, string> check) {
        for(int i = 0; i < 10 && !OverlayPreview.Is(KeyViewerOverlay.FontKey); i++) yield return null;
        KvLayoutModel model = KvEditorPage.Model;
        List<KvElement> keys = [.. KvKeys().Where(e => e.GlobalKey.Length > 0).GroupBy(e => e.GlobalKey).Select(g => g.First())];
        string ka = model.Key(keys[0]), kb = model.Key(keys[1]);
        keys[0].Count = 41;
        keys[1].Count = 7;
        KeyViewerOverlay.Rebuild();
        global::Facet.History.Clear();
        yield return KvInspect(0, keys[0], keys[1]);
        Submit(KvRow<Button>("KVI_COUNT_RESET"));
        yield return Frames(1);
        check($"{KvInspPage}: Reset Count zeroes both keys in one step", keys[0].Count == 0 && keys[1].Count == 0 && global::Facet.History.CanUndo,
            $"a={keys[0].Count} b={keys[1].Count} undo='{(global::Facet.History.NextUndoText)}'");
        KvRun("KVI_ADD_KEY");
        KeyCaptureService.Cancel();
        yield return Frames(1);
        global::Facet.History.Undo();
        yield return Frames(1);
        global::Facet.History.Undo();
        yield return Frames(1);
        KvElement a = model.Find(ka), b = model.Find(kb);
        check($"{KvInspPage}: Reset Count, Insert, undo, undo: the counts read 41 and 7 again", a?.Count == 41 && b?.Count == 7, $"a={a?.Count} b={b?.Count}");
        float until = Time.realtimeSinceStartup + 0.6f;   // the overlay rebuilds 0.2 s after the undo asked
        while(Time.realtimeSinceStartup < until) yield return null;
        Type t = typeof(KeyViewerOverlay), boxType = t.GetNestedType("Box", BindingFlags.NonPublic);
        var boxes = (IList)t.GetField("boxes", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        FieldInfo source = boxType.GetField("Source"), count = boxType.GetField("Count");
        int Shown(KvElement e) => boxes.Cast<object>().FirstOrDefault(x => ReferenceEquals(source.GetValue(x), e)) is { } box ? (int)count.GetValue(box) : -1;
        check($"{KvInspPage}: the overlay's boxes show 41 and 7 again", Shown(a) == 41 && Shown(b) == 7, $"a={Shown(a)} b={Shown(b)}");
    }

    // ---- the Key row: capture through KeyCaptureService, Escape leaves the key, a captured key is one step

    private static IEnumerator KvKeyCapture(Action<string, bool, string> check) {
        KvLayoutModel model = KvEditorPage.Model;
        KvElement e = KvKeys()[0];
        string key = model.Key(e), g0 = e.GlobalKey;
        global::Facet.History.Clear();
        yield return KvInspect(0, e);
        bool started = KvBeginCapture("KVI_REBIND");
        check($"{KvInspPage}: the Key row's BeginCapture starts the key capture", started && KeyCaptureService.IsCapturing, $"started={started} capturing={KeyCaptureService.IsCapturing}");
        KeyCaptureService.Cancel();
        yield return Frames(1);
        check($"{KvInspPage}: cancelling the capture leaves the key and records nothing",
            !KeyCaptureService.IsCapturing && model.Find(key)?.GlobalKey == g0 && !global::Facet.History.CanUndo, $"key={model.Find(key)?.GlobalKey} was={g0}");
        KvBeginCapture("KVI_REBIND");
        KvDeliverKey(KeyCode.F7);
        yield return Frames(1);
        KeyCode bound = model.Find(key)?.KeyCodeValue ?? KeyCode.None;
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvInspPage}: a captured F7 binds F7 in one step, and the undo restores the old key",
            bound == KeyCode.F7 && model.Find(key)?.GlobalKey == g0 && !global::Facet.History.CanUndo, $"bound={bound} after={model.Find(key)?.GlobalKey} was={g0}");
        int n = KvEls().Count;
        KvBeginCapture("KVI_REBIND");
        KvDeliverKey(KeyCode.Delete);
        bool released = KeyCaptureCoordinator.ReleasedThisFrame;
        KvEditorDoor.Tick();
        yield return Frames(1);
        check($"{KvInspPage}: capturing Delete binds it and deletes nothing", released && KvEls().Count == n && model.Find(key)?.KeyCodeValue == KeyCode.Delete,
            $"released={released} count={KvEls().Count}/{n} key={model.Find(key)?.KeyCodeValue}");

        KvElement g = model.Find(key);
        g.BindGhostKey(KeyCode.G);
        string ghost0 = g.GhostKey;
        global::Facet.History.Clear();
        yield return KvInspect(0, g);
        Submit(KvRow<Button>("KVI_GHOST_CLEAR"));
        yield return Frames(1);
        string cleared = model.Find(key)?.GhostKey;
        bool undone = global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvInspPage}: Clear Ghost Key unbinds the ghost key in exactly one step",
            ghost0.Length > 0 && cleared == "" && undone && model.Find(key)?.GhostKey == ghost0 && !global::Facet.History.CanUndo,
            $"was={ghost0} cleared='{cleared}' undone={undone} after={model.Find(key)?.GhostKey} more={(global::Facet.History.CanUndo)}");
    }

    private static bool KvBeginCapture(string id) {
        Button b = KvRow<Button>(id);
        Type start = typeof(global::Facet.Controls).GetNestedType("KeyStart", BindingFlags.NonPublic);
        return b != null && KvFacetData(b, start) is { } k && start.GetField("Begin").GetValue(k) is Func<bool> begin && begin();
    }

    // ---- Rebind Keys in Order: left to right, one capture each; Escape ends the walk

    private static IEnumerator KvRebindOrder(Action<string, bool, string> check) {
        KvLayoutModel model = KvEditorPage.Model;
        List<KvElement> ks = [.. KvKeys().Take(3)];
        float y = ks[0].Y;
        (ks[0].X, ks[1].X, ks[2].X) = (300f, 100f, 200f);
        foreach(KvElement k in ks) k.Y = y;
        string[] byX = [model.Key(ks[1]), model.Key(ks[2]), model.Key(ks[0])];
        KvEditorPage.Editor.Refresh();
        global::Facet.History.Clear();
        yield return Frames(1);
        KvSelect([.. ks]);
        KvRun(KeyLayoutText.RebindInOrder);
        yield return Frames(1);
        string status = KvStatus();
        KvDeliverKey(KeyCode.F1);
        KvDeliverKey(KeyCode.F2);
        KvDeliverKey(KeyCode.F3);
        yield return Frames(1);
        KeyCode[] got = [.. byX.Select(k => model.Find(k)?.KeyCodeValue ?? KeyCode.None)];
        check($"{KvInspPage}: Rebind Keys in Order says (1/3), then F1, F2 and F3 bind left to right",
            status.Contains("(1/3)") && got.SequenceEqual([KeyCode.F1, KeyCode.F2, KeyCode.F3]), $"status='{status}' got={string.Join(",", got)}");
        KvSelect([.. byX.Select(model.Find)]);
        KvRun(KeyLayoutText.RebindInOrder);
        yield return Frames(1);
        KvDeliverKey(KeyCode.F4);
        KvDeliverKey(KeyCode.None);   // Escape
        yield return Frames(1);
        got = [.. byX.Select(k => model.Find(k)?.KeyCodeValue ?? KeyCode.None)];
        check($"{KvInspPage}: Escape partway leaves the rest unchanged", got.SequenceEqual([KeyCode.F4, KeyCode.F2, KeyCode.F3]) && !KeyCaptureService.IsCapturing,
            $"got={string.Join(",", got)} capturing={KeyCaptureService.IsCapturing}");
    }
}
#endif
