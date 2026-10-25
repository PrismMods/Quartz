using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Utility;
// Grows a fixed-height row to fit wrapped text, and shrinks it back.
public sealed class GrowRow : MonoBehaviour {
    private TMP_Text text;
    private LayoutElement row;
    private float baseHeight;
    private float padding;
    private float lastWidth = -1f;
    private string lastText;
    private float lastSize;
    public static void Attach(TMP_Text text, float padding = 6f) {
        if(text == null) return;
        // Only the text's own row, never a section further up.
        Transform parent = text.transform.parent;
        LayoutElement row = parent != null ? parent.GetComponent<LayoutElement>() : null;
        if(row == null && parent != null && parent.parent != null) row = parent.parent.GetComponent<LayoutElement>();
        if(row == null) return;
        GrowRow grow = text.gameObject.AddComponent<GrowRow>();
        grow.text = text;
        grow.row = row;
        grow.baseHeight = Mathf.Max(row.preferredHeight, row.minHeight);
        grow.padding = padding;
    }
    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();
    private void Refresh() {
        if(text == null || row == null) return;
        float width = text.rectTransform.rect.width;
        if(width <= 1f) return;
        if(Mathf.Approximately(width, lastWidth) && text.text == lastText && Mathf.Approximately(text.fontSize, lastSize)) return;
        lastWidth = width;
        lastText = text.text;
        lastSize = text.fontSize;
        float needed = text.GetPreferredValues(text.text, width, 0f).y + padding;
        float height = Mathf.Max(baseHeight, needed);
        if(Mathf.Approximately(row.preferredHeight, height)) return;
        row.minHeight = height;
        row.preferredHeight = height;
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)row.transform);
    }
}
