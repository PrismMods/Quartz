#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Quartz.Features.KeyViewer.Editor;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The key viewer editor's inspector: the same rows as the classic inspector for five selections, the choice rows the
/// classic id set cannot see, and the scaffolding the row checks in FacetSelfTestChecks.KvInspector.Rows.cs share.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string KvInspPage = KeyViewerPage + ": inspector";

    private static IEnumerator KvInspectorChecks(Action<string, bool, string> check, Action<string> info) =>
        OnPage("keyviewer", KeyViewerPage, () => { }, () => { }, () => KvGuard(check, () => KvInspectorOnPage(check, info)));

    private static IEnumerator KvInspectorOnPage(Action<string, bool, string> check, Action<string> info) {
        if(!KvEditorDoor.Usable || !KvEditorDoor.Live) {
            check($"{KvInspPage}: the layout editor is on the page", false, $"usable={KvEditorDoor.Usable} live={KvEditorDoor.Live}");
            yield break;
        }
        yield return KvParity(check, info);
        KvScratchTabs();
        yield return KvBoxesSettle();
        yield return KvMixedRow(check);
        yield return KvRowDrag(check);
        yield return KvRowKeys(check);
        yield return KvResetCount(check);
        yield return KvKeyCapture(check);
        yield return KvRebindOrder(check);
        yield return KvInspectorPerf(check, info);
        KvKeyAfterRecreate(check);
    }

    // The edited tab deleted and made again under the same id: an old element's cached key must not name the new element.
    private static void KvKeyAfterRecreate(Action<string, bool, string> check) {
        KvLayoutModel model = KvEditorPage.Model;
        KvDocument doc = KvStore.Current;
        string tab = model.Editing, name = doc.TabName(tab);
        KvElement old = doc.Elements(tab, KvElementKind.Key).FirstOrDefault();
        string before = old == null ? null : model.Key(old);
        bool removed = doc.RemoveTab(tab);
        doc.EnsureTab(tab, name);
        KvElement fresh = KvElement.Wrap(KvPresets.NewPosition(0, 0, 60, 60, KvPresets.NewElementStyle(false)), KvElementKind.Key, "");
        doc.Add(tab, fresh);
        string after = old == null ? null : model.Key(old);
        check($"{KvInspPage}: after its tab is deleted and made again, an old element's key does not name the new element",
            before != null && removed && model.Editing == tab && model.Find(after) != fresh, $"before={before} removed={removed} after={after}");
    }

    // ---- parity with classic, on a scratch document KvStore.Current never sees

    // The classic toolbar's buttons (now the menu bar's) and its multi-select Arrange rows (now Arrange ▸ Align/Spread).
    private static readonly Regex KvMenuBarIds = new("^KVI_(UNDO|REDO|DUPLICATE|DELETE|COPY_STYLE|PASTE_STYLE|SNAP|SPREAD_H|SPREAD_V)$|^KVI_(Z|SNAP|ADD|ZOOM)_");
    private static readonly Regex KvRowId = new("^KVI_[A-Z0-9_]+$");
    private static readonly string[] KvClassicTabKeys = ["KVI_TAB_ELEMENT", "KVI_TAB_STYLE", "KVI_TAB_NOTE", "KVI_TAB_COUNTER", "KVI_TAB_SETTINGS"];

    // On a scratch tab of the live document (KvGuard puts it back), read from the live inspector's own tabs.
    private static IEnumerator KvParity(Action<string, bool, string> check, Action<string> info) {
        KvDocument doc = KvStore.Current;
        string tab = doc.NewTabId();
        doc.EnsureTab(tab, "Self Test Parity");
        KvElement Add(KvElementKind kind, float x, float y, float w, float h, string stat = null, KeyCode key = KeyCode.None) {
            KvElement e = KvElement.Wrap(KvPresets.NewPosition(x, y, w, h, KvPresets.NewElementStyle(kind != KvElementKind.Key)), kind,
                key == KeyCode.None ? "" : KvKeyNames.ToGlobalKeyOrRaw(key));
            if(kind == KvElementKind.Graph) e.Raw["graphType"] = "line";
            if(stat != null) e.StatType = stat;
            doc.Add(tab, e);
            return e;
        }
        KvElement[] keys = [Add(KvElementKind.Key, 0, 0, 60, 60, key: KeyCode.A), Add(KvElementKind.Key, 70, 0, 60, 60, key: KeyCode.S),
            Add(KvElementKind.Key, 140, 0, 60, 60, key: KeyCode.D)];
        KvElement kps = Add(KvElementKind.Stat, 0, 80, 100, 30, "kps");
        Add(KvElementKind.Stat, 110, 80, 100, 30, "total");
        KvElement graph = Add(KvElementKind.Graph, 0, 120, 200, 100, "kps");
        (string Name, KvElement[] Sel, Dictionary<string, string[]> Choices)[] cases = [
            ("a single key", [keys[0]], KvChoices(rain: true, counter: true)),
            ("a single stat", [kps], KvChoices(element: ["kv-stat-type"], counter: true)),
            ("a single graph", [graph], KvChoices(element: ["kv-graph-type", "kv-stat-type"])),
            ("three keys", keys, KvChoices(rain: true, counter: true)),
            ("a key and a stat", [keys[0], kps], KvChoices(rain: true, counter: true)),
        ];
        KvEditorPage.Model.Show(LayoutGroup.Hands, tab);
        KvEditorPage.Editor.Refresh();
        yield return KvBoxesSettle();
        foreach(var c in cases) {
            Dictionary<string, HashSet<string>> classic = KvClassicIds(doc, tab, c.Sel), now = [], choices = [], every = [];
            yield return KvLiveIds(c.Sel, now, choices, every);
            if(c.Sel.Length == 1 && c.Sel[0].Kind == KvElementKind.Key) {
                List<string> off = [];
                foreach(KeyValuePair<string, HashSet<string>> t in every) {
                    HashSet<string> named = [.. KvInspectorPages.RowIds(KvElementKind.Key, false, t.Key)];
                    if(!named.SetEquals(t.Value))
                        off.Add($"{t.Key}: unnamed [{string.Join(",", t.Value.Except(named))}] extra [{string.Join(",", named.Except(t.Value))}]");
                }
                check($"{KvInspPage}: the row ids the pages name for one key are the rows built", off.Count == 0 && every.Count > 0, string.Join("; ", off));
            }
            List<string> diffs = [];
            foreach(string t in classic.Keys.Union(now.Keys)) {
                HashSet<string> a = classic.TryGetValue(t, out var x) ? x : null, b = now.TryGetValue(t, out var y) ? y : null;
                if(a == null || b == null) { diffs.Add($"{t}: tab only in {(a == null ? "new" : "classic")}"); continue; }
                var missing = a.Except(b).ToList();
                var extra = b.Except(a).ToList();
                if(missing.Count + extra.Count > 0) diffs.Add($"{t}: missing [{string.Join(",", missing)}] extra [{string.Join(",", extra)}]");
            }
            info($"{KvInspPage}: {c.Name}: " + string.Join(" ", classic.Select(kv => $"{kv.Key}={kv.Value.Count}")));
            check($"{KvInspPage}: {c.Name}: every tab has the classic inspector's rows", diffs.Count == 0 && now.Count > 0, string.Join("; ", diffs));
            List<string> lost = [.. c.Choices.SelectMany(kv => kv.Value.Where(id => !(choices.TryGetValue(kv.Key, out var got) && got.Contains(id)))
                .Select(id => $"{kv.Key}/{id}"))];
            check($"{KvInspPage}: {c.Name}: the classic segment rows are choice rows", lost.Count == 0, "missing " + string.Join(",", lost));
        }
    }

    private static Dictionary<string, string[]> KvChoices(string[] element = null, bool rain = false, bool counter = false) {
        Dictionary<string, string[]> d = new() { ["KVI_TAB_ELEMENT"] = element ?? [], ["KVI_TAB_STYLE"] = ["kv-image-fit"] };
        if(rain) d["KVI_TAB_NOTE"] = ["kv-note-border-side", "kv-note-align"];
        if(counter) d["KVI_TAB_COUNTER"] = ["kv-counter-placement", "kv-counter-align", "kv-counter-mode"];
        return d;
    }

    // The classic inspector over a throwaway canvas under a hidden object on Quartz's canvas: each tab's tracked row ids.
    private static Dictionary<string, HashSet<string>> KvClassicIds(KvDocument doc, string tab, KvElement[] sel) {
        Dictionary<string, HashSet<string>> ids = [];
        var hostObj = new GameObject("kv-parity", typeof(RectTransform));
        hostObj.transform.SetParent(UICore.CanvasRect, false);
        hostObj.SetActive(false);
        var props = new GameObject("props", typeof(RectTransform));
        props.transform.SetParent(hostObj.transform, false);
        KvCanvas canvas = KvCanvas.Create(hostObj.transform);
        KvInspector insp = KvInspector.Attach(canvas);
        try {
            insp.BindHost(null, props.GetComponent<RectTransform>(), null);
            canvas.Bind(doc, tab);
            canvas.SetSelection(sel);
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo tabField = typeof(KvInspector).GetField("tab", Any), tracked = typeof(KvInspector).GetField("tracked", Any);
            var tabs = (Array)typeof(KvInspector).GetMethod("TabsForSelection", Any).Invoke(insp, null);
            foreach(object t in tabs) {
                string name = KvClassicTabKeys[(int)t];
                if(name == "KVI_TAB_SETTINGS") continue;   // not repeated: the page's own settings rows hold it
                tabField.SetValue(insp, t);
                insp.Push();
                ids[name] = [.. ((List<UIObject>)tracked.GetValue(insp)).Select(o => GenerateUI.LocaleKeyFromId(o.Id))
                    .Where(k => k != null && !KvMenuBarIds.IsMatch(k))];
            }
        } finally {
            insp.Dispose();
            canvas.Dispose();
            UnityEngine.Object.Destroy(hostObj);
        }
        return ids;
    }

    // The new pages built one at a time into a throwaway box on the editor: each tab's shown row ids, and every choice row's.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    // Selects sel on the live canvas, then shows each of the live inspector's tabs in turn and reads its rows.
    private static IEnumerator KvLiveIds(KvElement[] sel, Dictionary<string, HashSet<string>> ids, Dictionary<string, HashSet<string>> choices,
        Dictionary<string, HashSet<string>> every) {
        yield return KvInspect(0, sel);
        for(int i = 0; i < 8; i++) {
            global::Facet.Tabs tabs = KvInspectorTabs();
            if(tabs == null) yield break;
            tabs.Select(i);
            if(tabs.Selected != i) continue;   // past the last tab, or one hidden for this selection
            QuartzShell.Surface.Refresh();
            yield return Frames(1);
            tabs = KvInspectorTabs();
            if(tabs?.Content == null || tabs.Selected != i) continue;
            string title = tabs.SelectedName;
            List<VisualElement> all = tabs.Content.Query<VisualElement>().ToList();
            ids[title] = [.. all.Where(v => v.name != null && KvRowId.IsMatch(v.name) && !v.name.StartsWith("KVI_SEC_")
                && v.style.display.value != DisplayStyle.None).Select(v => v.name)];
            choices[title] = [.. all.Where(v => v.name != null && v.name.StartsWith("kv-") && !v.name.EndsWith("-input")).Select(v => v.name)];
            every[title] = [.. all.Where(v => v.name != null && (KvRowId.IsMatch(v.name) && !v.name.StartsWith("KVI_SEC_") || v.name.StartsWith("kv-"))
                && !v.name.EndsWith("-input")).Select(v => v.name)];
        }
    }

    // ---- shared by the row checks

    // The live inspector's tab bar, found through the Ref Facet keeps on it.
    private static global::Facet.Tabs KvInspectorTabs() {
        Type refType = typeof(global::Facet.Tabs).GetNestedType("Ref", BindingFlags.NonPublic);
        foreach(VisualElement v in KvInspectorRoot().Query<VisualElement>().ToList())
            if(KvFacetData(v, refType) is { } r) return (global::Facet.Tabs)refType.GetField("Tabs").GetValue(r);
        return null;
    }

    private static object KvFacetData(VisualElement e, Type t) =>
        typeof(global::Facet.Data).GetMethod("Find").MakeGenericMethod(t).Invoke(null, [e]);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static VisualElement KvInspectorRoot() => KvEditorPage.Editor.Root.Q("kle-inspector");

    // Select, let the inspector rebuild for it, then show tab `tab` (0 Element, 1 Style, 2 Rain, 3 Counter).
    private static IEnumerator KvInspect(int tab, params KvElement[] sel) {
        KvSelect(sel);
        QuartzShell.Surface.Refresh();
        yield return Frames(1);
        KvInspectorTabs()?.Select(tab);
        QuartzShell.Surface.Refresh();
        yield return Frames(1);
    }

    private static T KvRow<T>(string id) where T : VisualElement => KvInspectorRoot().Q<T>(id + "-input");

    // A slider row set to `value` as a keyboard change would: one whole write, one step.
    private static void KvSetSlider(string id, float value, float min, float max) {
        Slider s = KvRow<Slider>(id);
        if(s != null) s.value = (value - min) / (max - min);
    }

    private static float KvRadius(KvElement e) => KvProps.Float(e.Raw, "borderRadius", 10f);

    private static void KvDeliverKey(KeyCode key) =>
        typeof(KeyCaptureService).GetMethod("End", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, [key]);

    private static string KvStatus() => KvEditorPage.Editor.Root.Q("kle-status")?.Q<Label>()?.text ?? "";

    // ---- performance: the whole 108-key preset selected, the Style tab rebuilt for it

    private static IEnumerator KvInspectorPerf(Action<string, bool, string> check, Action<string> info) {
        KvDocument doc = KvStore.Current;
        string tab = doc.NewTabId();
        doc.EnsureTab(tab, "Self Test 108");
        KvMigration.GenerateStockTab(doc, tab, KvPresets.Style108);
        KvEditorPage.Model.Show(LayoutGroup.Hands, tab);
        KvEditorPage.Editor.Refresh();
        yield return KvBoxesSettle();
        List<KvElement> keys = [.. KvEls().Where(e => e.Kind == KvElementKind.Key)];
        yield return KvInspect(1, keys[0]);
        var clock = Stopwatch.StartNew();
        KvSelect([.. keys]);
        QuartzShell.Surface.Refresh();
        clock.Stop();
        double ms = clock.Elapsed.TotalMilliseconds;
        int rows = KvInspectorRoot().Query<VisualElement>().ToList().Count(v => v.name != null && KvRowId.IsMatch(v.name));
        string want = KvInspectorPages.Title(keys);   // the multi-select title with the count: the timed refresh rebuilt it
        bool titled = KvInspectorRoot().Query<TextElement>().ToList().Any(t => t.text == want);
        info($"[perf] key viewer inspector 108 keys {ms:0.0} ms ({keys.Count} keys, {rows} rows)");
        check($"{KvInspPage}: the inspector over {keys.Count} selected keys builds in at most 50 ms",
            keys.Count >= 100 && rows > 0 && titled && ms <= 50.0, $"ms={ms:0.0} keys={keys.Count} rows={rows} title '{want}' shown={titled}");

        // A row drag over the selection reads every selected element's key each frame; those reads allocate nothing.
        KvLayoutModel model = KvEditorPage.Model;
        foreach(KvElement k in keys) _ = model.Key(k);
        int frames = KvProbeRuns / keys.Count;
        long grew = KvHeapGrowth(() => {
            for(int f = 0; f < frames; f++)
                foreach(KvElement k in keys) _ = model.Key(k);
        });
        info($"[perf] key viewer row drag keys: heap grew {grew} bytes over {frames} frames x {keys.Count} keys");
        check($"{KvInspPage}: a row drag's per-frame key reads over {keys.Count} selected keys allocate nothing", grew < KvProbeSlack, $"{grew} bytes");
    }
}
#endif
