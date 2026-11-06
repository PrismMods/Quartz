using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Quartz.UI.Objects;
public static class UIInputBlocker {
    private static GameObject lastSelected;
    private static TMP_InputField lastTmp;
    private static InputField lastLegacy;
    // Typing into a uGUI field or a Facet field: the hotkey paths stay quiet for both.
    public static bool IsEditing => UguiEditing || Quartz.UI.FacetUi.FacetBridge.TextFocused();
    private static bool UguiEditing {
        get {
            EventSystem es = EventSystem.current;
            if(es == null) return false;
            GameObject selected = es.currentSelectedGameObject;
            if(selected == null) return false;
            if(!ReferenceEquals(selected, lastSelected)) {
                lastSelected = selected;
                lastTmp = selected.GetComponent<TMP_InputField>();
                lastLegacy = lastTmp != null ? null : selected.GetComponent<InputField>();
            }
            if(lastTmp != null) return lastTmp.isFocused;
            return lastLegacy != null && lastLegacy.isFocused;
        }
    }
}
