#if !IL2CPP && !QUARTZ_KEYVIEWER
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutEditor = PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The edited layout's elements: what the canvas draws and the commands on a selection. Until they are filled the canvas is
// empty and the commands do nothing.
internal sealed partial class KvLayoutModel {
    // The classic canvas: twice its centre.
    public override Vector2 CanvasSize => new(KvSnap.CanvasCenterX * 2f, KvSnap.CanvasCenterY * 2f);
    public override IReadOnlyList<KvElement> Elements() => [];
    public override IReadOnlyList<KvElement> Ghosts() => [];
    public override string Key(KvElement e) => null;
    public override KvElement Find(string key) => null;
    public override Rect GetRect(KvElement e) => default;
    public override void SetRect(KvElement e, Rect r) { }
    public override bool Hidden(KvElement e) => false;
    public override void Draw(VisualElement box, KvElement e) { }
    public override IList<global::Facet.MenuItem> InsertItems(KeyLayoutEditor editor) => [];
    public override IReadOnlyList<KvElement> Duplicate(IReadOnlyList<KvElement> items) => [];
    public override void Delete(IReadOnlyList<KvElement> items) { }
    public override string Copy(IReadOnlyList<KvElement> items) => null;
    public override IReadOnlyList<KvElement> Paste(string clip) => [];
    public override bool CopyStyle(KvElement item) => false;
    public override bool CanPasteStyle => false;
    public override void PasteStyle(IReadOnlyList<KvElement> items) { }
    public override void SetOrder(IReadOnlyList<KvElement> drawOrder) { }
    public override void SetHidden(IReadOnlyList<KvElement> items, bool hidden) { }
    public override bool CanRebind(KvElement e) => false;
    public override void Rebind(KeyLayoutEditor editor, IReadOnlyList<KvElement> inOrder) { }
    public override string InspectorTitle(IReadOnlyList<KvElement> selection) => "";
    public override IList<global::Facet.NavItem> Inspector(IReadOnlyList<KvElement> selection) => [];
}
#endif
