using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.Utility;
// Horizontal wheel scroll for overflowing strips; polls input so child EventTriggers cannot swallow it.
public sealed class StripWheel : MonoBehaviour {
    private RectTransform viewport, content;
    private bool captured;
    public void Init(RectTransform viewport, RectTransform content) {
        this.viewport = viewport;
        this.content = content;
    }
    private void OnDestroy() => SetCaptured(false);
    private void SetCaptured(bool value) {
        if(value == captured) return;
        captured = value;
        if(value) UIScrollController.AddInputCapture(viewport);
        else if(viewport != null) UIScrollController.RemoveInputCapture(viewport);
    }
    private void Update() {
        if(viewport == null || content == null) return;
        float overflow = content.rect.width - viewport.rect.width;
        SetCaptured(overflow > 0.5f);
        if(overflow <= 0f) return;
        Vector2 wheel = Input.mouseScrollDelta;
        float delta = wheel.y + wheel.x;
        if(Mathf.Abs(delta) <= 0.0001f) return;
        if(!RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, null)) return;
        Vector2 pos = content.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x + delta * MainCore.Conf.ScrollSpeed, -overflow, 0f);
        content.anchoredPosition = pos;
    }
}
