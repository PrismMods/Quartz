using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Prism;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Utility;
public class UIScrollController : MonoBehaviour {
    public RectTransform content;
    public RectTransform viewport;
    public float scrollDuration = 0.2f;
    public Easing scrollEase = Easing.OutCirc;
    private bool rightDragging;
    private float targetY;
    private GTween scrollTween;
    private float tweenedTo = float.NaN;
    private void Awake() {
        if(content != null) targetY = content.anchoredPosition.y;
    }
    private void Update() {
        if(content == null || viewport == null) return;
        HandleWheel();
        HandleRightDrag();
        // Content that shrank under the offset (an open section deleted at the page's end) left the view past its end, blank,
        // until the next wheel notch. Checked here, a frame on: a deleted row is destroyed and laid out only after the frame.
        float max = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        if(targetY > max) {
            targetY = max;
            ApplyTween();
        }
    }
    private bool PointerOverViewport() =>
        viewport != null && RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, null);
    private static readonly List<RectTransform> captures = [];
    public static void AddInputCapture(RectTransform region) {
        if(region != null && !captures.Contains(region)) captures.Add(region);
    }
    public static void RemoveInputCapture(RectTransform region) => captures.Remove(region);
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
        if(!PrismBridge.SmoothScroll) {   // PrismLib's Smooth scrolling off: the wheel, a right-drag and ScrollTo land at once
            tweenedTo = targetY;
            scrollTween?.Kill();
            scrollTween = null;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
            return;
        }
        if(scrollTween != null && tweenedTo.Equals(targetY)) return;
        tweenedTo = targetY;
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
    public float Offset => targetY;
    public void JumpTo(float y) {
        if(content == null || viewport == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float maxOffset = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        targetY = Mathf.Clamp(y, 0f, maxOffset);
        tweenedTo = targetY;
        scrollTween?.Kill();
        scrollTween = null;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
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
        tweenedTo = float.NaN;
        if(content != null) targetY = content.anchoredPosition.y;
    }
}
