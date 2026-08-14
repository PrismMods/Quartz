using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Quartz.UI.Objects;
public static class UIInputBlocker {
    public static bool IsEditing {
        get {
            EventSystem es = EventSystem.current;
            if(es == null) return false;
            GameObject selected = es.currentSelectedGameObject;
            if(selected == null) return false;
            TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
            if(tmp != null && tmp.isFocused) return true;
            InputField legacy = selected.GetComponent<InputField>();
            return legacy != null && legacy.isFocused;
        }
    }
}
