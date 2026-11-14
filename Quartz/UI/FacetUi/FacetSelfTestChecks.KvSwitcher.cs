#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Game.Stats;
using Quartz.IO;
using Quartz.Overlay;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
using Switcher = PrismLib.UI.Toolkit.KeyLayoutSwitcher<Quartz.Features.KeyViewer.Layout.KvElement>;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The key viewer layout switcher: its first open with the document at the layout limit, its cards, marks and previews, a
/// click and the Hidden card as undo steps, a double-click into either menu, the card menu, + New from Preset, the hub's
/// Layouts button, the hotkey and its rows, a door forced off, and Quartz's shutdown. Every check runs inside KvGuard.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string KvSwitcherPage = KeyViewerPage + ": switcher";
    private static string kvHand2;

    private static IEnumerator KvSwitcherChecks(Action<string, bool, string> check, Action<string> info) =>
        OnPage("keyviewer", KeyViewerPage, () => { }, () => { }, () => KvGuard(check, () => KvSwitcherOnPage(check, info)));

    private static IEnumerator KvSwitcherOnPage(Action<string, bool, string> check, Action<string> info) {
        if(!KvEditorDoor.Usable || !KvEditorDoor.Live) {
            check($"{KvSwitcherPage}: the layout editor is on the page", false, $"usable={KvEditorDoor.Usable} live={KvEditorDoor.Live}");
            yield break;
        }
        CoreSettings c = MainCore.Conf;
        bool newMenu = c.ExperimentalNewMenu;
        c.ExperimentalNewMenu = true;   // Edit follows the player's menu choice: both are checked
        OverlayPreview.Lease lease = OverlayPreview.Take(KeyViewerOverlay.FontKey);
        try {
            yield return KvSwitcherFirstOpen(check, info);   // first: it times the switcher's first open
            KvScratchTabs();
            kvHand2 = KvStore.Current.NewTabId();
            KvStore.Current.EnsureTab(kvHand2, "Self Test Hands 2");
            KvMigration.GenerateStockTab(KvStore.Current, kvHand2, 6);
            KvEditorPage.Editor.Refresh();
            yield return KvBoxesSettle();
            yield return KvSwitcherCards(check);
            yield return KvSwitcherClicks(check);
            yield return KvSwitcherMenu(check);
            yield return KvSwitcherNew(check);
            yield return KvSwitcherEdit(check);
            yield return KvSwitcherHub(check);
            yield return KvSwitcherHotkeyChecks(check);
            yield return KvSwitcherForcedOff(check);
            yield return KvSwitcherShutdown(check);
        } finally {
            KvEditorDoor.ForceGone = false;
            FacetBridge.HideKvSwitcher();
            c.ExperimentalNewMenu = newMenu;
            KvSwitcherHotkey.TrySet(c.KvSwitcherModifier, c.KvSwitcherKey);   // clears the refused line; KvGuard puts the chord back
            lease?.Release();
            global::Facet.Popup.CloseAll();
        }
    }

    // ---- helpers

    private static Switcher KvSw => KvEditorPage.Switcher;
    private static bool KvSwShows => KvSw is { Visible: true };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object KvSwField(object of, string name) =>
        of?.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(of);

    private static object KvSwGrid(bool hands) => KvSwField(KvSw, hands ? "_hands" : "_feet");

    // The switcher's own card records, in order; Layout is null for Feet's Hidden card.
    private static List<object> KvSwItems(bool hands) => [.. ((IList)KvSwField(KvSw, hands ? "_handCards" : "_footCards")).Cast<object>()];
    private static string KvSwLayout(object card) => (string)card.GetType().GetField("Layout").GetValue(card);
    private static object KvSwItem(bool hands, string layout) => KvSwItems(hands).FirstOrDefault(i => KvSwLayout(i) == layout);

    // The card elements, in the grid's order (one per record).
    private static List<VisualElement> KvSwCards(bool hands) => (List<VisualElement>)KvSwField(KvSwGrid(hands), "_cards") ?? [];

    private static VisualElement KvSwCard(bool hands, string layout) {
        List<object> items = KvSwItems(hands);
        List<VisualElement> cards = KvSwCards(hands);
        int i = items.FindIndex(it => KvSwLayout(it) == layout);
        return i >= 0 && i < cards.Count ? cards[i] : null;
    }

    private static Delegate KvSwOption(bool hands, string name) {
        object o = KvSwField(KvSwGrid(hands), "_o");
        return (Delegate)o?.GetType().GetProperty(name)?.GetValue(o);
    }

    private static IList<global::Facet.MenuItem> KvSwMenu(bool hands, string layout) =>
        (IList<global::Facet.MenuItem>)KvSwOption(hands, "Menu")?.DynamicInvoke(KvSwItem(hands, layout));

    private static bool KvSwMarked(VisualElement card) =>
        card?.Children().SelectMany(ch => ch.Children()).FirstOrDefault(e => e.name.EndsWith("-mark", StringComparison.Ordinal)) is { } mark
        && mark.style.display.value == DisplayStyle.Flex;

    // A left press and release on the card's centre, as the pointer gives them.
    private static void KvSwClick(VisualElement card) {
        if(card == null) return;
        Vector2 at = card.worldBound.center;
        KvSwPointer<PointerDownEvent>(card, at);
        KvSwPointer<PointerUpEvent>(card, at);
    }

    private static void KvSwPointer<T>(VisualElement target, Vector2 at) where T : PointerEventBase<T>, new() {
        using T ev = Pooled<T>();
        typeof(PointerEventBase<T>).GetProperty("button").SetValue(ev, 0);
        typeof(PointerEventBase<T>).GetProperty("position").SetValue(ev, (Vector3)at);
        ev.target = target;
        target.SendEvent(ev);
    }

    // The overlay's boxes drawn from this layout's elements.
    private static int KvOverlayBoxesOf(string tab) {
        var list = (IList)typeof(KeyViewerOverlay).GetField("boxes", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var mine = new HashSet<KvElement>(KvStore.Current.AllElements(tab));
        int n = 0;
        foreach(object b in list)
            if(b.GetType().GetField("Source").GetValue(b) is KvElement e && mine.Contains(e)) n++;
        return n;
    }

    private static IEnumerator KvSwOpen() {
        if(KvEditorPage.Editor?.Menu.Left.Q<Button>("kle-layouts") is { } b) Submit(b);
        yield return Frames(2);
    }

    // Facet's rows show or hide on a refresh pass, not every frame.
    private static IEnumerator KvSeconds(float s) {
        float until = Time.realtimeSinceStartup + s;
        while(Time.realtimeSinceStartup < until) yield return null;
    }

    private static Transform KvHub => UICore.CanvasObj.transform.Find("ReorganizeHub");

    // ---- the first open, at the layout limit with a 108-key layout

    private static IEnumerator KvSwitcherFirstOpen(Action<string, bool, string> check, Action<string> info) {
        check($"{KvSwitcherPage}: no switcher exists before its first open", KvSw == null, $"switcher={KvSw != null}");
        KvDocument doc = KvStore.Current;
        List<string> added = [];
        string big = doc.NewTabId();
        doc.EnsureTab(big, "Self Test 108");
        KvMigration.GenerateStockTab(doc, big, KvPresets.Style108);
        added.Add(big);
        while(doc.CustomTabCount < KvDocument.MaxCustomTabs) {
            string t = doc.NewTabId();
            doc.EnsureTab(t, "Self Test Fill " + added.Count);
            KvMigration.GenerateStockTab(doc, t, 4);
            added.Add(t);
        }
        yield return Frames(1);
        var sw = Stopwatch.StartNew();
        FacetBridge.OpenKvSwitcher();
        double ms = sw.Elapsed.TotalMilliseconds;
        int cards = KvSw == null ? 0 : KvSwCards(true).Count + KvSwCards(false).Count;
        info($"[perf] key viewer switcher open {ms:0.0} ms ({cards} cards, one of 108 keys)");
        check($"{KvSwitcherPage}: the first open with {KvM.Layouts(LayoutGroup.Hands).Count} hand layouts, one of 108 keys, takes 100 ms or less",
            KvSwShows && cards >= 30 && ms <= 100.0, $"ms={ms:0.0} cards={cards} shows={KvSwShows}");
        yield return Frames(2);
        FacetBridge.HideKvSwitcher();
        foreach(string t in added) doc.RemoveTab(t);
        global::Facet.History.Clear();
        KvEditorPage.Editor.Refresh();
        yield return Frames(2);
    }

    // ---- cards, marks and previews

    private static IEnumerator KvSwitcherCards(Action<string, bool, string> check) {
        yield return KvSwOpen();
        int hands = KvM.Layouts(LayoutGroup.Hands).Count, feet = KvM.Layouts(LayoutGroup.Feet).Count;
        int hc = KvSwCards(true).Count, fc = KvSwCards(false).Count;
        check($"{KvSwitcherPage}: the menu bar's Layouts opens the switcher with a card per hand layout, and per foot layout plus Hidden",
            KvSwShows && hc == hands && fc == feet + 1, $"shows={KvSwShows} hands={hc}/{hands} feet={fc}/{feet + 1}");
        string shownHand = KvM.Shown(LayoutGroup.Hands), shownFoot = KvM.Shown(LayoutGroup.Feet);
        bool marks = true;
        string d = "";
        foreach(bool h in new[] { true, false }) {
            List<object> items = KvSwItems(h);
            List<VisualElement> cards = KvSwCards(h);
            for(int i = 0; i < items.Count && i < cards.Count; i++) {
                string l = KvSwLayout(items[i]);
                bool want = l == (h ? shownHand : shownFoot);
                if(KvSwMarked(cards[i]) != want) { marks = false; d += $" {l ?? "hidden"}:{!want}"; }
            }
        }
        check($"{KvSwitcherPage}: the cards on screen carry the active mark, and no other", marks, d);
        bool thumbs = true;
        d = "";
        foreach(bool h in new[] { true, false }) {
            List<object> items = KvSwItems(h);
            List<VisualElement> cards = KvSwCards(h);
            for(int i = 0; i < items.Count && i < cards.Count; i++) {
                string l = KvSwLayout(items[i]);
                VisualElement thumb = cards[i].Q(name: (h ? "kle-switcher-hands" : "kle-switcher-feet") + "-thumb");
                bool ok = l == null ? thumb?.childCount == 0
                    : thumb?.childCount == 1 && thumb[0].name == "kle-thumb" && thumb[0].generateVisualContent != null;
                if(!ok) { thumbs = false; d += $" {l ?? "hidden"}:{thumb?.childCount}"; }
            }
        }
        var boxes = new List<PrismLib.UI.Toolkit.KeyLayoutBox>();
        KvM.Thumb(kvHand, boxes);
        check($"{KvSwitcherPage}: every layout card's preview is one painted element of its boxes; Hidden has none",
            thumbs && boxes.Count == KvStore.Current.AllElements(kvHand).Count, $"{d} boxes={boxes.Count}");
    }

    // ---- a click shows a layout as one step; Hidden hides the feet

    private static IEnumerator KvSwitcherClicks(Action<string, bool, string> check) {
        global::Facet.History.Clear();
        KvSwClick(KvSwCard(true, kvHand2));
        bool atOnce = KvStore.Current.SelectedTab == kvHand2;
        int want = KvStore.Current.AllElements(kvHand2).Count, boxes = -1;
        float until = Time.realtimeSinceStartup + 0.5f;
        while(Time.realtimeSinceStartup < until && (boxes = KvOverlayBoxesOf(kvHand2)) != want) yield return null;
        check($"{KvSwitcherPage}: a click on a hand card shows it at once, and the overlay rebuilds with it within 0.5 s",
            atOnce && boxes == want, $"atOnce={atOnce} boxes={boxes}/{want}");
        yield return Frames(2);   // the editor sees the new tab on its own tick
        bool canvas = KvM.Editing == kvHand2 && KvItemBoxes().Count == KvEls().Count && KvEls().Count == want;
        KvElement e = KvEls().FirstOrDefault();
        Rect was = e?.Bounds ?? default;
        if(e != null) KvDragNow(e, new Vector2(40f, 0f));
        yield return Frames(1);
        bool moved = e != null && KvStore.Current.AllElements(kvHand2).Contains(e) && e.Bounds.x > was.x;
        check($"{KvSwitcherPage}: after a card click, with no manual refresh, the canvas edits that layout and a drag lands in the live document",
            canvas && moved, $"canvas={canvas} editing={KvM.Editing} moved={moved}");
        global::Facet.History.Undo();
        global::Facet.History.Undo();
        yield return Frames(2);
        check($"{KvSwitcherPage}: undo walks back through the drag, then the click", KvStore.Current.SelectedTab == kvHand,
            $"shown={KvStore.Current.SelectedTab}");

        global::Facet.History.Clear();
        KvSwClick(KvSwCard(false, null));
        bool hidden = KvStore.Current.SelectedFootTab == null;
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvSwitcherPage}: the Hidden card hides the feet, and undo shows them again",
            hidden && KvStore.Current.SelectedFootTab == kvFoot, $"hidden={hidden} foot={KvStore.Current.SelectedFootTab}");

        // A press on the editor's canvas raises the menu over the switcher, so the editor's Ctrl+D acts.
        global::Facet.History.Clear();
        KvEditorPage.Editor.Refresh();
        yield return Frames(2);
        e = KvEls().FirstOrDefault();
        int count = KvEls().Count;
        if(e != null) {
            KvSend<PointerDownEvent>(KvPanel(e.Bounds.center), 0);
            KvSend<PointerUpEvent>(KvPanel(e.Bounds.center), 0);
        }
        yield return Frames(1);
        KvSelect(e);
        KvEditorPage.Editor.Tick(k => k == KeyCode.D, k => k is KeyCode.LeftControl or KeyCode.D, true, false, false);
        yield return Frames(1);
        check($"{KvSwitcherPage}: with the switcher open, a press on the editor's canvas then Ctrl+D duplicates",
            KvSwShows && KvEls().Count == count + 1, $"shows={KvSwShows} count={count}->{KvEls().Count}");
        if(KvEls().Count != count) global::Facet.History.Undo();
        yield return Frames(1);
    }

    // ---- the card menu: Delete, Rename, Export

    private static IEnumerator KvSwitcherMenu(Action<string, bool, string> check) {
        List<string> ids = [.. (KvSwMenu(true, kvHand2) ?? []).Select(i => i?.Id)];
        check($"{KvSwitcherPage}: a card's menu is Edit, Rename, Duplicate, Delete and Export as DM Note Config",
            ids.SequenceEqual([KeyLayoutText.EditLayout, KeyLayoutText.Rename, KeyLayoutText.Duplicate, KeyLayoutText.Delete, KeyLayoutText.ExportDm]),
            string.Join(",", ids));

        global::Facet.History.Clear();
        string shape = KvShape(kvHand2);
        KvIn(KvSwMenu(true, kvHand2), KeyLayoutText.Delete)?.Run?.Invoke();
        yield return Frames(2);
        Button ok = KvConfirmButton("confirm-ok");
        if(ok != null) Submit(ok);
        yield return Frames(2);
        bool gone = !KvStore.Current.HasTab(kvHand2) && KvSwCard(true, kvHand2) == null;
        global::Facet.History.Undo();
        yield return Frames(2);
        check($"{KvSwitcherPage}: Delete through the confirm removes the layout and its card; undo brings it back with its elements",
            ok != null && gone && KvStore.Current.HasTab(kvHand2) && KvShape(kvHand2) == shape && KvSwCard(true, kvHand2) != null,
            $"ok={ok != null} gone={gone} back={KvStore.Current.HasTab(kvHand2)}");
        global::Facet.Popup.CloseAll();

        global::Facet.History.Clear();
        object grid = KvSwGrid(true);
        grid.GetType().GetMethod("BeginRename").Invoke(grid, [KvSwItem(true, kvHand2)]);
        yield return Frames(2);
        TextField field = KvSwCard(true, kvHand2)?.Q<TextField>("kle-switcher-hands-rename");
        if(field != null) {
            field.value = "Self Test Switcher Renamed";
            using KeyDownEvent ev = Pooled<KeyDownEvent>();
            typeof(KeyboardEventBase<KeyDownEvent>).GetProperty("keyCode").SetValue(ev, KeyCode.Return);
            ev.target = field;
            field.SendEvent(ev);
        }
        yield return Frames(1);
        check($"{KvSwitcherPage}: Rename on a card names the layout", field != null && KvStore.Current.TabName(kvHand2) == "Self Test Switcher Renamed",
            $"field={field != null} name={KvStore.Current.TabName(kvHand2)}");

        // Export of a card that is not shown writes that layout, and changes neither the selection nor the history.
        global::Facet.History.Clear();
        string path = Path.Combine(Path.GetTempPath(), "quartz-selftest-switcher.json");
        (string shown, string editing, string next) = (KvStore.Current.SelectedTab, KvM.Editing, global::Facet.History.NextUndoText);
        bool written = KvM.ExportPath(kvHand2, path, out string said);
        string json = File.Exists(path) ? File.ReadAllText(path) : "";
        bool same = KvStore.Current.SelectedTab == shown && KvM.Editing == editing && global::Facet.History.NextUndoText == next;
        KvM.ExportPath(shown, path, out _);   // the shown layout writes something else
        bool differs = File.Exists(path) && File.ReadAllText(path) != json;
        try { File.Delete(path); } catch(Exception e) { Diag.Ignore(e); }
        check($"{KvSwitcherPage}: exporting a card that is not shown writes that layout and leaves the shown tab, the edited tab and the history as they were",
            written && json.Length > 0 && differs && shown != kvHand2 && same,
            $"written={written} differs={differs} same={same} len={json.Length} shown={KvStore.Current.SelectedTab} editing={KvM.Editing} said={said}");
    }

    // ---- + New from Preset

    private static IEnumerator KvSwitcherNew(Action<string, bool, string> check) {
        global::Facet.History.Clear();
        yield return Frames(1);   // Committed counts once a frame
        int cards = KvSwCards(true).Count, rebuilds = KvLayoutModel.OverlayRebuildRequests;
        List<string> hands = KvTabs(LayoutGroup.Hands);
        var items = (IList<global::Facet.MenuItem>)KvSwOption(true, "Add")?.DynamicInvoke();
        KvIn(items, "KEYVIEWER_STYLE_8")?.Run?.Invoke();
        yield return Frames(1);
        string t = KvTabs(LayoutGroup.Hands).Except(hands).FirstOrDefault();
        bool added = t != null && KvSwCards(true).Count == cards + 1 && KvStore.Current.AllElements(t).Count(e => e.Kind == KvElementKind.Key) == 8;
        bool step = global::Facet.History.NextUndoText == KvTr("MENU_KVE_NEW_FROM_PRESET");
        int rose = KvLayoutModel.OverlayRebuildRequests - rebuilds;
        global::Facet.History.Undo();
        yield return Frames(2);
        check($"{KvSwitcherPage}: + New from Preset ▸ 8 Keys adds a card as one committed step named New from Preset; undo removes the layout and its card",
            added && step && rose == 1 && !KvStore.Current.HasTab(t) && KvSwCards(true).Count == cards,
            $"added={added} step={step} next='{(global::Facet.History.NextUndoText)}' rose={rose} cards={KvSwCards(true).Count}/{cards}");
    }

    // ---- Edit: the new menu, or the classic one for players who turned it off

    private static IEnumerator KvSwitcherEdit(Action<string, bool, string> check) {
        QuartzShell.Hide();
        yield return Frames(1);
        FacetBridge.OpenKvSwitcher();
        yield return Frames(2);
        VisualElement card = KvSwCard(true, kvHand2);
        KvSwClick(card);
        KvSwClick(card);
        yield return Frames(3);
        check($"{KvSwitcherPage}: a double-click hides the switcher, opens the new menu on the Key Viewer page and edits that layout",
            !KvSwShows && FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == KeyViewerPage && KvM.Editing == kvHand2,
            $"shows={KvSwShows} menu={FacetBridge.NewMenuVisible()} page={QuartzShell.CurrentPage} editing={KvM.Editing}");

        CoreSettings c = MainCore.Conf;
        c.ExperimentalNewMenu = false;
        try {
            FacetBridge.OpenKvSwitcher();
            yield return Frames(2);
            KvIn(KvSwMenu(true, kvHand), KeyLayoutText.EditLayout)?.Run?.Invoke();
            yield return Frames(3);
            int state = NavRegistry.StateFor(KeyViewerPage);
            check($"{KvSwitcherPage}: with the new menu turned off, Edit opens the classic menu on the Key Viewer page with that layout shown",
                !KvSwShows && UICore.IsOpen && UICore.CurrentMenuState == state && !FacetBridge.NewMenuVisible() && KvStore.Current.SelectedTab == kvHand,
                $"open={UICore.IsOpen} state={UICore.CurrentMenuState}/{state} menu={FacetBridge.NewMenuVisible()} shown={KvStore.Current.SelectedTab}");
        } finally {
            c.ExperimentalNewMenu = true;
            Quartz.UI.MenuHandOff.ReturnTo = Quartz.UI.MenuHandOff.Menu.None;
            if(UICore.IsOpen) UICore.Close(true);
        }
        yield return KvBackOnPage();
    }

    private static IEnumerator KvBackOnPage() {
        Quartz.UI.MenuHandOff.ReturnTo = Quartz.UI.MenuHandOff.Menu.None;
        Quartz.UI.MenuHandOff.FromHub = false;
        if(UICore.IsOpen) UICore.Close(true);
        yield return Frames(2);
        FacetBridge.OpenNewMenu(KeyViewerPage);
        yield return Frames(3);
    }

    // ---- the hub's Layouts button

    private static IEnumerator KvSwitcherHub(Action<string, bool, string> check) {
        FacetBridge.HideKvSwitcher();
        QuartzShell.Hide();
        UICore.OpenToHub();
        float until = Time.realtimeSinceStartup + 0.4f;   // the hub's fade in ends
        while(Time.realtimeSinceStartup < until) yield return null;
        Vector2 size = ((RectTransform)KvHub.transform).sizeDelta;
        bool clicked = ClickLabel(KvHub, MainCore.Tr.Get("MENU_KVE_LAYOUTS", "Layouts"));
        yield return Frames(2);
        var group = KvHub.GetComponent<CanvasGroup>();
        bool under = group.alpha == 0f && !group.blocksRaycasts;
        VisualElement root = ((global::Facet.Surface)KvSwField(KvSw, "_surface"))?.Root;
        VisualElement window = root?.Q(name: KeyLayoutText.Switcher) ?? root?.Children().FirstOrDefault();
        VisualElement picked = window?.panel?.Pick(window.worldBound.center);
        bool ours = picked != null && root!.Contains(picked);
        check($"{KvSwitcherPage}: the hub has a Layouts button (260 x 364) that opens the switcher; the hub hides under it, and the switcher's centre picks the switcher",
            clicked && size == new Vector2(260f, 364f) && KvSwShows && under && ours,
            $"clicked={clicked} size={size} shows={KvSwShows} alpha={group.alpha} rays={group.blocksRaycasts} picked={picked?.name}");
        FacetBridge.HideKvSwitcher();
        yield return Frames(2);
        check($"{KvSwitcherPage}: hiding the switcher leaves the hub, shown again",
            AtHub() && group.alpha == 1f && group.blocksRaycasts, $"atHub={AtHub()} alpha={group.alpha} rays={group.blocksRaycasts}");
        UICore.ExitReorganize();
        yield return KvBackOnPage();
    }
}
#endif
