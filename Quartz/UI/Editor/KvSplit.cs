using UnityEngine;
namespace Quartz.UI.Editor;
/// <summary>
/// The canvas | divider | inspector split. Owns the one number the three share — the pane's
/// width — so the divider drag, the restore from config and the clamp all move the same rects
/// through the same path instead of each writing their own.
///
/// The three are anchored rather than laid out by a HorizontalLayoutGroup because the divider
/// drags <see cref="Pane"/>'s own sizeDelta, which a group controlling its children's widths
/// would overwrite on the next layout pass.
///
/// Polled rather than driven from OnRectTransformDimensionsChange for the reason on
/// <see cref="KvFillHeight"/>: the ceiling is a share of this rect's width, and that moves for
/// reasons this component never hears about — the panel being resized, the sidebar sliding open.
/// One rect read per frame, and only while the editor is the visible mode; the applied width
/// is recomputed only when the split itself actually changed size.
/// </summary>
internal sealed class KvSplit : MonoBehaviour {
    internal RectTransform Pane;
    internal RectTransform CanvasHost;
    internal RectTransform Divider;
    internal float DividerWidth;
    private RectTransform self;
    /// <summary>
    /// What the user asked for, which is not always what fits. Kept unclamped against the
    /// ceiling so that shrinking the panel and growing it back restores the pane they chose
    /// rather than leaving it at whatever the narrowest moment allowed.
    /// </summary>
    private float desired;
    private float appliedFor = float.NaN;
    private void Awake() => self = (RectTransform)transform;
    /// <summary>Half the split, so the canvas is never the smaller half of its own editor.</summary>
    private float MaxWidth() =>
        Mathf.Max(KvWidgets.MinPaneWidth, (self.rect.width - DividerWidth) * 0.5f);
    private float Clamp(float w) => Mathf.Clamp(w, KvWidgets.MinPaneWidth, MaxWidth());
    /// <summary>The width actually in use, which is what gets persisted.</summary>
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
