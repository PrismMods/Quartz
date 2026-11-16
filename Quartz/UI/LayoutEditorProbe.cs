#if !IL2CPP
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI;
/// <summary>
/// Whether PrismLib's shared layout editor is on screen, whichever mod draws it. PrismLib has no such signal; its surface is
/// the UIDocument named PrismLayoutEditor, hidden with display:none. Each host builds its own document and a hidden one
/// keeps its panel, so every such root is kept. Only Unity types are named here, so it never loads PrismLib.
/// </summary>
internal static class LayoutEditorProbe {
    private static readonly List<VisualElement> roots = [];
    private static int rescanUntil = -1; // after Forget: the last frame whose calls rescan while nothing shows
    internal static bool Shown() => ShownRoot() != null;
    /// <summary>The displayed editor root, or null.</summary>
    internal static VisualElement ShownRoot() {
        bool scan = roots.Count == 0 || Time.frameCount <= rescanUntil;
        for(int i = 0; !scan && i < roots.Count; i++) scan = roots[i].panel == null;
        if(scan) Scan();
        for(int i = 0; i < roots.Count; i++)
            if(roots[i].style.display.value != DisplayStyle.None) {
                rescanUntil = -1;
                return roots[i];
            }
        return null;
    }
    /// <summary>A host may have built its document since the last scan: the next calls rescan, until one shows or 10 frames pass.</summary>
    internal static void Forget() => rescanUntil = Time.frameCount + 10;
    private static void Scan() {
        roots.Clear();
        foreach(UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if(d.name == "PrismLayoutEditor" && d.rootVisualElement != null) roots.Add(d.rootVisualElement);
    }
}
#endif
