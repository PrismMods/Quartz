namespace Quartz.Features.KeyViewer.Layout;
internal readonly struct KvRect(float x, float y, float w, float h) {
    internal readonly float X = x, Y = y, W = w, H = h;
    internal float Left => X;
    internal float Right => X + W;
    internal float Top => Y;
    internal float Bottom => Y + H;
    internal float CenterX => X + W * 0.5f;
    internal float CenterY => Y + H * 0.5f;
    internal bool Intersects(KvRect o) =>
        Left < o.Right && Right > o.Left && Top < o.Bottom && Bottom > o.Top;
}
internal readonly struct KvGuide(bool vertical, float position, float from, float to) {
    internal readonly bool Vertical = vertical;
    internal readonly float Position = position;
    internal readonly float From = from, To = to;
}
internal readonly struct KvSnapResult(float x, float y, IReadOnlyList<KvGuide> guides) {
    internal readonly float X = x, Y = y;
    internal readonly IReadOnlyList<KvGuide> Guides = guides;
}
internal static class KvSnap {
    internal const float GridSnap = 5f;
    internal const float DragThreshold = 5f;
    internal const float AlignThreshold = 8f;
    internal const float SizeMatchThreshold = 4f;
    internal const float PasteOffset = 20f;
    internal const float MinZoom = 0.3f;
    internal const float MaxZoom = 4f;
    internal const float ZoomStep = 0.1f;
    private const float WheelAxisEpsilon = 0.01f;
    internal const float WheelDeadzone = 0.0001f;
    internal const float CanvasCenterX = 450f;
    internal const float CanvasCenterY = 195f;
    private const float GuideExtension = 500f;
    internal static float GridStepFor(float zoom, float gridSnap = GridSnap) {
        if(zoom <= 0f) return gridSnap;
        return Math.Max((float)Math.Round(gridSnap / zoom), 1f);
    }
    internal static bool ShiftLocksToX(float rawDeltaX, float rawDeltaY) => rawDeltaX >= rawDeltaY;
    internal static KvSnapResult SnapMove(
        KvRect moving, IReadOnlyList<KvRect> others, float zoom,
        bool alignment = true, float gridSnap = GridSnap
    ) {
        float step = GridStepFor(zoom, gridSnap);
        List<KvGuide> guides = [];
        float x = moving.X, y = moving.Y;
        bool snappedX = false, snappedY = false;
        if(alignment) {
            float bestDx = float.MaxValue, bestDy = float.MaxValue;
            float atX = 0f, atY = 0f;
            void TryX(float candidate) {
                Consider(candidate - moving.Left, candidate, ref bestDx, ref atX);
                Consider(candidate - moving.CenterX, candidate, ref bestDx, ref atX);
                Consider(candidate - moving.Right, candidate, ref bestDx, ref atX);
            }
            void TryY(float candidate) {
                Consider(candidate - moving.Top, candidate, ref bestDy, ref atY);
                Consider(candidate - moving.CenterY, candidate, ref bestDy, ref atY);
                Consider(candidate - moving.Bottom, candidate, ref bestDy, ref atY);
            }
            TryX(CanvasCenterX);
            TryY(CanvasCenterY);
            if(others != null) {
                foreach(KvRect o in others) {
                    TryX(o.Left);
                    TryX(o.CenterX);
                    TryX(o.Right);
                    TryY(o.Top);
                    TryY(o.CenterY);
                    TryY(o.Bottom);
                }
            }
            if(Math.Abs(bestDx) <= AlignThreshold) {
                x = (float)Math.Round(moving.X + bestDx);
                snappedX = true;
                guides.Add(new KvGuide(true, atX, moving.Top - GuideExtension, moving.Bottom + GuideExtension));
            }
            if(Math.Abs(bestDy) <= AlignThreshold) {
                y = (float)Math.Round(moving.Y + bestDy);
                snappedY = true;
                guides.Add(new KvGuide(false, atY, moving.Left - GuideExtension, moving.Right + GuideExtension));
            }
        }
        if(!snappedX) x = (float)Math.Round(moving.X / step) * step;
        if(!snappedY) y = (float)Math.Round(moving.Y / step) * step;
        return new KvSnapResult(Clamp(x), Clamp(y), guides);
    }
    internal static float SnapSize(float value, IReadOnlyList<float> siblingSizes, float gridSnap = GridSnap) {
        float snapped = (float)Math.Round(value / gridSnap) * gridSnap;
        if(siblingSizes != null) {
            float best = float.MaxValue;
            float at = snapped;
            foreach(float s in siblingSizes) {
                float d = s - value;
                if(Math.Abs(d) < Math.Abs(best)) {
                    best = d;
                    at = s;
                }
            }
            if(Math.Abs(best) <= SizeMatchThreshold) snapped = at;
        }
        return Math.Max(KvElement.MinSize, snapped);
    }
    internal static float ClampZoom(float zoom) => Math.Clamp(zoom, MinZoom, MaxZoom);
    internal static bool WheelMoved(float deltaX, float deltaY) =>
        Math.Abs(deltaX) > WheelDeadzone || Math.Abs(deltaY) > WheelDeadzone;
    internal const float WheelPanSpeed = 20f;
    internal static (float X, float Y) WheelPan(float deltaX, float deltaY, bool shift, float speed) {
        if(shift && Math.Abs(deltaX) <= WheelAxisEpsilon) return (deltaY * speed, 0f);
        return (deltaX * speed, -deltaY * speed);
    }
    internal static float ZoomStepFor(float deltaY) {
        float magnitude = Math.Min(Math.Abs(deltaY), 1f);
        return Math.Sign(deltaY) * ZoomStep * magnitude;
    }
    private static void Consider(float delta, float candidate, ref float best, ref float at) {
        if(Math.Abs(delta) >= Math.Abs(best)) return;
        best = delta;
        at = candidate;
    }
    private static float Clamp(float v) => Math.Clamp(v, KvElement.MinPosition, KvElement.MaxPosition);
}
