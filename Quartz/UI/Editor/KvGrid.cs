using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
/// <summary>
/// DM Note's canvas grid: 1px lines every <see cref="KvSnap.GridSnap"/> layout units, drifting with
/// the pan and scaling with the zoom, in the near-black it draws them in.
///
/// Lines, not dots — DM Note's GridBackground paints an SVG pattern of one vertical and one
/// horizontal stroke per cell, and the CSS dot grid it replaced is commented out in its source.
///
/// One mesh, rebuilt only when the pitch or the offset actually moves: <see cref="Sync"/> is called
/// from the canvas's existing overlay sync, which every pan, zoom, reset and fit already routes
/// through, and it returns without touching the mesh when neither changed. Nothing here polls.
/// </summary>
internal sealed class KvGrid : MaskableGraphic {
    /// <summary>
    /// The tightest the grid is allowed to get on screen before it starts doubling its pitch.
    ///
    /// This is the one deliberate departure from DM Note, which always draws every 5-unit line: at
    /// the bottom of the zoom range that is a 1px stroke every 1.5px, which reads as flat noise
    /// rather than as a grid and costs a few thousand vertices to say nothing. Doubling keeps the
    /// lines on real cell boundaries, so the grid still lines up with what the drag snaps to.
    /// </summary>
    private const float MinScreenPitch = 5f;
    private const float LineWidth = 1f;
    private float pitch = -1f;
    private Vector2 offset = new(float.NaN, float.NaN);
    /// <summary>
    /// Re-read the view. <paramref name="pan"/> is the content's anchored position — the same
    /// value DM Note takes its pattern offset from, modulo the scaled cell.
    /// </summary>
    internal void Sync(float zoom, Vector2 pan) {
        float scaled = KvSnap.GridSnap * Mathf.Max(zoom, 0.0001f);
        while(scaled < MinScreenPitch) scaled *= 2f;
        // -pan.y, not pan.y: the content is anchored top-left in a y-up rect, so panning the layout
        // down drives anchoredPosition.y negative. Negating it recovers DM Note's y-down panY,
        // which is what the pattern offset is measured from. Getting this backwards is invisible
        // while panning horizontally and makes the grid crawl the wrong way vertically.
        Vector2 next = new(Mod(pan.x, scaled), Mod(-pan.y, scaled));
        if(Mathf.Abs(scaled - pitch) < 0.001f
            && Mathf.Abs(next.x - offset.x) < 0.001f
            && Mathf.Abs(next.y - offset.y) < 0.001f) return;
        pitch = scaled;
        offset = next;
        SetVerticesDirty();
    }
    /// <summary>Euclidean, unlike <c>%</c>: a negative pan must wrap into the cell, not mirror it.</summary>
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
        // The pan offsets the pattern; stepping back one cell keeps the line that has scrolled
        // partly off the left/top edge drawn rather than popping in at the boundary.
        for(float x = r.xMin + offset.x - pitch; x <= r.xMax; x += pitch) {
            if(x + LineWidth < r.xMin) continue;
            AddQuad(vh, x, r.yMin, x + LineWidth, r.yMax, tint);
        }
        // y is measured down from the top edge: layout space is top-left origin, so the pattern has
        // to travel the same way the content does.
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
