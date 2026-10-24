using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
internal sealed class KvGrid : MaskableGraphic {
    private const float MinScreenPitch = 5f;
    private const float LineWidth = 1f;
    private float pitch = -1f;
    private Vector2 offset = new(float.NaN, float.NaN);
    internal void Sync(float zoom, Vector2 pan) {
        float scaled = KvSnap.GridSnap * Mathf.Max(zoom, 0.0001f);
        while(scaled < MinScreenPitch) scaled *= 2f;
        Vector2 next = new(Mod(pan.x, scaled), Mod(-pan.y, scaled));
        if(Mathf.Abs(scaled - pitch) < 0.001f
            && Mathf.Abs(next.x - offset.x) < 0.001f
            && Mathf.Abs(next.y - offset.y) < 0.001f) return;
        pitch = scaled;
        offset = next;
        SetVerticesDirty();
    }
    private static float Mod(float value, float m) {
        if(m <= 0f) return 0f;
        float r = value % m;
        return r < 0f ? r + m : r;
    }
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        if(pitch <= 0f) return;
        Rect r = rectTransform.rect;
        if(r.width <= 1f || r.height <= 1f) return;
        Color32 tint = KvPalette.GridLine;
        for(float x = r.xMin + offset.x - pitch; x <= r.xMax; x += pitch) {
            if(x + LineWidth < r.xMin) continue;
            AddQuad(vh, x, r.yMin, x + LineWidth, r.yMax, tint);
        }
        for(float y = r.yMax - offset.y + pitch; y >= r.yMin; y -= pitch) {
            if(y - LineWidth > r.yMax) continue;
            AddQuad(vh, r.xMin, y - LineWidth, r.xMax, y, tint);
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
