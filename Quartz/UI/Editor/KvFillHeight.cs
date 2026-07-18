using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
/// <summary>
/// Sizes a region of a scrollable page to whatever is left of that page's viewport, so an editor
/// carrying its own chrome is the page rather than the first item on it.
///
/// The rest of the page is measured as (content height - own height) rather than by adding up
/// siblings: the region sits several levels below the scroll content, so there is no single list
/// of rows to total, and the difference already accounts for every level at once. Both reads come
/// out of the same layout pass, so the difference is exact even before the first pass has run,
/// and the result is idempotent — this settles the frame after a resize and then holds.
///
/// Polled rather than driven from OnRectTransformDimensionsChange: the answer depends on the page
/// viewport, which moves for reasons this rect never hears about — the panel resizing, the sidebar
/// sliding open, the context band opening underneath it. Two rect reads, and only while the region
/// is the visible mode.
/// </summary>
internal sealed class KvFillHeight : MonoBehaviour {
    internal RectTransform Content;
    internal RectTransform Viewport;
    private RectTransform self;
    private LayoutElement le;
    private void Awake() {
        self = (RectTransform)transform;
        le = GetComponent<LayoutElement>();
    }
    private void LateUpdate() {
        if(le == null || self == null || Content == null || Viewport == null) return;
        // The layout group's own minimum rather than a constant: a group handed less than its
        // children's minimums lays them out past its rect instead of compressing them, so this is
        // the floor below which the page has to scroll instead. Readable only because the
        // LayoutElement leaves minHeight at -1 — a set minimum outranks the group's and would
        // answer with the value this just wrote.
        float floor = LayoutUtility.GetMinHeight(self);
        float rest = Content.rect.height - self.rect.height;
        float target = Mathf.Max(floor, Viewport.rect.height - rest);
        if(Mathf.Abs(target - le.preferredHeight) < 0.5f) return;
        le.preferredHeight = target;
    }
}
