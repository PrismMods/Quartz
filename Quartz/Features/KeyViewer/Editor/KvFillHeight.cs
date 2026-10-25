using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.KeyViewer.Editor;
internal sealed class KvFillHeight : MonoBehaviour {
    internal RectTransform Content;
    internal RectTransform Viewport;
    private RectTransform self;
    private LayoutElement le;
    private void Awake() {
        self = (RectTransform)transform;
        le = GetComponent<LayoutElement>();
    }
    private Vector4 appliedFor = new(-1f, -1f, -1f, -1f);
    private void LateUpdate() {
        if(le == null || self == null || Content == null || Viewport == null) return;
        // The floor counts too: wrapped rows can shrink after the height is pinned, leaving every rect unchanged.
        float floor = LayoutUtility.GetMinHeight(self);
        Vector4 inputs = new(Content.rect.height, self.rect.height, Viewport.rect.height, floor);
        if(inputs == appliedFor) return;
        appliedFor = inputs;
        float rest = Content.rect.height - self.rect.height;
        float target = Mathf.Max(floor, Viewport.rect.height - rest);
        if(Mathf.Abs(target - le.preferredHeight) < 0.5f) return;
        le.preferredHeight = target;
    }
}
