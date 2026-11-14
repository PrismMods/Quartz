#if !IL2CPP && !QUARTZ_KEYVIEWER
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutBox = PrismLib.UI.Toolkit.KeyLayoutBox;
using KeyLayoutEditor = PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using MenuItem = Facet.MenuItem;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The edited layout's elements: what the canvas draws and the commands on a selection. Every read of a tab checks it is in
// the document first, since reading a missing tab's elements would create it.
internal sealed partial class KvLayoutModel {
    // The classic insert's sizes.
    private const float KeyW = 60f, KeyH = 60f, StatW = 100f, StatH = 30f, GraphW = 200f, GraphH = 100f;
    private JObject styleClip;

    // The classic canvas: twice its centre.
    public override Vector2 CanvasSize => new(KvSnap.CanvasCenterX * 2f, KvSnap.CanvasCenterY * 2f);

    public override IReadOnlyList<KvElement> Elements() => ElementsOf(KvStore.Current, Editing);

    // The companion tab: the shown foot tab while a hand tab is edited, the shown hand tab while a foot tab is.
    public override IReadOnlyList<KvElement> Ghosts() {
        KvDocument doc = KvStore.Current;
        string tab = Editing;
        if(!doc.HasTab(tab)) return [];
        string other = doc.IsFootTab(tab) ? doc.SelectedTab : doc.SelectedFootTab;
        return other == tab ? [] : ElementsOf(doc, other);
    }

    private static List<KvElement> ElementsOf(KvDocument doc, string tab) => tab != null && doc.HasTab(tab) ? doc.AllElements(tab) : [];

    // An element of the document a replace just swapped out keeps the key it had there, so the canvas re-finds the
    // selection (and the inspector its rows) in the new document by key.
    private KvDocument keyDoc, lastKeyDoc;
    // Each element's key with the list and index it was found at, for this document and layout: a row's drag asks for every
    // selected element's key each frame, so a key still at its place is returned without a search or a new string.
    private readonly Dictionary<KvElement, (string key, List<KvElement> list, int index)> keys = [];
    private string keysTab;

    public override string Key(KvElement e) {
        KvDocument doc = KvStore.Current;
        string tab = Editing;
        if(!ReferenceEquals(doc, keyDoc)) {
            (lastKeyDoc, keyDoc) = (keyDoc, doc);
            keys.Clear();
        }
        if(!string.Equals(tab, keysTab, StringComparison.Ordinal)) {
            keys.Clear();
            keysTab = tab;
        }
        if(e == null) return null;
        if(keys.TryGetValue(e, out var hit) && hit.index < hit.list.Count && ReferenceEquals(hit.list[hit.index], e)) return hit.key;
        if(KvEditOps.Key(doc, tab, e) is { } key) {
            List<KvElement> list = doc.Elements(tab, e.Kind);
            keys[e] = (key, list, list.IndexOf(e));
            return key;
        }
        return lastKeyDoc == null ? null : KvEditOps.Key(lastKeyDoc, tab, e);
    }

    // A key is "<tab>/<kind>:<index>", so its tab is everything before the last slash.
    public override KvElement Find(string key) {
        int cut = key?.LastIndexOf('/') ?? -1;
        return cut <= 0 ? null : KvEditOps.Find(KvStore.Current, key![..cut], key);
    }

    public override Rect GetRect(KvElement e) => new(e.X, e.Y, e.W, e.H);

    public override void SetRect(KvElement e, Rect r) {
        e.MoveTo(r.x, r.y);
        e.Resize(r.width, r.height);
    }

    public override bool Hidden(KvElement e) => e.Hidden;
    public override void Draw(VisualElement box, KvElement e) => KvElementView.Draw(box, e);

    // Key, Stat ▸ (KPS, Avg, Max, Total) and Graph; existing Knobs stay editable, but none is inserted.
    public override IList<MenuItem> InsertItems(KeyLayoutEditor editor) => [
        new() { Id = "KVI_ADD_KEY", Text = "KVI_ADD_KEY", Run = () => {
            if(Insert(editor, KvElementKind.Key, null) is { } e) Rebind(editor, [e]);
        } },
        new() {
            Id = "KVI_ADD_STAT", Text = "KVI_ADD_STAT",
            Items = [StatItem(editor, "KVI_STAT_KPS", "kps"), StatItem(editor, "KVI_STAT_KPSAVG", "kpsAvg"),
                StatItem(editor, "KVI_STAT_KPSMAX", "kpsMax"), StatItem(editor, "KVI_STAT_TOTAL", "total")],
        },
        new() { Id = "KVI_ADD_GRAPH", Text = "KVI_ADD_GRAPH", Run = () => Insert(editor, KvElementKind.Graph, "kps") },
    ];

    private MenuItem StatItem(KeyLayoutEditor editor, string key, string stat) =>
        new() { Id = key, Text = key, Run = () => Insert(editor, KvElementKind.Stat, stat) };

    // One step: a new element centred on the view, on top of the tab, then selected. Null, with no step, when the tab is gone.
    private KvElement Insert(KeyLayoutEditor editor, KvElementKind kind, string stat) {
        (float w, float h) = kind switch {
            KvElementKind.Graph => (GraphW, GraphH),
            KvElementKind.Stat => (StatW, StatH),
            _ => (KeyW, KeyH),
        };
        string tab = Editing;
        if(!KvStore.Current.HasTab(tab)) return null;
        Vector2 at = editor.Canvas.ViewCentre;
        KvElement e = KvElement.Wrap(KvPresets.NewPosition(Mathf.Round(at.x - w * 0.5f), Mathf.Round(at.y - h * 0.5f), w, h,
            KvPresets.NewElementStyle(kind != KvElementKind.Key)), kind);
        if(kind == KvElementKind.Graph) e.Raw["graphType"] = "line";
        if(stat != null) e.StatType = stat;
        editor.Do(KeyLayoutText.Insert, () => {
            KvDocument doc = KvStore.Current;
            float top = -1f;
            foreach(KvElement o in doc.AllElements(tab)) top = Math.Max(top, o.Z);
            e.Z = top + 1f;
            doc.Add(tab, e);
            doc.ReindexZOrder(tab);
        });
        editor.Select([e]);
        return e;
    }

    public override IReadOnlyList<KvElement> Duplicate(IReadOnlyList<KvElement> items) => KvEditOps.Duplicate(KvStore.Current, Editing, items);
    public override void Delete(IReadOnlyList<KvElement> items) => KvEditOps.Delete(KvStore.Current, Editing, items);
    public override string Copy(IReadOnlyList<KvElement> items) => KvEditOps.Copy(items);
    public override IReadOnlyList<KvElement> Paste(string clip) => KvEditOps.Paste(KvStore.Current, Editing, clip) ?? [];

    public override bool CopyStyle(KvElement item) {
        if(item == null) return false;
        styleClip = item.CopyStyle();
        return true;
    }

    public override bool CanPasteStyle => styleClip != null;

    public override void PasteStyle(IReadOnlyList<KvElement> items) {
        if(styleClip == null || items == null) return;
        foreach(KvElement e in items) e.PasteStyle(styleClip);
    }

    public override void SetOrder(IReadOnlyList<KvElement> drawOrder) => KvEditOps.SetOrder(KvStore.Current, Editing, drawOrder);
    public override void SetHidden(IReadOnlyList<KvElement> items, bool hidden) => KvEditOps.SetHidden(items, hidden);
    public override bool CanRebind(KvElement e) => e?.Kind == KvElementKind.Key;

    // One capture per key, in order; each captured key is one step. Escape (KeyCode.None) ends the walk. The elements are
    // found again by key when their key arrives: an undo meanwhile replaces the document. When no capture follows (Escape,
    // the last key, or a capture that could not start) the resting status line comes back.
    public override void Rebind(KeyLayoutEditor editor, IReadOnlyList<KvElement> inOrder) {
        List<string> keys = [.. (inOrder ?? []).Where(CanRebind).Select(Key).Where(k => k != null)];
        void Next(int i) {
            if(i >= keys.Count) return;
            editor.Say(QuartzHost.Translate("KVI_LISTENING") + $" ({i + 1}/{keys.Count})");
            KeyCaptureService.Start(k => {
                if(k == KeyCode.None) return;
                if(Find(keys[i]) is { } e) editor.Do(KeyLayoutText.Rebind, () => e.BindKey(k));
                Next(i + 1);
            }, () => {
                if(!KeyCaptureService.IsCapturing) editor.Status.Show(StatusText);
            });
        }
        Next(0);
    }

    public override string InspectorTitle(IReadOnlyList<KvElement> selection) => KvInspectorPages.Title(selection);

    public override IList<global::Facet.NavItem> Inspector(IReadOnlyList<KvElement> selection) {
        Inspected.Tab = Editing;
        return KvInspectorPages.For(selection, Inspected);
    }

    // Static lookups only: every row step keeps these delegates for the session.
    private static readonly KvInspectorContext Inspected = new() {
        Selection = () => KvEditorPage.Editor?.Canvas.Selection ?? [],
        All = () => KvEditorPage.Model.Elements(),
        Key = e => KvEditorPage.Model.Key(e),
        UndoDocument = () => KvEditorPage.Model.UndoDocument(),
        Changed = () => KvEditorPage.Editor?.Changed(),
        Dirty = () => KvEditorPage.Editor?.MarkDirty(),
    };

    // A switcher card's boxes: each element's rectangle in its idle fill and border colours.
    public override void Thumb(string layout, List<KeyLayoutBox> into) {
        KvDocument doc = KvStore.Current;
        if(layout != null && doc.HasTab(layout)) ThumbBoxes(doc, layout, into);   // a missing tab is never created by a read
    }

    private static void ThumbBoxes(KvDocument doc, string tab, List<KeyLayoutBox> into) {
        foreach(KvElement e in ElementsOf(doc, tab))
            into.Add(new KeyLayoutBox { Rect = new Rect(e.X, e.Y, e.W, e.H), Fill = KvElementView.Fill(e), Border = KvElementView.Border(e), Dim = e.Hidden });
    }
}
#endif
