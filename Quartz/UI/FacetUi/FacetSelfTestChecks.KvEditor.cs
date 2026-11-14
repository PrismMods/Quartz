#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.IO;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Key Viewer page's layout editor: its column beside the settings, pinned while they scroll and stacked when narrow; no
/// classic-editor row; the menu bar's titles, items and tools; every label id's Lang key; the status line naming the tab; a
/// stale edited tab after a foreign replace; and the fallback card when PrismLib.UI is unusable. Every check runs inside
/// KvGuard, which puts the player's layout and settings back.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string KvScratchTab = "quartz-selftest-layout", KvScratchName = "Self Test Layout";

    /// <summary>Runs body with the player's key viewer layout, settings and the editor's own settings kept, and History
    /// cleared before and after, so a failing check's undo never reaches another check's step or the player's. After, the
    /// layout must be exactly what it was.</summary>
    internal static IEnumerator KvGuard(Action<string, bool, string> check, Func<IEnumerator> body) {
        KeyViewerOverlay.EnsureConf();
        KeyViewerOverlay.FlushCounts(true);
        string saved = KvStore.Current.ToJson();
        JToken conf = KeyViewerOverlay.Conf.Serialize();
        CoreSettings c = MainCore.Conf;
        (bool snap, bool grid, bool guides) = (c.KvSnapToGrid, c.KvShowGrid, c.KvShowGuides);
        (int switcherKey, int switcherModifier) = (c.KvSwitcherKey, c.KvSwitcherModifier);
        var windows = new Dictionary<string, string>(c.FacetWindows);
        global::Facet.History.Clear();
        try {
            yield return body();
        } finally {
            KvStore.Replace(KvDocument.Parse(saved));
            KeyViewerOverlay.Conf.Deserialize(conf);
            (c.KvSnapToGrid, c.KvShowGrid, c.KvShowGuides) = (snap, grid, guides);
            (c.KvSwitcherKey, c.KvSwitcherModifier) = (switcherKey, switcherModifier);
            c.FacetWindows.Clear();
            foreach(var kv in windows) c.FacetWindows[kv.Key] = kv.Value;
            MainCore.ConfMgr.RequestSave();
            KeyViewerOverlay.Save();
            KeyViewerOverlay.Rebuild();
            global::Facet.History.Clear();
        }
        string now = KvStore.Current.ToJson();
        check("key viewer editor checks put the player's layout back exactly", now == saved, $"saved={saved.Length} now={now.Length}");
    }

    private static IEnumerator KvEditorPageChecks(Action<string, bool, string> check, Action<string> info) =>
        OnPage("keyviewer", KeyViewerPage, () => { }, () => { }, () => KvGuard(check, () => KvEditorOnPage(check, info)));

    private static IEnumerator KvEditorOnPage(Action<string, bool, string> check, Action<string> info) {
        if(!KvEditorDoor.Usable || !KvEditorDoor.Live) {
            check($"{KeyViewerPage}: the layout editor is on the page", false, $"usable={KvEditorDoor.Usable} live={KvEditorDoor.Live}");
            yield break;
        }
        yield return KvColumns(check);
        check($"{KeyViewerPage}: the page has no classic-editor row", Body.Q(name: "MENU_KV_EDITOR") == null && Body.Q(name: "MENU_KV_EDITOR-input") == null,
            $"row={Body.Q(name: "MENU_KV_EDITOR") != null}");
        check($"{KeyViewerPage}: the editor's menu bar has File, Edit, Insert, Arrange and View, Layouts at its left, and undo, redo, Snap "
            + "and the zoom (100% after a reset) at its right; Edit, Arrange and View list their items in the spec's order", KvMenus(out string d), d);
        check($"{KeyViewerPage}: View's Zoom In shortcut \"+\" parses to the = key with no modifier", KvZoomInKey(out d), d);
        check($"{KeyViewerPage}: every editor label id maps to a Lang key in en-US and ko-KR, and the model labels with it", KvLabelKeys(out d), d);
        check($"{KeyViewerPage}: the status line names the edited tab by its name", KvStatusNamesTab(out d), d);
        yield return Frames(2);
        check($"{KeyViewerPage}: after a replace that drops the edited tab, the editor edits the selected tab and the tab is not written back",
            KvPhantomTab(out d), d);
        yield return Frames(2);
        yield return KvTickCost(check, info);
        yield return KvFallback(check);
    }

    // The editor's per-frame tick allocates nothing while the page shows, and does not run at all once the menu is hidden on it.
    private static IEnumerator KvTickCost(Action<string, bool, string> check, Action<string> info) {
        KvDocument doc = KvStore.Current;
        doc.EnsureTab(KvScratchTab, KvScratchName);
        doc.SetFootTab(KvScratchTab, true);   // the hash reads every tab's foot flag
        yield return Frames(2);
        long junk = KvHeapGrowth(() => { for(int i = 0; i < KvProbeRuns; i++) kvSink = new byte[128]; });
        kvSink = null;
        long shown = KvHeapGrowth(() => {
            for(int i = 0; i < KvProbeRuns; i++) { KvEditorDoor.Tick(); _ = doc.StructureHash(); }   // the hash is memoised per frame, so call it too
        });
        info($"[perf] key viewer editor tick: heap grew {shown} bytes over {KvProbeRuns} shown ticks and structure hashes ({KvProbeRuns} x 128-byte arrays grew it {junk})");
        check($"{KeyViewerPage}: the heap probe sees allocations", junk > 0, $"{junk} bytes");
        check($"{KeyViewerPage}: {KvProbeRuns} editor ticks and structure hashes with the page shown allocate nothing per tick", shown < KvProbeSlack, $"{shown} bytes");
        int runs = KvEditorPage.TickRuns;
        long hidden;
        QuartzShell.Surface.Visible = false;
        try {
            hidden = KvHeapGrowth(() => { for(int i = 0; i < KvProbeRuns; i++) FacetBridge.TickKvEditor(); });
        } finally {
            QuartzShell.Surface.Visible = true;
        }
        info($"[perf] key viewer editor tick, menu hidden on the page: heap grew {hidden} bytes over {KvProbeRuns} frames' ticks");
        check($"{KeyViewerPage}: with the menu hidden on the page, the editor does not tick and allocates nothing per frame",
            KvEditorPage.TickRuns == runs && hidden < KvProbeSlack, $"runs={KvEditorPage.TickRuns - runs} grew={hidden}");
        yield return Frames(2);
    }

    // Boehm refills small objects from partly used blocks first, which the heap size does not show; 2000 x 128 bytes read 0 here.
    private const int KvProbeRuns = 20000;
    // The heap is the whole process's, so another thread can add a block or two; one allocation per tick (16 bytes or more)
    // would add over 300 KB.
    private const long KvProbeSlack = 64 * 1024;
    private static object kvSink;
    // Heap growth over body with the collector paused (the game's Boehm GC reports no per-thread allocation count).
    private static long KvHeapGrowth(Action body) {
        var mode = UnityEngine.Scripting.GarbageCollector.GCMode;
        try {
            UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Disabled;
            long a = GC.GetTotalMemory(false);
            body();
            return GC.GetTotalMemory(false) - a;
        } finally {
            UnityEngine.Scripting.GarbageCollector.GCMode = mode;
        }
    }

    // The two columns: the editor right, 36% left; pinned while the page scrolls; the inspector under the canvas; stacked when narrow.
    private static IEnumerator KvColumns(Action<string, bool, string> check) {
        VisualElement root = Body.Q(name: "kle-root"), right = root?.parent, left = right?.parent?[0], bar = Body.Q(name: "kle-menubar");
        if(root == null || right == null || left == null || left == right) {
            check($"{KeyViewerPage}: the editor sits in the right column", false, $"root={root != null}");
            yield break;
        }
        float lw = left.layout.width, rw = right.layout.width, share = lw / (lw + rw);
        check($"{KeyViewerPage}: the editor's menu bar is in the right column, and the left column takes 36% (±2) of the two",
            bar != null && right.Contains(bar) && Mathf.Abs(share - 0.36f) <= 0.02f, $"bar={bar != null} left={lw:0} right={rw:0} share={share:0.000}");

        KvCanvasBounds(out Rect pane, out float stage);
        Rect insp = Body.Q(name: "kle-inspector")?.worldBound ?? default, col = right.worldBound;
        check($"{KeyViewerPage}: the inspector sits in the right column under the canvas, whose stage is at least 200 tall",
            insp.width > 0f && insp.xMin >= col.xMin - 1f && insp.xMax <= col.xMax + 1f && insp.yMin >= pane.yMax - 1f && stage >= 199f,
            $"inspector={insp} column={col} pane={pane} stage={stage:0}");

        ScrollView sv = ContentScroll;
        Vector2 was = sv.scrollOffset;
        float y0 = 0f, y1 = 0f, high = sv.verticalScroller.highValue;
        try {
            sv.scrollOffset = new Vector2(0f, 100f);
            yield return Frames(3);
            y0 = root.worldBound.y;
            sv.scrollOffset = new Vector2(0f, 400f);
            yield return Frames(3);
            y1 = root.worldBound.y;
        } finally {
            sv.scrollOffset = was;
        }
        check($"{KeyViewerPage}: scrolling the settings 300 px leaves the editor where it is (±1)", high >= 400f && Mathf.Abs(y1 - y0) <= 1f,
            $"scrollable={high:0} y={y0:0.0}->{y1:0.0}");
        yield return Frames(2);

        bool Stacked() => right.worldBound.yMin >= left.worldBound.yMax - 1f;
        Rect rect = QuartzShell.Window.Rect;
        try {
            float columns = right.parent.layout.width, cut = (columns - 700f) * QuartzShell.Window.Scale;
            QuartzShell.Window.Rect = new Rect(rect.x, rect.y, rect.width - cut, rect.height);
            int frames = 0;
            for(; frames < 10 && !Stacked(); frames++) yield return null;
            check($"{KeyViewerPage}: narrowed below 760, the editor stacks under the settings", Stacked(),
                $"frames={frames} width={right.parent.layout.width:0}");
        } finally {
            QuartzShell.Window.Rect = rect;
        }
        yield return Frames(3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void KvCanvasBounds(out Rect pane, out float stage) {
        var canvas = KvEditorPage.Editor.Canvas;
        pane = canvas.Root.worldBound;
        stage = canvas.Stage.worldBound.height;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KvMenus(out string detail) {
        var ed = KvEditorPage.Editor;
        string[] titles = ["MENU_KVE_FILE", "MENU_KVE_EDIT", "MENU_KVE_INSERT", "KVI_SEC_ARRANGE", "MENU_KVE_VIEW"];
        string[] texts = [.. ed.Menus.Select(m => m.Text)];
        HashSet<string> shown = [.. ed.Menu.Root.Query<TextElement>().ToList().Select(t => t.text)];
        bool named = texts.SequenceEqual(titles) && titles.All(k => shown.Contains(QuartzHost.Translate(k)));
        bool layouts = ed.Menu.Left.Q(name: "kle-layouts") != null;
        ed.Canvas.ResetView();
        List<string> tools = [.. ed.Menu.Tools.Root.Query<TextElement>().ToList().Select(t => t.text)];
        bool toolsOk = tools.Contains("↶") && tools.Contains("↷") && tools.Contains(QuartzHost.Translate("KVI_SNAP")) && tools.Contains("100%");
        string Ids(int i) => string.Join(",", ed.Menus[i].Items.Where(m => m != null && !m.IsSeparator).Select(m => m.Id));
        string edit = string.Join(",", KeyLayoutText.Undo, KeyLayoutText.Redo, KeyLayoutText.Cut, KeyLayoutText.Copy, KeyLayoutText.Paste,
            KeyLayoutText.Duplicate, KeyLayoutText.Delete, KeyLayoutText.CopyStyle, KeyLayoutText.PasteStyle, KeyLayoutText.SelectAll,
            KeyLayoutText.Rebind, KeyLayoutText.RebindInOrder);
        string arrange = string.Join(",", KeyLayoutText.ToFront, KeyLayoutText.ToBack, KeyLayoutText.AlignLeft, KeyLayoutText.AlignRight,
            KeyLayoutText.AlignTop, KeyLayoutText.AlignBottom, KeyLayoutText.SpreadH, KeyLayoutText.SpreadV, KeyLayoutText.Hide, KeyLayoutText.Show);
        string view = string.Join(",", KeyLayoutText.ZoomIn, KeyLayoutText.ZoomOut, KeyLayoutText.ZoomReset, KeyLayoutText.Fit, KeyLayoutText.Snap,
            KeyLayoutText.Grid, KeyLayoutText.Guides);
        bool order = Ids(1) == edit && Ids(3) == arrange && Ids(4) == view;
        detail = $"titles={string.Join(",", texts)} named={named} layouts={layouts} tools={string.Join("|", tools)} edit={Ids(1)} arrange={Ids(3)} view={Ids(4)}";
        return named && layouts && toolsOk && order;
    }

    // A bare "+" is the = key; a parse that failed would show the row's text and run nothing.
    private static bool KvZoomInKey(out string detail) {
        global::Facet.MenuItem item = KvEditorPage.Editor.Menus[4].Items.FirstOrDefault(m => m?.Id == KeyLayoutText.ZoomIn);
        const System.Reflection.BindingFlags Inner = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object Field(string name) => item == null ? null : typeof(global::Facet.MenuItem).GetField(name, Inner)?.GetValue(item);
        object key = Field("Key"), ctrl = Field("Ctrl"), shift = Field("Shift"), alt = Field("Alt");
        detail = $"shortcut='{item?.Shortcut}' key={key} ctrl={ctrl} shift={shift} alt={alt}";
        return item?.Shortcut == "+" && key is KeyCode.Equals && ctrl is false && shift is false && alt is false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KvLabelKeys(out string detail) {
        KvLayoutModel model = KvEditorPage.Model;
        List<string> bad = [.. KeyLayoutText.Ids.Where(id => KvLabels.KeyFor(id) is not { } key || MainCore.Tr.GetForLanguage(key, "en-US", null) == null
            || MainCore.Tr.GetForLanguage(key, "ko-KR", null) == null || model.Label(id, KeyLayoutText.English(id)) != key)];
        detail = $"ids={KeyLayoutText.Ids.Count} mapped={KvLabels.Map.Count} bad={string.Join(",", bad)}";
        return bad.Count == 0 && KvLabels.Map.Count == KeyLayoutText.Ids.Count;
    }

    // A scratch tab whose name differs from its id, shown and edited.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KvStatusNamesTab(out string detail) {
        KvLayoutModel model = KvEditorPage.Model;
        KvStore.Current.EnsureTab(KvScratchTab, KvScratchName);
        model.Show(LayoutGroup.Hands, KvScratchTab);
        KvEditorPage.Editor.Refresh();
        string want = string.Format(QuartzHost.Translate("KEYVIEWER_EDITOR_STATUS"), 0, KvScratchName), got = model.StatusText();
        detail = $"editing={model.Editing} status='{got}' want='{want}'";
        return model.Editing == KvScratchTab && got == want;
    }

    // The scratch tab is being edited when something else replaces the document with one that lacks it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KvPhantomTab(out string detail) {
        KvLayoutModel model = KvEditorPage.Model;
        KvDocument doc = KvStore.Current;
        doc.RemoveTab(KvScratchTab);
        string without = doc.ToJson();
        doc.EnsureTab(KvScratchTab, KvScratchName);
        model.Show(LayoutGroup.Hands, KvScratchTab);
        bool before = model.Editing == KvScratchTab;
        KvStore.Replace(KvDocument.Parse(without));
        KvEditorPage.Editor.Refresh();
        string status = model.StatusText();
        KvDocument now = KvStore.Current;
        bool phantom = now.Tabs.Contains(KvScratchTab);
        detail = $"before={before} phantom={phantom} editing={model.Editing} selected={now.SelectedTab} status='{status}'";
        return before && !phantom && model.Editing == now.SelectedTab;
    }

    // With the door forced off, a rebuilt page shows the card that opens the classic editor; back on, the editor returns.
    private static IEnumerator KvFallback(Action<string, bool, string> check) {
        KvEditorDoor.ForceGone = true;
        try {
            QuartzShell.Go(CorePages.HomePageKey);
            yield return Frames(2);
            QuartzShell.Go(KeyViewerPage);
            yield return Frames(3);
            check($"{KeyViewerPage}: with PrismLib.UI unusable the page has the classic-editor card and no editor",
                Body.Q(name: "kle-root") == null && Body.Q<Button>("MENU_KV_EDITOR-input") != null, $"editor={Body.Q(name: "kle-root") != null}");
            yield return EditorLink(check, KeyViewerPage, "MENU_KV_EDITOR");
        } finally {
            KvEditorDoor.ForceGone = false;
        }
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
        QuartzShell.Go(KeyViewerPage);
        yield return Frames(3);
        check($"{KeyViewerPage}: usable again, the rebuilt page has the editor and no card",
            KvEditorDoor.Live && Body.Q(name: "kle-menubar") != null && Body.Q(name: "MENU_KV_EDITOR") == null,
            $"live={KvEditorDoor.Live} card={Body.Q(name: "MENU_KV_EDITOR") != null}");
    }
}
#endif
