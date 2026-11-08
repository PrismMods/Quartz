#if !IL2CPP
using Facet;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The frame of a moved feature page: its header with the feature's switch and a one-line description, then its cards.
/// A page whose feature puts an element on screen has two columns, the cards left and the element's live preview right.
/// </summary>
internal static class ShellPage {
    internal static void Feature(VisualElement p, string featureId, string titleKey, string descKey, string overlayId, Action<VisualElement> cards) {
        Controls.PageHeader(p, titleKey, QuartzShell.FeatureSwitch(featureId), o => {
            o.Id = titleKey;
            o.Description = descKey;
        });
        if(overlayId == null) {
            cards(p);
            return;
        }
        VisualElement left = Controls.Columns(p, out VisualElement right, o => {
            o.Left = 1.25f;
            o.Right = 1f;
            o.StackBelow = 760f;
        });
        cards(left);
        FacetOverlayPreview.Pane(right, overlayId);
    }
}
#endif
