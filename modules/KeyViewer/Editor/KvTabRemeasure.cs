using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
internal sealed class KvTabRemeasure : MonoBehaviour {
    private readonly List<(LayoutElement Le, TextMeshProUGUI Label, float Pad)> pills = [];
    private RectTransform track;
    private LayoutElement viewport;
    private float gap, minWidth, maxWidth;
    private float lastTotal = -1f;
    private int stableFrames;
    private int frames;
    private const int MaxFrames = 120;
    private const int StableThreshold = 2;
    internal static void Attach(
        RectTransform track,
        List<(LayoutElement Le, TextMeshProUGUI Label, float Pad)> pills,
        LayoutElement viewport = null, float gap = 0f, float minWidth = 0f, float maxWidth = 0f
    ) {
        if(track == null || pills == null || pills.Count == 0) return;
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
