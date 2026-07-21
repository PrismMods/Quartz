using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.ProgressBar;
internal sealed class SegmentedBarGraphic : MaskableGraphic {
    internal Func<float, Color> GradientEval;
    private int count = 64;
    private float gap = 2f;
    private Color onColor = Color.red;
    private Color offColor = Color.black;
    private int litFrom = -1;
    private int litTo = -1;
    private float lastWidth = float.NaN;
    private float lastHeight = float.NaN;
    private bool gradient;
    internal void SetLook(int segmentCount, float segmentGap, Color fill, Color back, bool useGradient) {
        segmentCount = Mathf.Clamp(segmentCount, 1, 512);
        if(!useGradient
            && count == segmentCount
            && Mathf.Approximately(gap, segmentGap)
            && onColor == fill
            && offColor == back
            && gradient == useGradient) return;
        count = segmentCount;
        gap = segmentGap;
        onColor = fill;
        offColor = back;
        gradient = useGradient;
        SetVerticesDirty();
    }
    internal void SetProgress(float start, float now) {
        int from = SegmentAt(start);
        int to = SegmentAt(now);
        Rect r = rectTransform.rect;
        if(from == litFrom && to == litTo
            && Mathf.Approximately(r.width, lastWidth)
            && Mathf.Approximately(r.height, lastHeight)) return;
        litFrom = from;
        litTo = to;
        lastWidth = r.width;
        lastHeight = r.height;
        SetVerticesDirty();
    }
    private int SegmentAt(float ratio) => Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(ratio) * count), 0, count);
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        Rect r = rectTransform.rect;
        if(r.width <= 0.5f || r.height <= 0.5f) return;
        float pitch = r.width / count;
        float w = Mathf.Max(1f, pitch - Mathf.Max(0f, gap));
        float pad = (pitch - w) * 0.5f;
        for(int i = 0; i < count; i++) {
            bool on = i >= litFrom && i < litTo;
            Color c = on && gradient && GradientEval != null
                ? GradientEval((i + 0.5f) / count)
                : on ? onColor : offColor;
            if(c.a <= 0.001f) continue;
            float x0 = r.xMin + i * pitch + pad;
            AddQuad(vh, x0, r.yMin, x0 + w, r.yMax, c);
        }
    }
    private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c) {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
        vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
        vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
        vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
