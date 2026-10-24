using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
internal sealed class KvFillHeight : MonoBehaviour {
    internal RectTransform Content;
    internal RectTransform Viewport;
    private RectTransform self;
    private LayoutElement le;
    private void Awake() {
        self = (RectTransform)transform;
        le = GetComponent<LayoutElement>();
    }
    private Vector3 appliedFor = new(-1f, -1f, -1f);
    private void LateUpdate() {
        if(le == null || self == null || Content == null || Viewport == null) return;
        Vector3 inputs = new(Content.rect.height, self.rect.height, Viewport.rect.height);
        if(inputs == appliedFor) return;
        appliedFor = inputs;
        float floor = LayoutUtility.GetMinHeight(self);
        float rest = Content.rect.height - self.rect.height;
        float target = Mathf.Max(floor, Viewport.rect.height - rest);
        if(Mathf.Abs(target - le.preferredHeight) < 0.5f) return;
        le.preferredHeight = target;
    }
}
