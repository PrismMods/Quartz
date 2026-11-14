#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.IO;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The key viewer editor's canvas: one box per element drawn from its fields, the companion tab's ghosts, a drag that
/// commits once, the Insert menu, the context menu and right-drag pan, double-click capture, steps live as recorded and the
/// View ▸ Grid pref. The commands, the epoch and the counts are in FacetSelfTestChecks.KvCanvas.Commands.cs. Every check
/// runs inside KvGuard on scratch tabs: a hand tab of 8 keys and a foot tab of 4.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string KvCanvasPage = KeyViewerPage + ": canvas";
    private static string kvHand, kvFoot;

    private static IEnumerator KvCanvasChecks(Action<string, bool, string> check, Action<string> info) =>
        OnPage("keyviewer", KeyViewerPage, () => { }, () => { }, () => KvGuard(check, () => KvCanvasOnPage(check)));

    private static IEnumerator KvCanvasOnPage(Action<string, bool, string> check) {
        if(!KvEditorDoor.Usable || !KvEditorDoor.Live) {
            check($"{KvCanvasPage}: the layout editor is on the page", false, $"usable={KvEditorDoor.Usable} live={KvEditorDoor.Live}");
            yield break;
        }
        KvScratchTabs();
        yield return KvBoxesSettle();
        yield return KvCanvasDraw(check);
        yield return KvCanvasDrag(check);
        yield return KvStepsLive(check);
        yield return KvCommands(check);
        yield return KvInsert(check);
        yield return KvContextMenu(check);
        yield return KvDoubleClick(check);
        KvPrefs(check);
        yield return KvEpoch(check);
        yield return KvCounts(check);
        yield return KvUndoAway(check);
    }

    // As classic Create and AddFoot make them: a hand tab of 8 keys shown and edited, and a foot tab of 4 shown beside it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void KvScratchTabs() {
        KvDocument doc = KvStore.Current;
        kvHand = doc.NewTabId();
        doc.EnsureTab(kvHand, "Self Test Hands");
        KvMigration.GenerateStockTab(doc, kvHand, 4);
        kvFoot = doc.NewTabId();
        doc.EnsureTab(kvFoot, "Self Test Feet");
        doc.SetFootTab(kvFoot, true);
        KvMigration.GenerateStockFootTab(doc, kvFoot, kvHand, 4);
        KvEditorPage.Model.Show(LayoutGroup.Feet, kvFoot);
        KvEditorPage.Model.Show(LayoutGroup.Hands, kvHand);
        global::Facet.History.Clear();
        var ed = KvEditorPage.Editor;
        ed.Canvas.ResetView();
        ed.Canvas.Snap = true;
        ed.Refresh();
    }

    // ---- helpers

    private static List<KvElement> KvEls() => [.. KvEditorPage.Model.Elements()];

    // The canvas's element boxes (each holds the drawn content), without the ghosts.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<VisualElement> KvItemBoxes() {
        var c = KvEditorPage.Editor.Canvas;
        VisualElement ghosts = c.Content.Q(name: "kle-ghosts");
        return [.. c.Content.Query(name: "kv-content").ToList().Select(v => v.parent).Where(b => ghosts == null || !ghosts.Contains(b))];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static VisualElement KvGhostLayer() => KvEditorPage.Editor.Canvas.Content.Q(name: "kle-ghosts");

    private static VisualElement KvBoxOf(KvElement e) => KvItemBoxes().FirstOrDefault(b =>
        Mathf.Abs(b.style.left.value.value - e.X) < 0.01f && Mathf.Abs(b.style.top.value.value - e.Y) < 0.01f);

    // New boxes are built on the canvas's next refresh pass.
    private static IEnumerator KvBoxesSettle() {
        for(int i = 0; i < 40 && KvItemBoxes().Count != KvEls().Count; i++) yield return null;
        yield return Frames(1);
    }

    // A canvas point as a panel position: canvas to stage through the view, then stage to panel.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Vector2 KvPanel(Vector2 canvas) {
        var c = KvEditorPage.Editor.Canvas;
        return c.Stage.LocalToWorld(c.CanvasToStage(canvas));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void KvSend<T>(Vector2 panel, int button, int pointerId = 0) where T : PointerEventBase<T>, new() {
        VisualElement stage = KvEditorPage.Editor.Canvas.Stage;
        using T ev = Pooled<T>();
        typeof(PointerEventBase<T>).GetProperty("pointerId").SetValue(ev, pointerId);
        typeof(PointerEventBase<T>).GetProperty("button").SetValue(ev, button);
        typeof(PointerEventBase<T>).GetProperty("position").SetValue(ev, (Vector3)panel);
        ev.target = stage;
        stage.SendEvent(ev);
    }

    // A left drag in one frame (down, three moves, up), so the real pointer cannot land between; returns the rebuild requests
    // counted just before the release.
    private static int KvDragNow(KvElement e, Vector2 by) {
        Vector2 from = e.Bounds.center;
        Vector2 a = KvPanel(from);
        KvSend<PointerDownEvent>(a, 0);
        for(int i = 1; i <= 3; i++) KvSend<PointerMoveEvent>(KvPanel(from + by * (i / 3f)), 0);
        int during = KvLayoutModel.OverlayRebuildRequests;
        KvSend<PointerUpEvent>(KvPanel(from + by), 0);
        return during;
    }

    private static global::Facet.MenuItem KvItem(string id) {
        static global::Facet.MenuItem In(IList<global::Facet.MenuItem> items, string id) {
            foreach(var it in items ?? []) {
                if(it == null) continue;
                if(it.Id == id) return it;
                if(In(it.Items, id) is { } found) return found;
            }
            return null;
        }
        return In([.. KvEditorPage.Editor.Menus], id);
    }

    private static void KvRun(string id) => KvItem(id).Run();
    private static void KvSelect(params KvElement[] items) => KvEditorPage.Editor.Select(items);
    private static List<KvElement> KvSel() => [.. KvEditorPage.Editor.Canvas.Selection];
    private static string KvJson() => KvStore.Current.ToJson();
    private static Button KvHeaderUndo() => QuartzShell.Window.TitleBarRight.Query<Button>().Where(b => b.text == "↶").First();

    // ---- drawing

    private static IEnumerator KvCanvasDraw(Action<string, bool, string> check) {
        KvLayoutModel model = KvEditorPage.Model;
        List<KvElement> els = KvEls();
        VisualElement ghosts = KvGhostLayer();
        int feet = KvStore.Current.AllElements(kvFoot).Count;
        List<VisualElement> boxes = KvItemBoxes();
        bool faint = ghosts != null && ghosts.childCount == feet && ghosts.Children().All(g =>
            g.pickingMode == PickingMode.Ignore && Mathf.Abs(g.style.opacity.value - 0.18f) < 0.001f);
        check($"{KvCanvasPage}: one box per element of the tab, and the companion foot tab's elements as ghosts (unpickable, 0.18)",
            els.Count >= 8 && boxes.Count == els.Count && faint, $"elements={els.Count} boxes={boxes.Count} ghosts={ghosts?.childCount} feet={feet} faint={faint}");

        els[1].Hidden = true;
        KvEditorPage.Editor.Refresh();
        yield return Frames(1);
        int dim = KvItemBoxes().Count(b => Mathf.Abs(b.style.opacity.value - 0.35f) < 0.001f);
        float hidden = KvBoxOf(els[1])?.style.opacity.value ?? 0f;
        check($"{KvCanvasPage}: a hidden element's box is at 0.35", dim == 1 && Mathf.Abs(hidden - 0.35f) < 0.001f, $"dim={dim} hidden={hidden}");
        els[1].Hidden = false;

        KvElement key = els.First(e => e.Kind == KvElementKind.Key);
        key.Raw["borderRadius"] = 20;
        key.Raw["borderWidth"] = 5;
        key.Raw["fontSize"] = 30;
        key.Raw["counter"] = new Newtonsoft.Json.Linq.JObject { ["enabled"] = true, ["align"] = "bottom", ["fontSize"] = 16 };
        KvEditorPage.Editor.Refresh();
        yield return Frames(2);
        VisualElement box = KvBoxOf(key);
        IResolvedStyle r = box?.resolvedStyle;
        Label label = box?.Q<Label>("kv-label"), counter = box?.Q<Label>("kv-counter");
        check($"{KvCanvasPage}: a styled key draws its radius 20, border 5, label size 30 and a \"0\" counter inside",
            r != null && Mathf.Abs(r.borderTopLeftRadius - 20f) < 0.01f && Mathf.Abs(r.borderTopWidth - 5f) < 0.5f
            && label != null && Mathf.Abs(label.resolvedStyle.fontSize - 30f) < 0.01f && counter?.text == "0",
            $"box={box != null} radius={r?.borderTopLeftRadius} border={r?.borderTopWidth} font={label?.resolvedStyle.fontSize} counter='{counter?.text}'");

        // Feet hidden (common): no ghosts, no throw, and the missing companion is not written into the document.
        KvDocument doc = KvStore.Current;
        string shownFeet = doc.SelectedFootTab;
        int tabs = doc.Tabs.Count();
        doc.SelectedFootTab = null;
        bool threw = false;
        try {
            KvEditorPage.Editor.Refresh();
        } catch(Exception e) {
            MainCore.Log.Err($"[SelfTest] canvas refresh with no foot tab threw: {e}");
            threw = true;
        }
        yield return Frames(1);
        check($"{KvCanvasPage}: with no foot tab shown the canvas draws no ghosts and adds no tab",
            !threw && model.Ghosts().Count == 0 && KvGhostLayer()?.childCount == 0 && KvStore.Current.Tabs.Count() == tabs,
            $"threw={threw} ghosts={model.Ghosts().Count} layer={KvGhostLayer()?.childCount} tabs={tabs}->{KvStore.Current.Tabs.Count()}");
        doc.SelectedFootTab = shownFeet;
        KvEditorPage.Editor.Refresh();
        yield return Frames(1);
    }

    // ---- drag

    private static IEnumerator KvCanvasDrag(Action<string, bool, string> check) {
        KvLayoutModel model = KvEditorPage.Model;
        KvEditorPage.Editor.Canvas.ResetView();
        KvEditorPage.Editor.Canvas.Clear();
        global::Facet.History.Clear();
        yield return Frames(1);
        List<KvElement> els = KvEls();
        KvElement e = els[0];
        string key = model.Key(e);
        (float x0, float y0) = (e.X, e.Y);
        int before = KvLayoutModel.OverlayRebuildRequests;
        int during = KvDragNow(e, new Vector2(37f, 12f));
        int after = KvLayoutModel.OverlayRebuildRequests;
        KvElement now = model.Find(key);
        Rect got = now == null ? default : model.GetRect(now);
        // On the 5 px grid, or an edge or centre of the box on an edge or centre of another element or a ghost.
        List<Rect> others = [.. els.Where(o => o != e).Select(o => o.Bounds).Concat(model.Ghosts().Select(o => o.Bounds))];
        static float[] Lines(float lo, float size) => [lo, lo + size * 0.5f, lo + size];
        bool Snapped(float v, float size, Func<Rect, (float, float)> axis) =>
            Mathf.Abs(v / 5f - Mathf.Round(v / 5f)) < 0.001f
            || others.Any(o => Lines(v, size).Any(a => Lines(axis(o).Item1, axis(o).Item2).Any(b => Mathf.Abs(a - b) < 0.01f)));
        bool moved = now != null && (now.X != x0 || now.Y != y0) && got.x == now.X && got.y == now.Y
            && Snapped(now.X, now.W, o => (o.x, o.width)) && Snapped(now.Y, now.H, o => (o.y, o.height));
        check($"{KvCanvasPage}: a left drag by (+37, +12) moves the key to a snapped place (grid, or an element's or ghost's edge or centre) that GetRect agrees with",
            moved, $"from=({x0},{y0}) to=({now?.X},{now?.Y}) rect={got}");
        check($"{KvCanvasPage}: the drag asks for no overlay rebuild while it moves and for exactly one at release",
            during == before && after == before + 1, $"before={before} during={during} after={after}");
        string move = global::Facet.Words.Own("Move", QuartzHost.Host);
        string named = global::Facet.History.NextUndoText;
        check($"{KvCanvasPage}: the drag is one undo step named Move", named == move, $"next='{named}' want='{move}'");
        yield return Frames(1);   // a commit counts once a frame
        int r0 = KvLayoutModel.OverlayRebuildRequests;
        global::Facet.History.Undo();
        now = model.Find(key);
        check($"{KvCanvasPage}: undoing the drag puts the key back and asks for one overlay rebuild",
            now != null && now.X == x0 && now.Y == y0 && KvLayoutModel.OverlayRebuildRequests == r0 + 1,
            $"at=({now?.X},{now?.Y}) want=({x0},{y0}) requests={KvLayoutModel.OverlayRebuildRequests - r0}");
        yield return Frames(1);
    }

    // ---- steps live as they are recorded

    private static IEnumerator KvStepsLive(Action<string, bool, string> check) {
        global::Facet.History.Clear();
        yield return Frames(1);
        KvRun("KVI_STAT_KPS");
        yield return Frames(2);
        StepLive(check, "Insert ▸ Stat ▸ KPS", "MENU_KVE_INSERT");
        KvSelect(KvEls()[0]);
        KvRun(KeyLayoutText.Duplicate);
        yield return Frames(2);
        StepLive(check, "Duplicate", "KVI_DUPLICATE");
        global::Facet.History.Undo();
        global::Facet.History.Undo();
        yield return KvBoxesSettle();
    }

    private static void StepLive(Action<string, bool, string> check, string what, string key) {
        string want = QuartzHost.Translate(key), next = global::Facet.History.NextUndoText;
        bool can = global::Facet.History.CanUndo;
        Button undo = KvHeaderUndo();
        check($"{KvCanvasPage}: {what} leaves a live step labelled in the player's language, and the header's undo is enabled",
            can && next == want && want != key && undo != null && undo.enabledSelf, $"can={can} next='{next}' want='{want}' header={undo?.enabledSelf}");
    }

    // ---- insert

    private static IEnumerator KvInsert(Action<string, bool, string> check) {
        var canvas = KvEditorPage.Editor.Canvas;
        List<KvElement> was = KvEls();
        Vector2 centre = canvas.ViewCentre;
        KvRun("KVI_STAT_KPSMAX");
        List<KvElement> added = [.. KvEls().Except(was)];
        KvElement s = added.FirstOrDefault();
        Vector2 at = s == null ? default : s.Bounds.center;
        check($"{KvCanvasPage}: Insert ▸ Stat ▸ Max adds one Max stat centred on the view (±1)",
            added.Count == 1 && s!.Kind == KvElementKind.Stat && s.StatType == "kpsMax" && (at - centre).magnitude <= 1.5f
            && KvSel().SequenceEqual(added),
            $"added={added.Count} kind={s?.Kind} stat={s?.StatType} at={at} centre={centre} selected={KvSel().Count}");
        was = KvEls();
        KvRun("KVI_ADD_KEY");
        added = [.. KvEls().Except(was)];
        bool armed = KeyCaptureService.IsCapturing;
        KeyCaptureService.Cancel();
        string status = KvEditorPage.Editor.Status.Root.Q<Label>()?.text ?? "";
        bool listening = status.StartsWith(QuartzHost.Translate("KVI_LISTENING"));
        check($"{KvCanvasPage}: Insert ▸ Key adds a key and arms its key capture; Escape drops the listening status",
            added.Count == 1 && added[0].Kind == KvElementKind.Key && armed && !listening,
            $"added={added.Count} armed={armed} status='{status}'");
        IList<global::Facet.MenuItem> items = KvEditorPage.Editor.Menus[2].Items;
        List<string> ids = [.. items.Where(i => i != null && !i.IsSeparator).Select(i => i.Id)];
        check($"{KvCanvasPage}: the Insert menu is Key, Stat and Graph, with no Knob",
            ids.SequenceEqual(["KVI_ADD_KEY", "KVI_ADD_STAT", "KVI_ADD_GRAPH"]) && !KvMenuWords(items).Any(w => w.Contains("KNOB")),
            string.Join(",", ids));
        yield return KvBoxesSettle();
    }

    // Every Id and Text in a menu, submenus included.
    private static IEnumerable<string> KvMenuWords(IEnumerable<global::Facet.MenuItem> items) =>
        (items ?? []).Where(i => i != null).SelectMany(i => new[] { i.Id ?? "", i.Text ?? "" }.Concat(KvMenuWords(i.Items)));

    // ---- context menu and right-drag

    private static IEnumerator KvContextMenu(Action<string, bool, string> check) {
        var canvas = KvEditorPage.Editor.Canvas;
        KvElement key = KvEls().First(e => e.Kind == KvElementKind.Key);
        Vector2 p = KvPanel(key.Bounds.center);
        KvSend<PointerDownEvent>(p, 1);
        KvSend<PointerUpEvent>(p, 1);
        yield return Frames(2);
        string[] want = [QuartzHost.Translate("KVI_REBIND"), QuartzHost.Translate("MENU_KVE_CUT"), QuartzHost.Translate("MENU_KVE_TO_FRONT")];
        List<string> missing = [.. want.Where(t => MenuRow(t) == null)];
        check($"{KvCanvasPage}: a right-click on a key opens a menu with Rebind Key, Cut and Bring to Front", missing.Count == 0,
            $"missing={string.Join(",", missing)}");
        global::Facet.Popup.CloseAll();
        yield return Frames(2);

        // On empty stage, in stage px: the pan moves the canvas under the pointer, so every point is fixed first.
        Vector2 s0 = new(8f, 8f), pan = canvas.Pan;
        bool empty = canvas.HitTest(s0) == null;
        Vector2[] at = [.. new[] { 0f, 10f, 20f }.Select(dx => canvas.Stage.LocalToWorld(s0 + new Vector2(dx, 0f)))];
        KvSend<PointerDownEvent>(at[0], 1);
        KvSend<PointerMoveEvent>(at[1], 1);
        KvSend<PointerMoveEvent>(at[2], 1);
        KvSend<PointerUpEvent>(at[2], 1);
        yield return Frames(2);
        Vector2 moved = canvas.Pan - pan;
        bool menu = MenuRow(QuartzHost.Translate("MENU_KVE_PASTE")) != null || MenuRow(QuartzHost.Translate("MENU_KVE_SELECT_ALL")) != null;
        check($"{KvCanvasPage}: a right-drag of 20 px pans by 20 and opens no menu",
            empty && (moved - new Vector2(20f, 0f)).magnitude < 0.01f && !menu, $"empty={empty} pan={moved} menu={menu}");
        global::Facet.Popup.CloseAll();
        canvas.ResetView();
        yield return Frames(1);
    }

    // ---- double-click

    private static IEnumerator KvDoubleClick(Action<string, bool, string> check) {
        List<KvElement> els = KvEls();
        bool Twice(KvElement e) {
            Vector2 p = KvPanel(e.Bounds.center);
            for(int i = 0; i < 2; i++) {
                KvSend<PointerDownEvent>(p, 0);
                KvSend<PointerUpEvent>(p, 0);
            }
            bool armed = KeyCaptureService.IsCapturing;
            KeyCaptureService.Cancel();
            return armed;
        }
        bool key = Twice(els.First(e => e.Kind == KvElementKind.Key));
        yield return Frames(1);
        bool stat = Twice(els.First(e => e.Kind == KvElementKind.Stat));
        check($"{KvCanvasPage}: a double-click on a key arms its capture, on a stat it does not", key && !stat, $"key={key} stat={stat}");
        yield return Frames(1);
    }

    // ---- View ▸ Grid

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void KvPrefs(Action<string, bool, string> check) {
        CoreSettings c = MainCore.Conf;
        bool was = c.KvShowGrid;
        KvRun(KeyLayoutText.Grid);
        bool now = c.KvShowGrid, canvas = KvEditorPage.Editor.Canvas.ShowGrid;
        KvRun(KeyLayoutText.Grid);
        check($"{KvCanvasPage}: View ▸ Grid writes the Grid setting and the canvas's grid", now == !was && canvas == now && c.KvShowGrid == was,
            $"was={was} now={now} canvas={canvas} back={c.KvShowGrid}");
    }
}
#endif
