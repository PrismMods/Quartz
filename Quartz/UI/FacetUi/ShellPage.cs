#if !IL2CPP
using Facet;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The frame of a moved feature page: its header with the feature's switch and a one-line description, then its cards.
/// A page whose feature puts an element on screen has two columns, the header and cards left and the element's live
/// preview right, with emptyKey's reason under it while it has nothing to show. The header sits over its cards, so its
/// switch ends on their rows' control edge.
/// </summary>
internal static class ShellPage {
    internal static void Feature(VisualElement p, string featureId, string titleKey, string descKey, string overlayId, Action<VisualElement> cards,
        Func<string> emptyKey = null) {
        if(overlayId == null) {
            Header(p, featureId, titleKey, descKey);
            cards(p);
            return;
        }
        Facet.Columns cols = Controls.Columns(p, o => {
            o.Left = 1.25f;
            o.Right = 1f;
            o.StackBelow = 760f;
        });
        Header(cols.Left, featureId, titleKey, descKey);
        cards(cols.Left);
        FacetOverlayPreview.Pane(cols.Right, overlayId, emptyKey);
    }

    private static void Header(VisualElement p, string featureId, string titleKey, string descKey) =>
        Controls.PageHeader(p, titleKey, QuartzShell.FeatureSwitch(featureId), o => {
            o.Id = titleKey;
            o.Description = descKey;
        });
}
#endif
