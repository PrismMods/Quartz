#if !IL2CPP && !QUARTZ_KEYVIEWER
using KeyLayoutEditor = PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The File menu and the tab actions. Until they are filled they offer nothing and do nothing.
internal sealed partial class KvLayoutModel {
    public override string LimitText() => null;
    public override IList<global::Facet.MenuItem> NewItems(LayoutGroup group, Action<string> created) => [];
    public override string Duplicate(string layout) => null;
    public override void Delete(string layout) { }
    public override void Rename(string layout, string name) { }
    public override void Move(string layout, int indexInGroup) { }
    public override void Export(string layout) { }
    public override IList<global::Facet.MenuItem> FileItems(KeyLayoutEditor editor) => [];
}
#endif
