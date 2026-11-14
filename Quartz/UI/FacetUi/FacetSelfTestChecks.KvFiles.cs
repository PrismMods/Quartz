#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.FacetUi.KeyViewerEditor;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The key viewer editor's tabs and File menu: presets and foot counts as one undo step each, the strip's rename, reorder,
/// duplicate, delete and click, the layout limit, import and export through temporary files (one undo step that keeps live
/// counts), and the File menu's shape. Every check runs inside KvGuard on scratch tabs.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string KvFilesPage = KeyViewerPage + ": files";

    private static IEnumerator KvFilesChecks(Action<string, bool, string> check, Action<string> info) =>
        OnPage("keyviewer", KeyViewerPage, () => { }, () => { }, () => KvGuard(check, () => KvFilesOnPage(check)));

    private static IEnumerator KvFilesOnPage(Action<string, bool, string> check) {
        if(!KvEditorDoor.Usable || !KvEditorDoor.Live) {
            check($"{KvFilesPage}: the layout editor is on the page", false, $"usable={KvEditorDoor.Usable} live={KvEditorDoor.Live}");
            yield break;
        }
        KvScratchTabs();
        yield return KvBoxesSettle();
        yield return KvPresetSteps(check);
        yield return KvStripActions(check);
        string dir = Path.Combine(Path.GetTempPath(), "quartz-selftest-kv");
        Directory.CreateDirectory(dir);
        try {
            yield return KvImportExport(check, dir);
        } finally {
            try { Directory.Delete(dir, true); } catch(Exception e) { Diag.Ignore(e); }
        }
        KvFileMenuShape(check);
        yield return KvTabLimit(check);   // last: it fills the document to the limit
    }

    // ---- helpers

    private static string KvTr(string key) => QuartzHost.Translate(key);
    private static KvLayoutModel KvM => KvEditorPage.Model;
    private static List<string> KvTabs(LayoutGroup g) => [.. KvM.Layouts(g)];
    private static bool KvUndoNamed(string key) => global::Facet.History.CanUndo && global::Facet.History.NextUndoText == KvTr(key)
        && KvHeaderUndo() is { enabledSelf: true };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static global::Facet.Reorder<string> KvStrip(bool hands) => (global::Facet.Reorder<string>)typeof(PrismLib.UI.Toolkit.KeyLayoutEditor<KvElement>)
        .GetField(hands ? "_hands" : "_feet", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(KvEditorPage.Editor);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static global::Facet.ReorderOptions<string> KvStripOptions(bool hands) {
        object strip = KvStrip(hands);
        return (global::Facet.ReorderOptions<string>)strip.GetType().GetField("_o", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(strip);
    }

    private static global::Facet.MenuItem KvIn(IList<global::Facet.MenuItem> items, string id) => items?.FirstOrDefault(i => i?.Id == id);

    // Each element's kind, rectangle and key, in the document's order.
    private static string KvShape(string tab) =>
        string.Join(";", KvStore.Current.AllElements(tab).Select(e => $"{e.Kind}:{e.Bounds}:{e.GlobalKey}"));

    private static JObject KvConfNoCounts() {
        var o = (JObject)KeyViewerOverlay.Conf.Serialize();
        o.Remove(nameof(KeyViewerSettings.Counts));
        return o;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Label KvStatusLabel() => KvEditorPage.Editor.Status.Root.Q<Label>();

    // The status line's text colour is the one a label painted with this role gets.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KvStatusIs(global::Facet.Role role) {
        Label shown = KvStatusLabel(), probe = new();
        KvEditorPage.Editor.Status.Root.Add(probe);
        global::Facet.Paint.Fg(probe, role);
        bool same = shown != null && shown.style.color.value == probe.style.color.value;
        probe.RemoveFromHierarchy();
        return same;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Button KvConfirmButton(string name) {
        var open = (List<global::Facet.Popup>)typeof(global::Facet.Popup).GetField("_open", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        return open.Count == 0 ? null : open[^1].Content.Q<Button>(name);
    }

    // ---- presets and foot counts

    private static IEnumerator KvPresetSteps(Action<string, bool, string> check) {
        var presets = KvItem(KeyLayoutText.Presets)?.Items ?? [];
        List<string> want = [.. KvPresets.Styles.Select(KvLayoutModel.StyleKey)];
        check($"{KvFilesPage}: File ▸ Presets lists the {KvPresets.Styles.Length} classic presets in the classic order, by name",
            presets.Select(i => i?.Id).SequenceEqual(want) && presets.All(i => i.Text == KvTr(i.Id)),
            $"ids={string.Join(",", presets.Select(i => i?.Id))}");

        global::Facet.History.Clear();
        List<string> hands = KvTabs(LayoutGroup.Hands);
        KvItem("KEYVIEWER_STYLE_8")?.Run?.Invoke();
        yield return Frames(1);
        string t = KvTabs(LayoutGroup.Hands).Except(hands).FirstOrDefault();
        int keys = t == null ? -1 : KvStore.Current.AllElements(t).Count(e => e.Kind == KvElementKind.Key);
        bool step = KvUndoNamed("MENU_KVE_NEW_FROM_PRESET");
        check($"{KvFilesPage}: File ▸ Presets ▸ 8 Keys adds one hand tab of 8 keys, shown and edited, as one step named New from Preset with the header's undo enabled",
            KvTabs(LayoutGroup.Hands).Count == hands.Count + 1 && keys == 8 && KvM.Editing == t && KvM.Shown(LayoutGroup.Hands) == t && step,
            $"tabs={hands.Count}->{KvTabs(LayoutGroup.Hands).Count} keys={keys} editing={KvM.Editing} shown={KvM.Shown(LayoutGroup.Hands)} next='{(global::Facet.History.NextUndoText)}'");
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: one undo removes the preset tab", KvTabs(LayoutGroup.Hands).SequenceEqual(hands) && !KvStore.Current.HasTab(t),
            $"tabs={KvTabs(LayoutGroup.Hands).Count}");

        global::Facet.History.Clear();
        KvItem(KeyLayoutText.NewLayout)?.Run?.Invoke();
        yield return Frames(1);
        t = KvTabs(LayoutGroup.Hands).Except(hands).FirstOrDefault();
        string name = t == null ? null : KvStore.Current.TabName(t);
        bool named = name != null && name.StartsWith(KvTr("MENU_KVE_NEW_TAB_NAME"), StringComparison.Ordinal)
            && KvStore.Current.AllElements(t).Count == 0 && KvM.Shown(LayoutGroup.Hands) == t;
        step = KvUndoNamed("MENU_KVE_NEW_TAB");
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: File ▸ New Layout Tab adds one empty hand tab named Layout, shown, as one step; undo removes it",
            named && step && KvTabs(LayoutGroup.Hands).SequenceEqual(hands),
            $"tab={t} name={name} named={named} step={step} tabs after undo={KvTabs(LayoutGroup.Hands).Count}");

        global::Facet.History.Clear();
        KvIn(KvStripOptions(true).Add(), "empty")?.Run?.Invoke();
        yield return Frames(1);
        t = KvTabs(LayoutGroup.Hands).Except(hands).FirstOrDefault();
        step = KvUndoNamed("MENU_KVE_NEW_FROM_PRESET");
        bool shown = t != null && KvM.Shown(LayoutGroup.Hands) == t;
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: the Hands strip's ＋ ▸ New Layout Tab adds one shown tab as one step; undo removes it",
            shown && step && KvTabs(LayoutGroup.Hands).SequenceEqual(hands), $"tab={t} shown={shown} step={step}");

        global::Facet.History.Clear();
        List<string> feet = KvTabs(LayoutGroup.Feet);
        KvIn(KvStripOptions(false).Add(), "foot-4")?.Run?.Invoke();
        yield return Frames(1);
        t = KvTabs(LayoutGroup.Feet).Except(feet).FirstOrDefault();
        keys = t == null ? -1 : KvStore.Current.AllElements(t).Count(e => e.Kind == KvElementKind.Key);
        step = KvUndoNamed("MENU_KVE_NEW_TAB");
        bool footShown = t != null && KvM.Shown(LayoutGroup.Feet) == t;
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: Feet ＋ ▸ 4 Keys adds a shown foot tab of 4 keys as one step; undo removes it",
            keys == 4 && footShown && step && KvTabs(LayoutGroup.Feet).SequenceEqual(feet), $"tab={t} keys={keys} shown={footShown} step={step}");

        global::Facet.History.Clear();
        string foot = KvStore.Current.SelectedFootTab;
        KvIn(KvStripOptions(false).TitleMenu(), KeyLayoutText.HideFeet)?.Run?.Invoke();
        yield return Frames(1);
        bool hidden = KvStore.Current.SelectedFootTab == null;
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: Feet's Hide Foot Keys shows no foot tab, and undo shows it again",
            foot != null && hidden && KvStore.Current.SelectedFootTab == foot, $"before={foot} hidden={hidden} after={KvStore.Current.SelectedFootTab}");
    }

    // ---- the strip

    private static IEnumerator KvStripActions(Action<string, bool, string> check) {
        var strip = KvStrip(true);
        var o = KvStripOptions(true);

        global::Facet.History.Clear();
        strip.BeginRename(kvHand);
        yield return Frames(2);
        TextField field = strip.Root.Q<TextField>("kle-hands-rename");
        if(field != null) {
            field.value = "Self Test Renamed";
            using KeyDownEvent ev = Pooled<KeyDownEvent>();
            typeof(KeyboardEventBase<KeyDownEvent>).GetProperty("keyCode").SetValue(ev, KeyCode.Return);
            ev.target = field;
            field.SendEvent(ev);
        }
        yield return Frames(1);
        check($"{KvFilesPage}: renaming a hand tab in the strip and pressing Enter names it", field != null && KvStore.Current.TabName(kvHand) == "Self Test Renamed",
            $"field={field != null} name={KvStore.Current.TabName(kvHand)}");

        List<string> hands = KvTabs(LayoutGroup.Hands), feet = KvTabs(LayoutGroup.Feet);
        global::Facet.History.Clear();
        o.Move(0, 1);
        yield return Frames(1);
        List<string> moved = KvTabs(LayoutGroup.Hands);
        bool swapped = moved.Count == hands.Count && moved[0] == hands[1] && moved[1] == hands[0] && KvTabs(LayoutGroup.Feet).SequenceEqual(feet);
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: dragging hand tab 0 to 1 moves it within Hands only, and undo moves it back",
            swapped && KvTabs(LayoutGroup.Hands).SequenceEqual(hands), $"moved={string.Join(",", moved)} was={string.Join(",", hands)}");

        global::Facet.History.Clear();
        KvIn(o.Menu(kvHand), KeyLayoutText.Duplicate)?.Run?.Invoke();
        yield return Frames(1);
        List<string> now = KvTabs(LayoutGroup.Hands);
        int at = now.IndexOf(kvHand);
        string copy = at >= 0 && at + 1 < now.Count ? now[at + 1] : null;
        check($"{KvFilesPage}: Duplicate in a tab's menu adds a copy right after it, with the same elements",
            now.Count == hands.Count + 1 && copy != null && !hands.Contains(copy) && KvShape(copy) == KvShape(kvHand), $"copy={copy} at={at}");
        global::Facet.History.Undo();
        yield return Frames(1);

        // Delete asks first: Cancel keeps the tab, OK removes it.
        string victim = KvStore.Current.NewTabId();   // the current document: an undo replaces it
        KvStore.Current.EnsureTab(victim, "Self Test Victim");
        KvEditorPage.Editor.Refresh();
        yield return Frames(1);
        KvIn(o.Menu(victim), KeyLayoutText.Delete)?.Run?.Invoke();
        yield return Frames(2);
        Button cancel = KvConfirmButton("confirm-cancel");
        if(cancel != null) Submit(cancel);
        yield return Frames(2);
        bool kept = KvStore.Current.HasTab(victim);
        KvIn(o.Menu(victim), KeyLayoutText.Delete)?.Run?.Invoke();
        yield return Frames(2);
        Button ok = KvConfirmButton("confirm-ok");
        if(ok != null) Submit(ok);
        yield return Frames(2);
        check($"{KvFilesPage}: Delete asks first; Cancel keeps the tab and OK removes it",
            cancel != null && kept && ok != null && !KvStore.Current.HasTab(victim), $"cancel={cancel != null} kept={kept} ok={ok != null} gone={!KvStore.Current.HasTab(victim)}");
        global::Facet.Popup.CloseAll();

        // A click shows the tab; undo shows the previous one again. A Move made on A, a click on B, then two undos: A shows
        // and its element is back.
        string b = KvStore.Current.NewTabId();
        KvStore.Current.EnsureTab(b, "Self Test Other");
        KvM.Show(LayoutGroup.Hands, kvHand);
        KvEditorPage.Editor.Refresh();
        global::Facet.History.Clear();
        KvEditorPage.Editor.Canvas.Clear();
        yield return KvBoxesSettle();
        KvElement e = KvEls()[0];
        string key = KvM.Key(e);
        float x0 = e.X;
        KvDragNow(e, new Vector2(30f, 0f));
        yield return Frames(1);
        o.Select(b);
        yield return Frames(1);
        bool clicked = KvM.Shown(LayoutGroup.Hands) == b && KvM.Editing == b;
        global::Facet.History.Undo();
        yield return Frames(1);
        bool back = KvM.Shown(LayoutGroup.Hands) == kvHand && KvM.Editing == kvHand;
        global::Facet.History.Undo();
        yield return KvBoxesSettle();
        KvElement after = KvM.Find(key);
        check($"{KvFilesPage}: a click on a hand tab shows and edits it, and undo shows the previous tab",
            clicked && back, $"clicked={clicked} back={back} shown={KvM.Shown(LayoutGroup.Hands)}");
        check($"{KvFilesPage}: Move on one tab, a click on another, two undos: the first tab shows and its element is back",
            KvM.Editing == kvHand && after != null && Mathf.Abs(after.X - x0) < 0.01f, $"editing={KvM.Editing} x={after?.X} want={x0}");
    }

    // ---- import and export

    private const string KvPng1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    private static string KvImages() => KvStore.Current.Root["embeddedLocalImages"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "";

    private static IEnumerator KvImportExport(Action<string, bool, string> check, string dir) {
        // The names an export writes and an import reads, for every mouse button: the enum parser alone shifts them by one.
        string swapped = string.Join(", ", Enumerable.Range(0, 7).Select(b => KeyCode.Mouse0 + b)
            .Select(k => (k, back: KeyViewerOverlay.ResolveGlobalKey(KvKeyNames.ToGlobalKey(k)))).Where(p => p.back != p.k)
            .Select(p => $"{p.k}->{KvKeyNames.ToGlobalKey(p.k)}->{p.back}"));
        check($"{KvFilesPage}: every mouse button's DM Note name reads back as the same button", swapped == "", swapped);
        KvLayoutModel model = KvM;
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        model.Show(LayoutGroup.Hands, kvHand);
        global::Facet.History.Clear();
        string a = Path.Combine(dir, "a.json");
        string shape = KvShape(kvHand), foot = KvStore.Current.SelectedFootTab;
        KvStore.Current.SelectedFootTab = null;   // the hand tab alone: a shown foot tab would be exported beside it
        bool exported = model.ExportPath(a, KvExportFormat.DmNote, out string msg);
        KvStore.Current.SelectedFootTab = foot;
        KvEls()[0].X += 50f;   // the scratch tab changes after the export

        // Live counts set before the import survive its undo: an element's and one with no element.
        List<KvElement> keys = [.. KvEls().Where(e => e.Kind == KvElementKind.Key)];
        string counted = model.Key(keys[1]);
        keys[1].Count = 41;
        conf.Counts["quartz-selftest"] = 7;
        string doc0 = KvJson();
        JObject conf0 = KvConfNoCounts();
        List<string> hands = KvTabs(LayoutGroup.Hands);
        bool imported = model.ImportPath(a, out string said);
        yield return Frames(1);
        string t = KvTabs(LayoutGroup.Hands).Except(hands).FirstOrDefault();
        check($"{KvFilesPage}: a DM Note export, imported back, adds one tab equal to the exported one (geometry and keys), shown and edited",
            exported && imported && t != null && KvShape(t) == shape && model.Editing == t && model.Shown(LayoutGroup.Hands) == t
            && said == string.Format(KvTr("MENU_KVE_IMPORTED"), KvStore.Current.TabName(t)),
            $"exported={exported} ({msg}) imported={imported} tab={t} said='{said}' shape={(t == null ? null : KvShape(t))} want={shape}");
        bool one = global::Facet.History.CanUndo;
        global::Facet.History.Undo();
        yield return Frames(1);
        KvElement c = model.Find(counted);
        check($"{KvFilesPage}: one undo of the import puts the layout and the Key Viewer settings back exactly",
            one && KvJson() == doc0 && JToken.DeepEquals(KvConfNoCounts(), conf0), $"step={one} layout={KvJson() == doc0} settings={JToken.DeepEquals(KvConfNoCounts(), conf0)}");
        check($"{KvFilesPage}: the import's undo keeps the live counts set before it", c?.Count == 41 && conf.Counts.TryGetValue("quartz-selftest", out int n) && n == 7,
            $"element={c?.Count} conf={(conf.Counts.TryGetValue("quartz-selftest", out int m) ? m : -1)}");
        conf.Counts.Remove("quartz-selftest");

        // An embedded image comes in with its import, goes with the undo and comes back with the redo.
        const string imageId = "quartz-selftest-image";
        string img = Path.Combine(dir, "img.json");
        KvDocument withImage = KvDocument.Parse(File.ReadAllText(a));
        withImage.Tabs.SelectMany(tb => withImage.Elements(tb, KvElementKind.Key)).First().Raw["inactiveImage"] = KvDocument.DmLocalImagePrefix + imageId;
        withImage.Root["embeddedLocalImages"] = new JArray { new JObject { ["imageId"] = imageId, ["dataBase64"] = KvPng1x1, ["extension"] = "png" } };
        File.WriteAllText(img, withImage.ToJson());
        string table0 = KvImages();
        hands = KvTabs(LayoutGroup.Hands);
        global::Facet.History.Clear();
        bool withImg = model.ImportPath(img, out said);
        yield return Frames(1);
        bool came = KvImages().Contains(imageId);
        global::Facet.History.Undo();
        yield return Frames(1);
        string undone = KvImages();
        global::Facet.History.Redo();
        yield return Frames(1);
        string it = KvTabs(LayoutGroup.Hands).Except(hands).FirstOrDefault();
        bool resolves = it != null && KvStore.Current.Elements(it, KvElementKind.Key)
            .Any(e => KvStore.Current.TryEmbeddedImage(e.Raw["inactiveImage"]?.ToString(), out _, out _));
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: an import's embedded image leaves the table with its undo and is back with its redo",
            withImg && came && undone == table0 && resolves && KvImages() == table0,
            $"imported={withImg} ({said}) came={came} undoneSame={undone == table0} redoResolves={resolves} endSame={KvImages() == table0}");

        // A preset with no keys adds nothing and says so.
        string none = Path.Combine(dir, "none.json");
        File.WriteAllText(none, "{\"keys\":{}}");
        int count0 = KvStore.Current.Tabs.Count();
        global::Facet.History.Clear();
        model.ImportPath(none, out said);
        yield return Frames(1);
        check($"{KvFilesPage}: importing a preset with no keys adds no tab, records no step and says there was nothing to import",
            KvStore.Current.Tabs.Count() == count0 && !global::Facet.History.CanUndo && said == KvTr("KEYVIEWER_EDITOR_IMPORT_NO_KEYS") && KvStatusLabel()?.text == said,
            $"tabs={count0}->{KvStore.Current.Tabs.Count()} step={(global::Facet.History.CanUndo)} said='{said}'");

        // Not a preset: no step, and the store's own error in the danger colour.
        string x = Path.Combine(dir, "x.json");
        File.WriteAllText(x, "x");
        global::Facet.History.Clear();
        bool bad = model.ImportPath(x, out said);
        yield return Frames(1);
        check($"{KvFilesPage}: importing a file that holds 'x' fails, records no step and says why in the danger colour",
            !bad && !global::Facet.History.CanUndo && !string.IsNullOrEmpty(said) && KvStatusLabel()?.text == said && KvStatusIs(global::Facet.Role.Danger),
            $"ok={bad} step={(global::Facet.History.CanUndo)} said='{said}' shown='{KvStatusLabel()?.text}'");

        // The CSS import's shape without its picker: a settings-only change is one step.
        JObject before = KvConfNoCounts();
        global::Facet.History.Clear();
        model.ImportStep("KEYVIEWER_DM_CSS_IMPORT", () => { conf.DmCssEnabled = !conf.DmCssEnabled; return true; });
        bool stepped = global::Facet.History.CanUndo;
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: a settings-only import is one step, and one undo puts the settings back exactly",
            stepped && !global::Facet.History.CanUndo && JToken.DeepEquals(KvConfNoCounts(), before), $"step={stepped} more={(global::Facet.History.CanUndo)}");

        // A .qkv carries a changed Key Viewer setting.
        string b = Path.Combine(dir, "b.qkv");
        float scale = conf.DmScale;
        conf.DmScale = 1.7f;
        bool wrote = model.ExportPath(b, KvExportFormat.Quartz, out _);
        conf.DmScale = 1f;
        global::Facet.History.Clear();
        bool read = model.ImportPath(b, out _);
        yield return Frames(1);
        float got = conf.DmScale;
        global::Facet.History.Undo();
        yield return Frames(1);
        check($"{KvFilesPage}: a .qkv import brings its scale 1.7 back, and one undo gives 1.0",
            wrote && read && Mathf.Abs(got - 1.7f) < 0.001f && Mathf.Abs(conf.DmScale - 1f) < 0.001f, $"wrote={wrote} read={read} got={got} undone={conf.DmScale}");
        conf.DmScale = scale;

        // A ghost key is not carried into a DM Note export, and the status says so by name.
        KvEls().First(e => e.Kind == KvElementKind.Key).BindGhostKey(KeyCode.Z);
        model.ExportPath(Path.Combine(dir, "g.json"), KvExportFormat.DmNote, out said);
        yield return Frames(1);
        check($"{KvFilesPage}: a DM Note export of a tab with a ghost key says ghost keys are left out",
            said != null && said.Contains(KvTr("KEYVIEWER_DMGAP_GHOST")) && KvStatusLabel()?.text == said, $"said='{said}'");
    }

    // ---- the File menu

    private static void KvFileMenuShape(Action<string, bool, string> check) {
        var items = KvEditorPage.Editor.Menus[0].Items;
        string ids = string.Join(",", items.Select(i => i == null || i.IsSeparator ? "|" : i.Id));
        string want = string.Join(",", KeyLayoutText.NewLayout, KeyLayoutText.Presets, "|", "MENU_KVE_IMPORT_JSON", "MENU_KVE_IMPORT_QKV",
            "KEYVIEWER_DM_CSS_IMPORT", "|", "KEYVIEWER_EDITOR_EXPORT_JSON", "KEYVIEWER_EDITOR_EXPORT_QKV", "|", "KEYVIEWER_EDITOR_OPEN_FOLDER");
        var open = KvIn(items, "KEYVIEWER_EDITOR_OPEN_FOLDER");
        bool texts = items.Where(i => i != null && !i.IsSeparator && i.Id.StartsWith("KEYVIEWER_", StringComparison.Ordinal)).All(i => i.Text == KvTr(i.Id));
        check($"{KvFilesPage}: File lists New Layout Tab, Presets, the three imports, the two exports and Open Presets Folder (enabled), in order",
            ids == want && texts && open != null && (open.Enabled == null || open.Enabled()), $"ids={ids}");
    }

    // ---- the layout limit

    private static IEnumerator KvTabLimit(Action<string, bool, string> check) {
        KvDocument doc = KvStore.Current;
        for(int i = 0; doc.CustomTabCount < KvDocument.MaxCustomTabs && i < 64; i++) doc.EnsureTab(doc.NewTabId(), "Self Test Fill " + i);
        KvEditorPage.Editor.Refresh();
        yield return Frames(3);

        var strip = KvStrip(true);
        ScrollView sv = strip.Root.Q<ScrollView>();
        List<VisualElement> faces = sv == null ? [] : [.. sv.contentContainer.Children().Where(v => v.name != strip.Root.name + "-drop")];
        bool line = faces.Count == KvTabs(LayoutGroup.Hands).Count && faces.All(f => Mathf.Abs(f.layout.y - faces[0].layout.y) < 0.5f);
        if(sv != null) sv.scrollOffset = new Vector2(sv.contentContainer.layout.width, 0f);
        yield return Frames(2);
        bool reach = faces.Count > 0 && faces[^1].worldBound.xMax <= sv!.contentViewport.worldBound.xMax + 1f && faces[^1].worldBound.xMin >= sv.contentViewport.worldBound.xMin - 1f;
        check($"{KvFilesPage}: {KvDocument.MaxCustomTabs} tabs sit on one line in the Hands strip, and the last one scrolls into reach",
            line && reach, $"faces={faces.Count} line={line} reach={reach}");

        global::Facet.History.Clear();
        int tabs = doc.Tabs.Count();
        string limit = KvM.LimitText();
        var added = KvStripOptions(true).Add();
        string status = KvStatusLabel()?.text;
        check($"{KvFilesPage}: at the limit the strip's ＋ offers nothing and the status says the limit",
            limit == string.Format(KvTr("KEYVIEWER_EDITOR_TAB_MAX"), doc.CustomTabCount) && added == null && status == limit, $"limit='{limit}' status='{status}' items={added?.Count}");
        KvItem(KeyLayoutText.NewLayout)?.Run?.Invoke();
        KvItem("KEYVIEWER_STYLE_8")?.Run?.Invoke();
        string dup = KvM.Duplicate(kvHand);
        KvIn(KvM.NewItems(LayoutGroup.Hands, _ => { }), "empty")?.Run?.Invoke();
        yield return Frames(1);
        check($"{KvFilesPage}: at the limit File ▸ New Layout Tab, a preset, the switcher's Duplicate and a bare New add nothing and record nothing",
            doc.Tabs.Count() == tabs && dup == null && !global::Facet.History.CanUndo && KvStatusLabel()?.text == limit,
            $"tabs={tabs}->{doc.Tabs.Count()} dup={dup} step={(global::Facet.History.CanUndo)} status='{KvStatusLabel()?.text}'");

        string full = Path.Combine(Path.GetTempPath(), "quartz-selftest-kv-limit.json");
        bool wrote = KvStore.ExportToPath(full, KvExportFormat.DmNote, out _);
        bool imported = KvM.ImportPath(full, out string said);
        try { File.Delete(full); } catch(Exception e) { Diag.Ignore(e); }
        yield return Frames(1);
        check($"{KvFilesPage}: at the limit File ▸ Import adds nothing, records nothing and says the limit",
            wrote && !imported && doc.Tabs.Count() == tabs && !global::Facet.History.CanUndo && said == limit && KvStatusLabel()?.text == limit,
            $"wrote={wrote} imported={imported} tabs={tabs}->{doc.Tabs.Count()} step={(global::Facet.History.CanUndo)} said='{said}'");

        // A .qkv that carries only Key Viewer settings adds no layout, so the limit does not refuse it.
        string only = Path.Combine(Path.GetTempPath(), "quartz-selftest-kv-settings.qkv");
        JObject carried = new() {
            [nameof(KeyViewerSettings.DmNoteSpeed)] = KeyViewerOverlay.Conf.DmNoteSpeed,
            [nameof(KeyViewerSettings.DmFootRelative)] = KeyViewerOverlay.Conf.DmFootRelative,   // a missing one is set false
        };
        File.WriteAllText(only, new JObject { ["keys"] = new JObject(), [KvStore.SettingsKey] = carried }.ToString());
        bool applied = KvM.ImportPath(only, out said);
        try { File.Delete(only); } catch(Exception e) { Diag.Ignore(e); }
        yield return Frames(1);
        check($"{KvFilesPage}: at the limit a .qkv with only Key Viewer settings still imports them and adds no tab",
            applied && doc.Tabs.Count() == tabs && said == string.Format(KvTr("KEYVIEWER_EDITOR_IMPORTED_QKV"), 2),
            $"imported={applied} tabs={tabs}->{doc.Tabs.Count()} said='{said}'");

        float until = Time.realtimeSinceStartup + 5.5f;
        while(Time.realtimeSinceStartup < until) yield return null;
        check($"{KvFilesPage}: 5 s later the status line is back to the element count", KvStatusLabel()?.text == KvM.StatusText(),
            $"shown='{KvStatusLabel()?.text}' want='{KvM.StatusText()}'");
    }
}
#endif
