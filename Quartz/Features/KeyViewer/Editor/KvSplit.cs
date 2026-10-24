using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed class KvSplit : MonoBehaviour {
    internal RectTransform Pane;
    internal RectTransform CanvasHost;
    internal RectTransform Divider;
    internal float DividerWidth;
    private RectTransform self;
    private float desired;
    private float appliedFor = float.NaN;
    private void Awake() => self = (RectTransform)transform;
    private float MaxWidth() =>
        Mathf.Max(KvWidgets.MinPaneWidth, (self.rect.width - DividerWidth) * 0.5f);
    private float Clamp(float w) => Mathf.Clamp(w, KvWidgets.MinPaneWidth, MaxWidth());
    internal float Applied => Clamp(desired);
    internal void SetWidth(float width) {
        desired = Mathf.Max(KvWidgets.MinPaneWidth, width);
        Apply();
    }
    private void Apply() {
        if(self == null || Pane == null || CanvasHost == null || Divider == null) return;
        float width = Clamp(desired);
        Pane.sizeDelta = new Vector2(width, Pane.sizeDelta.y);
        Divider.anchoredPosition = new Vector2(-width, 0f);
        CanvasHost.offsetMax = new Vector2(-(width + DividerWidth), 0f);
        appliedFor = self.rect.width;
    }
    private void LateUpdate() {
        if(self == null) return;
        if(Mathf.Approximately(appliedFor, self.rect.width)) return;
        Apply();
    }
}
