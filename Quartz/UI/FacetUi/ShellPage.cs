#if !IL2CPP
using Facet;
using Quartz.UI.Utility;
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
        // The switch shares the classic Features page's hotkey: a write shows on its toggle, and a right-click binds it.
        string hotkey = "feature_" + featureId;
        QuartzShell.FeatureFrame(p, titleKey, QuartzShell.FeatureSwitch(featureId).Then(v => ToggleBinds.Show(hotkey, v)), descKey, cards,
            overlayId == null ? null : r => FacetOverlayPreview.Pane(r, overlayId, emptyKey));
        ShellRows.HeaderHotkey(p, titleKey, hotkey);
    }
}
#endif
