#if !IL2CPP
using Facet;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The frame of a moved feature page: its header with the feature's switch and a one-line description, then its cards.
/// A page whose feature puts an element on screen has two columns, the header and cards left and the element's live
/// preview right, with emptyKey's reason under it while it has nothing to show. The header sits over its cards, so its
/// switch ends on their rows' control edge.
/// </summary>
internal static class ShellPage {
    /// <summary>A page's whole explanation goes in its header's description (wrapped), not a cut-off first row: the description
    /// label takes the note's id, so search and the checks still find the note by it.</summary>
    internal static void Described(VisualElement p, string headerId, string noteId) {
        if(p.Q(name: headerId) is not { } head) return;
        var labels = new List<Label>();
        Labels(head, labels);
        if(labels.Count > 1) labels[1].name = noteId;   // the first is the title
    }

    // In tree order, as Query finds them (Query's builder type is not in the CI stubs).
    private static void Labels(VisualElement e, List<Label> into) {
        foreach(VisualElement c in e.Children()) {
            if(c is Label l) into.Add(l);
            Labels(c, into);
        }
    }

    internal static void Feature(VisualElement p, string featureId, string titleKey, string descKey, string overlayId, Action<VisualElement> cards,
        Func<string> emptyKey = null, Action<Vector3> previewClick = null) {
        // The switch's hotkey is the feature's own (feature_<id>, which ToggleBinds flips directly): a right-click binds it.
        string hotkey = "feature_" + featureId;
        QuartzShell.FeatureFrame(p, titleKey, QuartzShell.FeatureSwitch(featureId), descKey, cards,
            overlayId == null ? null : r => FacetOverlayPreview.Pane(r, overlayId, emptyKey, previewClick));
        ShellRows.HeaderHotkey(p, titleKey, hotkey);
    }
}
#endif
