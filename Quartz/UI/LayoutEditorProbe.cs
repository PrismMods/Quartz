#if !IL2CPP
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI;
/// <summary>
/// Whether PrismLib's shared layout editor is on screen, whichever mod draws it. PrismLib has no such signal; its surface is
/// the UIDocument named PrismLayoutEditor, hidden with display:none. Only Unity types are named here, so it never loads PrismLib.
/// </summary>
internal static class LayoutEditorProbe {
    private static VisualElement root;
    internal static bool Shown() {
        if(root?.panel == null) {
            root = null;
            foreach(UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if(d.name == "PrismLayoutEditor" && d.rootVisualElement != null) { root = d.rootVisualElement; break; }
        }
        return root != null && root.style.display.value != DisplayStyle.None;
    }
}
#endif
