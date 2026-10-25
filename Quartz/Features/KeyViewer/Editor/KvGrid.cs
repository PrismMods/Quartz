using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.KeyViewer.Editor;
internal sealed class KvGrid : MaskableGraphic {
    private const float MinScreenPitch = 5f;
    private float pitch = -1f;
    private Vector2 offset = new(float.NaN, float.NaN);
    private Vector3 drawnAt = new(float.NaN, float.NaN, float.NaN);
    private float drawnScale = -1f;
    // Lines snap to screen pixels, so moving or rescaling the window has to redraw them too.
    private void LateUpdate() {
        Vector3 at = rectTransform.position;
        float scale = rectTransform.lossyScale.x;
        if(at == drawnAt && Mathf.Approximately(scale, drawnScale)) return;
        drawnAt = at;
        drawnScale = scale;
        SetVerticesDirty();
    }
    internal void Sync(float zoom, Vector2 pan) {
        float scaled = KvSnap.GridSnap * Mathf.Max(zoom, 0.0001f);
        // MinScreenPitch is in real pixels, so a scaled-down UI doesn't pack lines into a moire.
        float pixels = Mathf.Max(rectTransform.lossyScale.x, 0.0001f);
        while(scaled * pixels < MinScreenPitch) scaled *= 2f;
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
        // Overlay canvas: world units are screen pixels. Lines are exactly one pixel wide and sit on
        // pixel boundaries; a sub-pixel line (UI scale < 1) blinks in and out as the view pans.
        Vector3 scale = rectTransform.lossyScale;
        float sx = scale.x > 0.0001f ? scale.x : 1f;
        float sy = scale.y > 0.0001f ? scale.y : 1f;
        Vector3 origin = rectTransform.position;
        float lineX = 1f / sx;
        float lineY = 1f / sy;
        for(float x = r.xMin + offset.x - pitch; x <= r.xMax; x += pitch) {
            float px = SnapX(x, origin.x, sx);
            if(px + lineX < r.xMin) continue;
            AddQuad(vh, px, r.yMin, px + lineX, r.yMax, tint);
        }
        for(float y = r.yMax - offset.y + pitch; y >= r.yMin; y -= pitch) {
            float py = SnapX(y, origin.y, sy);
            if(py - lineY > r.yMax) continue;
            AddQuad(vh, r.xMin, py - lineY, r.xMax, py, tint);
        }
    }
    private static float SnapX(float local, float origin, float scale) =>
        (Mathf.Round(origin + local * scale) - origin) / scale;
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
