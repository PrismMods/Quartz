#if !IL2CPP && !QUARTZ_KEYVIEWER
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using KeyLayoutBox = PrismLib.UI.Toolkit.KeyLayoutBox;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
/// <summary>
/// The key viewer layouts as the shared editor sees them, over KvStore.Current. Every editor change mutates the current
/// document in place; only Restore replaces it. One per session (KvEditorPage.Model), so its epoch outlives a page rebuild
/// and the undo of an edit made with the page away is still saved.
/// </summary>
internal sealed partial class KvLayoutModel : PrismLib.UI.Toolkit.KeyLayoutModel<KvElement> {
    private const string ImagesKey = "embeddedLocalImages";
    private readonly KvEditEpoch epoch = new(() => Time.frameCount);
    private string editing;
    private int committedFrame = -1;
#if DEBUG
    /// <summary>How many times an edit or an undo asked the overlay to rebuild; the self-test reads it.</summary>
    internal static int OverlayRebuildRequests;
#endif

    internal KvLayoutModel() {
        editing = KvStore.Current.SelectedTab;
        // An undo can happen with the editor off a panel (the header's undo on another page): its save is the model's.
        global::Facet.History.AfterApply += Committed;
    }

    public override global::Facet.Host Host => QuartzHost.Host;
    public override string Label(string id, string english) => KvLabels.KeyFor(id) ?? english;
    public override object UndoDocument() => epoch.Token(KvStore.Current);

    // The tab being edited, a newline, then the document without its embedded images: those can be megabytes, and an undo
    // keeps the live table (an import captures it on its own).
    public override string Capture() {
        KeyViewerOverlay.FlushCounts();
        KvDocument doc = KvStore.Current;
        epoch.Own(doc);
        JProperty images = doc.Root.Property(ImagesKey);
        JToken table = images?.Value;
        if(images != null) images.Value = JValue.CreateNull();
        try {
            return Editing + "\n" + doc.ToJson();
        } finally {
            if(images != null) images.Value = table!;
        }
    }

    public override void Restore(string snapshot) => Restore(snapshot, KvStore.Current.Root.Property(ImagesKey));

    // images: the table the restored document gets (detached from its owner, not copied); null leaves it with none.
    private void Restore(string snapshot, JProperty images) {
        int cut = snapshot.IndexOf('\n');
        string tab = cut < 0 ? null : snapshot[..cut];
        KeyViewerOverlay.FlushCounts();
        KvDocument live = KvStore.Current, next = KvDocument.Parse(cut < 0 ? snapshot : snapshot[(cut + 1)..]);
        next.KeepCountsFrom(live);
        next.Root.Remove(ImagesKey);
        if(images != null) {
            images.Remove();
            next.Root.Add(images);
        }
        KvStore.Replace(next);
        epoch.Adopt(next);
        if(tab != null) editing = tab;   // Editing falls back if that tab is gone
        KeyViewerOverlay.Rebuild();   // its boxes held the replaced elements
    }

    // Once a frame: a canvas undo can reach here both from the canvas and from History.AfterApply.
    public override void Committed() {
        if(committedFrame == Time.frameCount) return;
        committedFrame = Time.frameCount;
        KvStore.RequestSave();
        KeyViewerOverlay.RequestLayoutRebuild();
#if DEBUG
        OverlayRebuildRequests++;
#endif
    }

    public override bool Pref(string id) => id switch {
        KeyLayoutText.Snap => MainCore.Conf.KvSnapToGrid,
        KeyLayoutText.Grid => MainCore.Conf.KvShowGrid,
        KeyLayoutText.Guides => MainCore.Conf.KvShowGuides,
        _ => false,
    };

    public override void SetPref(string id, bool on) {
        switch(id) {
            case KeyLayoutText.Snap: MainCore.Conf.KvSnapToGrid = on; break;
            case KeyLayoutText.Grid: MainCore.Conf.KvShowGrid = on; break;
            case KeyLayoutText.Guides: MainCore.Conf.KvShowGuides = on; break;
            default: return;
        }
        MainCore.ConfMgr.RequestSave();
    }

    // ---- layouts

    public override IReadOnlyList<string> Layouts(LayoutGroup group) {
        KvDocument doc = KvStore.Current;
        bool feet = group == LayoutGroup.Feet;
        return [.. doc.Tabs.Where(t => doc.IsFootTab(t) == feet)];
    }

    public override string Name(string layout) => KvStore.Current.TabName(layout);

    // Only while the document has it: reading a missing tab's elements would create it, and the next save would write it.
    public override string Editing {
        get {
            KvDocument doc = KvStore.Current;
            if(!doc.HasTab(editing)) editing = doc.SelectedTab;
            return editing;
        }
    }

    public override string Shown(LayoutGroup group) =>
        group == LayoutGroup.Feet ? KvStore.Current.SelectedFootTab : KvStore.Current.SelectedTab;

    public override void Show(LayoutGroup group, string layout) {
        KvDocument doc = KvStore.Current;
        if(layout != null && !doc.HasTab(layout)) return;
        if(group == LayoutGroup.Feet) doc.SelectedFootTab = layout;
        else doc.SelectedTab = layout;
        if(layout != null) editing = layout;
    }

    // Filled with the layout switcher.
    public override void Thumb(string layout, List<KeyLayoutBox> into) { }

    public override string StatusText() {
        KvDocument doc = KvStore.Current;
        string tab = Editing;
        int count = doc.HasTab(tab) ? doc.AllElements(tab).Count : 0;
        return string.Format(QuartzHost.Translate("KEYVIEWER_EDITOR_STATUS"), count, doc.TabName(tab));
    }
}
#endif
