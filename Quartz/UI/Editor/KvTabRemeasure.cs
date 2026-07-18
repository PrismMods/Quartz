using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
/// <summary>
/// Re-sizes content-fit tab pills for a few frames after they are built, until their widths settle.
///
/// A pill's width is <c>label.GetPreferredValues(text).x</c>, and TMP returns a wrong (usually tiny)
/// value while a dynamic font's atlas has not populated yet — which is exactly the state on the
/// first frame after a game restart, so every tab renders ellipsized ("16 Keys" → "1…"). The
/// measurement is never wrong twice: once the atlas is ready it is stable. So this reapplies the
/// widths each LateUpdate until they stop changing, then removes itself — no permanent per-frame
/// cost, and no reliance on catching the one frame the atlas becomes ready.
/// </summary>
internal sealed class KvTabRemeasure : MonoBehaviour {
    private readonly List<(LayoutElement Le, TextMeshProUGUI Label, float Pad)> pills = [];
    private RectTransform track;
    // Optional: a strip whose own width is the sum of its pills (KvTabStrip's viewport) rather than
    // driven by a ContentSizeFitter (KvTabs). Null means the pills alone are re-sized.
    private LayoutElement viewport;
    private float gap, minWidth, maxWidth;
    private float lastTotal = -1f;
    private int stableFrames;
    // Backstop so a pill that never settles (e.g. an empty label) cannot keep this alive forever.
    private int frames;
    private const int MaxFrames = 120;
    private const int StableThreshold = 2;
    internal static void Attach(
        RectTransform track,
        List<(LayoutElement Le, TextMeshProUGUI Label, float Pad)> pills,
        LayoutElement viewport = null, float gap = 0f, float minWidth = 0f, float maxWidth = 0f
    ) {
        if(track == null || pills == null || pills.Count == 0) return;
        // A rebuild (tab set changed) supersedes an in-flight remeasure holding the old, now-cleared
        // pills; drop it rather than let the two overlap.
        if(track.TryGetComponent(out KvTabRemeasure stale)) Destroy(stale);
        KvTabRemeasure r = track.gameObject.AddComponent<KvTabRemeasure>();
        r.track = track;
        r.viewport = viewport;
        r.gap = gap;
        r.minWidth = minWidth;
        r.maxWidth = maxWidth;
        r.pills.AddRange(pills);
    }
    private void LateUpdate() {
        float total = 0f;
        int n = 0;
        foreach((LayoutElement le, TextMeshProUGUI label, float pad) in pills) {
            if(le == null || label == null) continue;
            float w = label.GetPreferredValues(label.text).x + pad;
            le.preferredWidth = w;
            le.minWidth = w;
            total += w;
            n++;
        }
        if(viewport != null) {
            float strip = total + gap * Mathf.Max(0, n - 1);
            viewport.preferredWidth = Mathf.Min(strip, maxWidth);
            viewport.minWidth = Mathf.Min(strip, minWidth);
        }
        if(track != null) LayoutRebuilder.MarkLayoutForRebuild(track);
        if(Mathf.Abs(total - lastTotal) < 0.5f) stableFrames++;
        else stableFrames = 0;
        lastTotal = total;
        if(++frames >= MaxFrames || stableFrames >= StableThreshold) Destroy(this);
    }
}
