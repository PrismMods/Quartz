using UnityEngine;
using UnityEngine.UI;

namespace Quartz.Features.KeyViewer;

// ===== rain (port of v1's KvRain* — one manager Update, batched row meshes,
// custom graphic with vertex-alpha fade; allocation only on key press) =====

// Spawn-time parameters of one drop. The drop stretches between a leading
// edge ((now - start) * speed) and a trailing edge that starts moving on
// release — same scheme as v1.
internal sealed class RawRain {
    public int Group;
    // Draw order within the group (DM Note: the key's zIndex; simple mode
    // leaves 0). Drops with equal Order keep spawn order, newest on top.
    public float Order;
    public float StartTime;
    public float EndTime = -1f;
    public float AnchorX;
    public float Width;
    public float BaseY;
    public float TrackHeight;
    public float Speed;
    public float FadePx;
    public bool Reverse;
    public Color Color;
    public Color ColorTop;
    public Color ColorBottom;
    // DM Note glow halo. GlowSize <= 0 (simple-mode rain, or glow disabled)
    // skips the halo entirely.
    public float GlowSize;
    public Color GlowTop;
    public Color GlowBottom;
}

// Renderer for ALL rain drops in one mesh. Drops are held in 3 ordering
// groups (back-to-front) and emitted group 1 → 3 so a single Graphic/batch
// reproduces the layering the former 3 sibling rows gave — one mesh rebuild
// per frame instead of three.
internal sealed class RainGraphic : MaskableGraphic {
    private List<RawRain>[] groups;
    private float now;

    public void SetSource(List<RawRain>[] source) {
        groups = source;
        SetVerticesDirty();
    }

    public void SetFrame(float frameTime) {
        now = frameTime;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        if(groups == null) return;

        Rect layer = rectTransform.rect;
        // Group order IS draw order: group 1 first (behind) … group 3 last
        // (front), matching the old sibling-row ordering.
        for(int g = 0; g < groups.Length; g++) {
            List<RawRain> active = groups[g];
            for(int i = 0; i < active.Count; i++) AddDrop(vh, layer, active[i]);
        }
    }

    private void AddDrop(VertexHelper vh, Rect layer, RawRain raw) {
        float lead = (now - raw.StartTime) * raw.Speed;
        float trail = raw.EndTime < 0f ? 0f : Mathf.Max(0f, (now - raw.EndTime) * raw.Speed);
        float dNear = trail;
        float dFar = Mathf.Min(lead, raw.TrackHeight);
        float height = dFar - dNear;
        if(height <= 0.5f || raw.Width <= 0.5f) return;

        float dropY = raw.Reverse ? raw.BaseY + raw.TrackHeight - dFar : raw.BaseY + dNear;
        float xMin = layer.xMin + raw.AnchorX - (raw.Width * 0.5f);
        float xMax = xMin + raw.Width;
        float yMin = layer.yMax + dropY;
        float yMax = yMin + height;

        // Plain vertex-coloured quad — no rounded corners. Matches the
        // original KRP rain: 4 verts / 2 tris per drop, the cheapest a drop
        // can be. The top/bottom colour gradient and the fade are both linear
        // in distance and distance is linear in Y, so a single quad
        // reproduces them exactly — except across the fade boundary, where
        // the alpha kinks. Split into two quads there (as the original did),
        // one quad otherwise.
        Color cMin = ColorForY(raw, dNear, dFar, yMin, yMin, height);
        Color cMax = ColorForY(raw, dNear, dFar, yMax, yMin, height);

        if(raw.FadePx > 0.5f && raw.TrackHeight > 0.5f) {
            float fadeStartD = raw.TrackHeight - raw.FadePx;
            float span = dFar - dNear;
            if(span > 0.0001f) {
                float tB = raw.Reverse
                    ? (fadeStartD - dFar) / (dNear - dFar)
                    : (fadeStartD - dNear) / span;
                if(tB > 0.0001f && tB < 0.9999f) {
                    float yMid = yMin + (tB * height);
                    Color cMid = ColorForY(raw, dNear, dFar, yMid, yMin, height);
                    AddQuad(vh, xMin, yMin, xMax, yMid, cMin, cMid);
                    AddQuad(vh, xMin, yMid, xMax, yMax, cMid, cMax);
                    AddGlow(vh, raw, xMin, yMin, xMax, yMax, cMin, cMax);
                    return;
                }
            }
        }

        AddQuad(vh, xMin, yMin, xMax, yMax, cMin, cMax);
        AddGlow(vh, raw, xMin, yMin, xMax, yMax, cMin, cMax);
    }

    private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color bottom, Color top) {
        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.position = new Vector3(xMin, yMin, 0f); v.color = bottom; vh.AddVert(v);
        v.position = new Vector3(xMax, yMin, 0f); v.color = bottom; vh.AddVert(v);
        v.position = new Vector3(xMax, yMax, 0f); v.color = top; vh.AddVert(v);
        v.position = new Vector3(xMin, yMax, 0f); v.color = top; vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }

    // Four independently-coloured corners: (xMin,yMin), (xMax,yMin),
    // (xMax,yMax), (xMin,yMax) — used by the glow halo, where a strip's
    // outer edge fades to transparent while its inner edge doesn't.
    private static void AddQuad4(VertexHelper vh, float xMin, float yMin, float xMax, float yMax,
        Color bl, Color br, Color tr, Color tl) {
        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.position = new Vector3(xMin, yMin, 0f); v.color = bl; vh.AddVert(v);
        v.position = new Vector3(xMax, yMin, 0f); v.color = br; vh.AddVert(v);
        v.position = new Vector3(xMax, yMax, 0f); v.color = tr; vh.AddVert(v);
        v.position = new Vector3(xMin, yMax, 0f); v.color = tl; vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }

    // DM Note's noteGlow halo: a soft border around the drop's current
    // rect, GlowSize px wide, faded to transparent outward. DmNote does
    // this in a fragment shader via a rounded-rect SDF; a plain UI mesh
    // has no per-pixel shader, so it's approximated with 4 edge strips
    // (linear fade, matching the linear track fade above) plus 4 corner
    // patches using a bilinear diagonal fade — the same separable
    // falloff GlowSprite() bakes into a texture for the CSS box-shadow,
    // just done in vertex colour here instead. cMin/cMax are the body's
    // own (already fade-adjusted) edge colours, so glow alpha correctly
    // vanishes wherever the body itself has already faded out.
    private static void AddGlow(VertexHelper vh, RawRain raw, float xMin, float yMin, float xMax, float yMax, Color cMin, Color cMax) {
        float g = raw.GlowSize;
        if(g <= 0.5f) return;

        Color glowBottom = new(raw.GlowBottom.r, raw.GlowBottom.g, raw.GlowBottom.b, raw.GlowBottom.a * cMin.a);
        Color glowTop = new(raw.GlowTop.r, raw.GlowTop.g, raw.GlowTop.b, raw.GlowTop.a * cMax.a);
        Color zeroBottom = new(glowBottom.r, glowBottom.g, glowBottom.b, 0f);
        Color zeroTop = new(glowTop.r, glowTop.g, glowTop.b, 0f);

        // Left / right edges: the body's own top-bottom gradient, faded
        // to 0 outward.
        AddQuad4(vh, xMin - g, yMin, xMin, yMax, zeroBottom, glowBottom, glowTop, zeroTop);
        AddQuad4(vh, xMax, yMin, xMax + g, yMax, glowBottom, zeroBottom, zeroTop, glowTop);

        // Top / bottom edges: flat colour at that Y, faded to 0 outward.
        AddQuad4(vh, xMin, yMax, xMax, yMax + g, glowTop, glowTop, zeroTop, zeroTop);
        AddQuad4(vh, xMin, yMin - g, xMax, yMin, zeroBottom, zeroBottom, glowBottom, glowBottom);

        // Corners: one full (inner) vertex, three zero — a bilinear
        // diagonal fade.
        AddQuad4(vh, xMin - g, yMin - g, xMin, yMin, zeroBottom, zeroBottom, glowBottom, zeroBottom);
        AddQuad4(vh, xMax, yMin - g, xMax + g, yMin, zeroBottom, zeroBottom, zeroBottom, glowBottom);
        AddQuad4(vh, xMin - g, yMax, xMin, yMax + g, zeroTop, glowTop, zeroTop, zeroTop);
        AddQuad4(vh, xMax, yMax, xMax + g, yMax + g, glowTop, zeroTop, zeroTop, zeroTop);
    }

    private static Color ColorForY(RawRain raw, float dNear, float dFar, float y, float yMin, float height) {
        float t = height <= 0.0001f ? 0f : (y - yMin) / height;
        float d = raw.Reverse
            ? Mathf.Lerp(dFar, dNear, t)
            : Mathf.Lerp(dNear, dFar, t);

        float alpha = (raw.FadePx > 0.5f && raw.TrackHeight > 0.5f)
            ? AlphaAtD(d, raw.TrackHeight - raw.FadePx, raw.TrackHeight, raw.FadePx)
            : 1f;

        return ColorAtD(raw, d, alpha);
    }

    private static Color ColorAtD(RawRain raw, float d, float alphaMul) {
        float t = raw.TrackHeight <= 0.0001f ? 0f : Mathf.Clamp01(d / raw.TrackHeight);
        Color c = Color.Lerp(raw.ColorBottom, raw.ColorTop, t);
        c.a *= alphaMul;
        return c;
    }

    private static float AlphaAtD(float d, float fadeStartD, float trackH, float fade) {
        if(d <= fadeStartD) return 1f;
        if(d >= trackH) return 0f;
        return (trackH - d) / fade;
    }
}

// Manages all rain drops, rendered by ONE RainGraphic. Drops live in 3
// ordering groups (back-to-front); a frame of active rain is one mesh
// rebuild + one batch instead of the former three sibling rows.
internal sealed class RainManager : MonoBehaviour {
    private RainGraphic graphic;
    private readonly List<RawRain>[] groups = [new(64), new(64), new(64)];
    private readonly Queue<RawRain> pending = new(64);

    public void SetLayer(RectTransform value) {
        pending.Clear();
        for(int i = 0; i < groups.Length; i++) groups[i].Clear();

        if(graphic != null) {
            Destroy(graphic.gameObject);
            graphic = null;
        }
        if(value == null) return;

        GameObject obj = new("RainDrops");
        obj.transform.SetParent(value, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        graphic = obj.AddComponent<RainGraphic>();
        graphic.raycastTarget = false;
        graphic.color = Color.white;
        graphic.SetSource(groups);
    }

    public void Enqueue(RawRain raw) {
        if(raw != null) pending.Enqueue(raw);
    }

    public void Clear() {
        pending.Clear();
        for(int i = 0; i < groups.Length; i++) groups[i].Clear();
        if(graphic != null) graphic.SetVerticesDirty();
    }

    private void Update() {
        if(graphic == null) {
            pending.Clear();
            return;
        }

        while(pending.Count > 0) {
            RawRain raw = pending.Dequeue();
            List<RawRain> group = groups[Mathf.Clamp(raw.Group, 1, 3) - 1];
            // Insert sorted by Order (stable: an equal Order lands after the
            // existing drops), mirroring DmNote's noteBuffer.allocate()
            // insertion sort by trackIndex — the buffer order IS the draw
            // order, and it must follow key zIndex, never press order. Simple
            // mode spawns everything with Order 0, so skip the O(n) scan —
            // append lands in the same place a scan would find anyway, and
            // this is the hot path at high KPS.
            if(raw.Order == 0) {
                group.Add(raw);
            } else {
                int at = group.Count;
                for(int i = 0; i < group.Count; i++) {
                    if(group[i].Order > raw.Order) {
                        at = i;
                        break;
                    }
                }
                group.Insert(at, raw);
            }
        }

        float now = Time.unscaledTime;
        bool dirty = false;

        for(int g = 0; g < groups.Length; g++) {
            List<RawRain> active = groups[g];
            if(active.Count > 0) dirty = true;
            int write = 0;
            for(int read = 0; read < active.Count; read++) {
                RawRain raw = active[read];
                float trail = raw.EndTime < 0f ? 0f : Mathf.Max(0f, (now - raw.EndTime) * raw.Speed);
                if(trail <= raw.TrackHeight + 8f) {
                    if(write != read) active[write] = raw;
                    write++;
                    continue;
                }

                dirty = true;
            }
            if(write < active.Count) active.RemoveRange(write, active.Count - write);
        }

        if(dirty) graphic.SetFrame(now);
    }
}
