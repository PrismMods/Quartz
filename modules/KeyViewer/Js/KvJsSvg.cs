using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.KeyViewer.Js;
internal sealed class KvJsSvgGraphic : MaskableGraphic {
    internal sealed class Shape {
        public int Kind;
        public Vector2[] Points;
        public Color FillA = Color.clear, FillB = Color.clear;
        public bool FillGradient;
        public Color StrokeA = Color.clear, StrokeB = Color.clear;
        public bool StrokeGradient;
        public float StrokeWidth = 1f;
        public float DashOn, DashOff;
        public float Alpha = 1f;
    }
    public const int KindPolygon = 0, KindPolyline = 1, KindLine = 2, KindRect = 3;
    private readonly List<Shape> shapes = [];
    private Vector4 viewBox = new(0f, 0f, 100f, 100f);
    public void SetShapes(List<Shape> next, Vector4 box) {
        shapes.Clear();
        shapes.AddRange(next);
        viewBox = box;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if(rect.width <= 0f || rect.height <= 0f || viewBox.z <= 0f || viewBox.w <= 0f) return;
        float sx = rect.width / viewBox.z;
        float sy = rect.height / viewBox.w;
        foreach(Shape shape in shapes) {
            try {
                Emit(vh, shape, rect, sx, sy);
            } catch(Exception e) { Quartz.Core.Diag.Ignore(e); }
        }
    }
    private Vector2 Map(Vector2 p, Rect rect, float sx, float sy) =>
        new(rect.xMin + (p.x - viewBox.x) * sx, rect.yMax - (p.y - viewBox.y) * sy);
    private void Emit(VertexHelper vh, Shape shape, Rect rect, float sx, float sy) {
        if(shape.Points == null || shape.Points.Length == 0) return;
        switch(shape.Kind) {
            case KindPolygon:
                EmitPolygon(vh, shape, rect, sx, sy);
                if(shape.StrokeA.a > 0f || shape.StrokeGradient) EmitStroke(vh, shape, rect, sx, sy, closed: true);
                break;
            case KindPolyline:
                EmitStroke(vh, shape, rect, sx, sy, closed: false);
                break;
            case KindLine:
                EmitStroke(vh, shape, rect, sx, sy, closed: false);
                break;
            case KindRect: {
                if(shape.Points.Length < 2) return;
                Vector2 a = Map(shape.Points[0], rect, sx, sy);
                Vector2 b = Map(shape.Points[1], rect, sx, sy);
                AddQuad(vh,
                    new Vector2(a.x, b.y), new Vector2(b.x, b.y), new Vector2(b.x, a.y), new Vector2(a.x, a.y),
                    SampleFill(shape, shape.Points[0].x), SampleFill(shape, shape.Points[1].x));
                break;
            }
            default:
                break;
        }
    }
    private float GradientT(Shape shape, float x) =>
        viewBox.z <= 0f ? 0f : Mathf.Clamp01((x - viewBox.x) / viewBox.z);
    private Color SampleFill(Shape shape, float x) {
        Color c = shape.FillGradient ? Color.Lerp(shape.FillA, shape.FillB, GradientT(shape, x)) : shape.FillA;
        c.a *= shape.Alpha;
        return c;
    }
    private Color SampleStroke(Shape shape, float x) {
        Color c = shape.StrokeGradient ? Color.Lerp(shape.StrokeA, shape.StrokeB, GradientT(shape, x)) : shape.StrokeA;
        c.a *= shape.Alpha;
        return c;
    }
    private void EmitPolygon(VertexHelper vh, Shape shape, Rect rect, float sx, float sy) {
        Vector2[] pts = shape.Points;
        if(pts.Length < 3 || (shape.FillA.a <= 0f && !shape.FillGradient)) return;
        bool areaChart = pts.Length >= 3 && Mathf.Approximately(pts[0].y, MaxY(pts)) && Mathf.Approximately(pts[pts.Length - 1].y, MaxY(pts));
        if(areaChart) {
            float baseY = pts[0].y;
            for(int i = 1; i < pts.Length - 2; i++) {
                Vector2 p0 = pts[i];
                Vector2 p1 = pts[i + 1];
                AddQuad(vh,
                    Map(new Vector2(p0.x, baseY), rect, sx, sy),
                    Map(p0, rect, sx, sy),
                    Map(p1, rect, sx, sy),
                    Map(new Vector2(p1.x, baseY), rect, sx, sy),
                    SampleFill(shape, p0.x), SampleFill(shape, p1.x));
            }
            return;
        }
        Vector2 origin = Map(pts[0], rect, sx, sy);
        Color originColor = SampleFill(shape, pts[0].x);
        for(int i = 1; i < pts.Length - 1; i++) {
            int start = vh.currentVertCount;
            AddVert(vh, origin, originColor);
            AddVert(vh, Map(pts[i], rect, sx, sy), SampleFill(shape, pts[i].x));
            AddVert(vh, Map(pts[i + 1], rect, sx, sy), SampleFill(shape, pts[i + 1].x));
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
    private static float MaxY(Vector2[] pts) {
        float max = float.MinValue;
        foreach(Vector2 p in pts) max = Mathf.Max(max, p.y);
        return max;
    }
    private void EmitStroke(VertexHelper vh, Shape shape, Rect rect, float sx, float sy, bool closed) {
        Vector2[] pts = shape.Points;
        if(pts.Length < 2 || (shape.StrokeA.a <= 0f && !shape.StrokeGradient)) return;
        float half = Mathf.Max(0.5f, shape.StrokeWidth) * 0.5f;
        int count = closed ? pts.Length : pts.Length - 1;
        for(int i = 0; i < count; i++) {
            Vector2 va = pts[i];
            Vector2 vb = pts[(i + 1) % pts.Length];
            if(shape.DashOn > 0f) {
                EmitDashed(vh, shape, va, vb, rect, sx, sy, half);
                continue;
            }
            EmitSegment(vh, shape, va, vb, rect, sx, sy, half);
        }
    }
    private void EmitDashed(VertexHelper vh, Shape shape, Vector2 va, Vector2 vb, Rect rect, float sx, float sy, float half) {
        Vector2 a = Map(va, rect, sx, sy);
        Vector2 b = Map(vb, rect, sx, sy);
        float total = (b - a).magnitude;
        if(total <= 0.001f) return;
        float on = Mathf.Max(0.5f, shape.DashOn * 2f);
        float off = Mathf.Max(0.5f, (shape.DashOff > 0f ? shape.DashOff : shape.DashOn) * 2f);
        float pos = 0f;
        Color ca = SampleStroke(shape, va.x);
        Color cb = SampleStroke(shape, vb.x);
        while(pos < total) {
            float end = Mathf.Min(pos + on, total);
            Vector2 pa = a + (b - a) * (pos / total);
            Vector2 pb = a + (b - a) * (end / total);
            AddSegmentQuad(vh, pa, pb, half, Color.Lerp(ca, cb, pos / total), Color.Lerp(ca, cb, end / total));
            pos = end + off;
        }
    }
    private void EmitSegment(VertexHelper vh, Shape shape, Vector2 va, Vector2 vb, Rect rect, float sx, float sy, float half) {
        AddSegmentQuad(vh,
            Map(va, rect, sx, sy), Map(vb, rect, sx, sy), half,
            SampleStroke(shape, va.x), SampleStroke(shape, vb.x));
    }
    private static void AddSegmentQuad(VertexHelper vh, Vector2 a, Vector2 b, float half, Color ca, Color cb) {
        Vector2 dir = b - a;
        if(dir.sqrMagnitude < 0.000001f) return;
        Vector2 n = new Vector2(-dir.y, dir.x) / Mathf.Max(0.000001f, dir.magnitude) * half;
        int start = vh.currentVertCount;
        AddVert(vh, a - n, ca);
        AddVert(vh, a + n, ca);
        AddVert(vh, b + n, cb);
        AddVert(vh, b - n, cb);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
    private static void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br, Color left, Color right) {
        int start = vh.currentVertCount;
        AddVert(vh, bl, left);
        AddVert(vh, tl, left);
        AddVert(vh, tr, right);
        AddVert(vh, br, right);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
    private static void AddVert(VertexHelper vh, Vector2 pos, Color color) {
        UIVertex v = UIVertex.simpleVert;
        v.position = pos;
        v.color = color;
        vh.AddVert(v);
    }
}
internal static class KvJsSvg {
    public static void Build(KvJsSvgGraphic graphic, KvJsVNode svg, float alpha) {
        Vector4 box = ParseViewBox(svg.Attr("viewBox"));
        Dictionary<string, (Color a, Color b)> gradients = new(StringComparer.Ordinal);
        CollectGradients(svg, gradients);
        List<KvJsSvgGraphic.Shape> shapes = [];
        CollectShapes(svg, gradients, shapes, alpha);
        graphic.SetShapes(shapes, box);
    }
    private static Vector4 ParseViewBox(string raw) {
        if(string.IsNullOrEmpty(raw)) return new Vector4(0f, 0f, 100f, 100f);
        string[] parts = raw.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length != 4) return new Vector4(0f, 0f, 100f, 100f);
        return new Vector4(F(parts[0]), F(parts[1]), Mathf.Max(1f, F(parts[2])), Mathf.Max(1f, F(parts[3])));
    }
    private static void CollectGradients(KvJsVNode node, Dictionary<string, (Color a, Color b)> gradients) {
        if(node.Children == null) return;
        foreach(KvJsVNode child in node.Children) {
            if(child.IsText) continue;
            if(child.Tag.Equals("linearGradient", StringComparison.OrdinalIgnoreCase)) {
                string id = child.Attr("id");
                if(string.IsNullOrEmpty(id) || child.Children == null) continue;
                List<Color> stops = [];
                foreach(KvJsVNode stop in child.Children) {
                    if(stop.IsText || !stop.Tag.Equals("stop", StringComparison.OrdinalIgnoreCase)) continue;
                    stops.Add(StopColor(stop));
                }
                if(stops.Count == 0) continue;
                gradients[id] = (stops[0], stops[stops.Count - 1]);
                continue;
            }
            CollectGradients(child, gradients);
        }
    }
    private static Color StopColor(KvJsVNode stop) {
        Color color = Color.white;
        float opacity = 1f;
        string styleRaw = stop.Attr("style");
        if(!string.IsNullOrEmpty(styleRaw)) {
            foreach(string decl in styleRaw.Split(';')) {
                int colon = decl.IndexOf(':');
                if(colon <= 0) continue;
                string prop = decl.Substring(0, colon).Trim().ToLowerInvariant();
                string value = decl.Substring(colon + 1).Trim();
                if(prop == "stop-color" && KeyViewerStylesheet.TryParseColor(value, out CssColor c)) color = new Color(c.R, c.G, c.B, c.A);
                else if(prop == "stop-opacity") opacity = F(value, 1f);
            }
        }
        string colorAttr = stop.Attr("stop-color");
        if(!string.IsNullOrEmpty(colorAttr) && KeyViewerStylesheet.TryParseColor(colorAttr, out CssColor ac)) color = new Color(ac.R, ac.G, ac.B, ac.A);
        string opacityAttr = stop.Attr("stop-opacity");
        if(!string.IsNullOrEmpty(opacityAttr)) opacity = F(opacityAttr, 1f);
        color.a *= opacity;
        return color;
    }
    private static void CollectShapes(KvJsVNode node, Dictionary<string, (Color a, Color b)> gradients, List<KvJsSvgGraphic.Shape> shapes, float alpha) {
        if(node.Children == null) return;
        foreach(KvJsVNode child in node.Children) {
            if(child.IsText) continue;
            switch(child.Tag.ToLowerInvariant()) {
                case "defs":
                    break;
                case "polygon":
                case "polyline": {
                    Vector2[] pts = ParsePoints(child.Attr("points"));
                    if(pts.Length < 2) break;
                    KvJsSvgGraphic.Shape shape = new() {
                        Kind = child.Tag.Equals("polygon", StringComparison.OrdinalIgnoreCase) ? KvJsSvgGraphic.KindPolygon : KvJsSvgGraphic.KindPolyline,
                        Points = pts,
                        StrokeWidth = F(child.Attr("stroke-width"), 1f),
                        Alpha = F(child.Attr("opacity"), 1f) * alpha,
                    };
                    ApplyPaint(child.Attr("fill"), gradients, c => { shape.FillA = c.a; shape.FillB = c.b; shape.FillGradient = c.grad; });
                    ApplyPaint(child.Attr("stroke"), gradients, c => { shape.StrokeA = c.a; shape.StrokeB = c.b; shape.StrokeGradient = c.grad; });
                    ParseDash(child.Attr("stroke-dasharray"), shape);
                    shapes.Add(shape);
                    break;
                }
                case "line": {
                    KvJsSvgGraphic.Shape shape = new() {
                        Kind = KvJsSvgGraphic.KindLine,
                        Points = [
                            new Vector2(F(child.Attr("x1")), F(child.Attr("y1"))),
                            new Vector2(F(child.Attr("x2")), F(child.Attr("y2"))),
                        ],
                        StrokeWidth = F(child.Attr("stroke-width"), 1f),
                        Alpha = F(child.Attr("opacity"), 1f) * alpha,
                    };
                    ApplyPaint(child.Attr("stroke"), gradients, c => { shape.StrokeA = c.a; shape.StrokeB = c.b; shape.StrokeGradient = c.grad; });
                    ParseDash(child.Attr("stroke-dasharray"), shape);
                    shapes.Add(shape);
                    break;
                }
                case "rect": {
                    float x = F(child.Attr("x")), y = F(child.Attr("y"));
                    KvJsSvgGraphic.Shape shape = new() {
                        Kind = KvJsSvgGraphic.KindRect,
                        Points = [
                            new Vector2(x, y),
                            new Vector2(x + F(child.Attr("width")), y + F(child.Attr("height"))),
                        ],
                        Alpha = F(child.Attr("opacity"), 1f) * alpha,
                    };
                    ApplyPaint(child.Attr("fill"), gradients, c => { shape.FillA = c.a; shape.FillB = c.b; shape.FillGradient = c.grad; });
                    shapes.Add(shape);
                    break;
                }
                default:
                    CollectShapes(child, gradients, shapes, alpha);
                    break;
            }
        }
    }
    private static void ParseDash(string raw, KvJsSvgGraphic.Shape shape) {
        if(string.IsNullOrEmpty(raw)) return;
        string[] parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length == 0) return;
        shape.DashOn = F(parts[0], 0f);
        shape.DashOff = parts.Length > 1 ? F(parts[1], shape.DashOn) : shape.DashOn;
    }
    private static void ApplyPaint(string raw, Dictionary<string, (Color a, Color b)> gradients, Action<(Color a, Color b, bool grad)> set) {
        if(string.IsNullOrEmpty(raw) || raw.Equals("none", StringComparison.OrdinalIgnoreCase)) return;
        if(raw.StartsWith("url(#", StringComparison.OrdinalIgnoreCase)) {
            string id = raw.Substring(5).TrimEnd(')');
            if(gradients.TryGetValue(id, out (Color a, Color b) grad)) set((grad.a, grad.b, true));
            return;
        }
        if(KeyViewerStylesheet.TryParseColor(raw, out CssColor c)) {
            Color color = new(c.R, c.G, c.B, c.A);
            set((color, color, false));
        }
    }
    private static Vector2[] ParsePoints(string raw) {
        if(string.IsNullOrEmpty(raw)) return [];
        string[] parts = raw.Split(new[] { ' ', ',', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int count = parts.Length / 2;
        Vector2[] pts = new Vector2[count];
        for(int i = 0; i < count; i++) pts[i] = new Vector2(F(parts[i * 2]), F(parts[i * 2 + 1]));
        return pts;
    }
    private static float F(string raw, float fallback = 0f) =>
        !string.IsNullOrEmpty(raw) && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
}
