using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.Utility;
public class UIScrollController : MonoBehaviour {
    public RectTransform content;
    public RectTransform viewport;
    public float scrollDuration = 0.2f;
    public Easing scrollEase = Easing.OutCirc;
    private bool rightDragging;
    private float targetY;
    private GTween scrollTween;
    private void Awake() {
        if(content != null) targetY = content.anchoredPosition.y;
    }
    private void Update() {
        if(content == null || viewport == null) return;
        HandleWheel();
        HandleRightDrag();
    }
    private bool PointerOverViewport() =>
        viewport != null && RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, null);
    private static readonly List<RectTransform> captures = [];
    /// <summary>
    /// Register a region that takes the wheel and the right button for itself (a zoom/pan
    /// surface). Needed because this controller polls Input rather than implementing
    /// IScrollHandler, so a nested widget has no event to consume and would otherwise scroll the
    /// page as well as itself.
    /// </summary>
    public static void AddInputCapture(RectTransform region) {
        if(region != null && !captures.Contains(region)) captures.Add(region);
    }
    public static void RemoveInputCapture(RectTransform region) => captures.Remove(region);
    /// <summary>
    /// Checked as a predicate rather than a frame-scoped claim so it holds regardless of which
    /// Update runs first.
    ///
    /// A controller never yields to its own viewport: a region is registered so the page behind it
    /// keeps its hands off, not so the scroll inside it refuses to work. Without the exemption a
    /// scroll view nested in a page — the layout editor's property panel — could not both take the
    /// wheel from the page and use it.
    /// </summary>
    private static bool PointerOverCapture(RectTransform self) {
        for(int i = captures.Count - 1; i >= 0; i--) {
            RectTransform region = captures[i];
            if(region == null) {
                captures.RemoveAt(i);
                continue;
            }
            if(region == self) continue;
            if(region.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(region, Input.mousePosition, null)) return true;
        }
        return false;
    }
    private void HandleWheel() {
        float wheel = Input.mouseScrollDelta.y;
        if(Mathf.Abs(wheel) <= 0.0001f) return;
        if(!PointerOverViewport()) return;
        if(PointerOverCapture(viewport)) return;
        AddDelta(-wheel * MainCore.Conf.ScrollSpeed);
        ApplyTween();
    }
    private void HandleRightDrag() {
        // A capture region owns the right button too — it pans with it, and the page jumping to
        // the pointer at the same time would fight that. Only the press is gated: a drag that
        // began on the page keeps scrolling after it wanders over the canvas.
        if(Input.GetMouseButtonDown(1) && PointerOverViewport() && !PointerOverCapture(viewport)) rightDragging = true;
        if(Input.GetMouseButtonUp(1)) {
            rightDragging = false;
            ApplyTween();
        }
        if(!rightDragging) return;
        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
        if(maxOffset <= 0f) return;
        Vector2 mouse = Input.mousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, mouse, null, out Vector2 local);
        float normalized = 1f - Mathf.Clamp01((local.y + (viewportHeight * 0.5f)) / viewportHeight);
        targetY = normalized * maxOffset;
        ApplyTween();
    }
    private void AddDelta(float deltaPixels) {
        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
        targetY += deltaPixels;
        targetY = Mathf.Clamp(targetY, 0f, maxOffset);
    }
    private void ApplyTween() {
        scrollTween?.Kill();
        scrollTween = GTweenExtensions.Tween(
            () => content.anchoredPosition.y,
            x => content.anchoredPosition = new Vector2(content.anchoredPosition.x, x),
            targetY,
            scrollDuration
        )
        .SetEasing(scrollEase);
        MainCore.TC.Play(scrollTween);
    }
    public void ScrollTo(float y) {
        if(content == null || viewport == null) return;
        float maxOffset = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        targetY = Mathf.Clamp(y, 0f, maxOffset);
        ApplyTween();
    }
    public void SetContent(RectTransform content, RectTransform viewport) {
        this.content = content;
        this.viewport = viewport;
        if(content != null) targetY = content.anchoredPosition.y;
    }
}
