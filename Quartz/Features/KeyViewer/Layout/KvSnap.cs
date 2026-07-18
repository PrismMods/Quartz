namespace Quartz.Features.KeyViewer.Layout;
/// <summary>Axis-aligned rect in layout coordinates (DM Note dx/dy/width/height space).</summary>
internal readonly struct KvRect(float x, float y, float w, float h) {
    internal readonly float X = x, Y = y, W = w, H = h;
    internal float Left => X;
    internal float Right => X + W;
    internal float Top => Y;
    internal float Bottom => Y + H;
    internal float CenterX => X + W * 0.5f;
    internal float CenterY => Y + H * 0.5f;
    /// <summary>Overlap test. DM Note's marquee selects on intersection, not containment.</summary>
    internal bool Intersects(KvRect o) =>
        Left < o.Right && Right > o.Left && Top < o.Bottom && Bottom > o.Top;
}
/// <summary>A guide line to draw while dragging. <see cref="Vertical"/> lines have a fixed X.</summary>
internal readonly struct KvGuide(bool vertical, float position, float from, float to) {
    internal readonly bool Vertical = vertical;
    internal readonly float Position = position;
    internal readonly float From = from, To = to;
}
internal readonly struct KvSnapResult(float x, float y, IReadOnlyList<KvGuide> guides) {
    internal readonly float X = x, Y = y;
    internal readonly IReadOnlyList<KvGuide> Guides = guides;
}
/// <summary>
/// Grid + alignment snapping for the layout editor, in layout space rather than screen space.
///
/// Constants are DM Note's, so the editor feels the same as the app users are coming from —
/// see its hooks/Grid/constants.ts and utils/grid/smartGuides.ts. Kept engine-free so the
/// interaction rules are testable without a Unity host.
/// </summary>
internal static class KvSnap {
    internal const float GridSnap = 5f;
    internal const float DragThreshold = 5f;
    internal const float AlignThreshold = 8f;
    internal const float SizeMatchThreshold = 4f;
    internal const float PasteOffset = 20f;
    internal const float MinZoom = 0.3f;
    internal const float MaxZoom = 4f;
    internal const float ZoomStep = 0.1f;
    /// <summary>Below this a scroll axis is treated as absent — DM Note's threshold for deciding
    /// whether a real horizontal delta arrived.</summary>
    private const float WheelAxisEpsilon = 0.01f;
    /// <summary>A scroll smaller than this is noise rather than a gesture. Far below
    /// <see cref="WheelAxisEpsilon"/> on purpose: a slow trackpad scroll reports in hundredths of a
    /// tick and still has to pan.</summary>
    internal const float WheelDeadzone = 0.0001f;
    /// <summary>Canvas centre DM Note draws its centre guides against.</summary>
    internal const float CanvasCenterX = 450f;
    internal const float CanvasCenterY = 195f;
    private const float GuideExtension = 500f;
    /// <summary>
    /// Grid step in layout units for a given zoom. DM Note shrinks the step as you zoom in
    /// (`max(round(gridSnapSize / zoom), 1)`) so the snap feels constant on screen rather than
    /// in the document: 1 unit at 4x, 17 at 0.3x.
    /// </summary>
    internal static float GridStepFor(float zoom, float gridSnap = GridSnap) {
        if(zoom <= 0f) return gridSnap;
        return Math.Max((float)Math.Round(gridSnap / zoom), 1f);
    }
    /// <summary>
    /// Axis the drag locks to when shift is held, latched once at drag start.
    /// Returns true for the X axis.
    ///
    /// NB this reproduces a DM Note quirk deliberately: it compares the RAW SIGNED deltas
    /// (`deltaX >= deltaY`), not their magnitudes, so dragging left-and-down locks to Y where
    /// an abs() comparison would pick X. Matching it keeps muscle memory intact; "fixing" it
    /// would make shift-drag behave differently between the two apps.
    /// </summary>
    internal static bool ShiftLocksToX(float rawDeltaX, float rawDeltaY) => rawDeltaX >= rawDeltaY;
    /// <summary>
    /// Snap a dragged rect against <paramref name="others"/> and the canvas centre.
    ///
    /// Alignment wins over the grid per-axis: an axis that found an alignment guide rounds to a
    /// whole unit instead of the grid step, so an edge lands exactly flush rather than being
    /// pulled back off by grid rounding.
    /// </summary>
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
    /// <summary>
    /// Snap a resized dimension to the grid, then to a sibling's width/height when one is
    /// within <see cref="SizeMatchThreshold"/>. Never returns below <see cref="KvElement.MinSize"/>.
    /// </summary>
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
    /// <summary>True when a scroll carries enough of either axis to act on. X is included because a
    /// sideways trackpad swipe reports no Y at all.</summary>
    internal static bool WheelMoved(float deltaX, float deltaY) =>
        Math.Abs(deltaX) > WheelDeadzone || Math.Abs(deltaY) > WheelDeadzone;
    /// <summary>
    /// Pixels of pan per unit of scroll. Deliberately NOT <c>CoreSettings.ScrollSpeed</c> (80),
    /// which is tuned for stepping a document one wheel-click at a time. A trackpad streams many
    /// events per swipe and the offset lands 1:1 on screen, so reusing that made a short two-finger
    /// swipe throw the canvas across the viewport.
    /// </summary>
    internal const float WheelPanSpeed = 20f;
    /// <summary>
    /// Resolve a scroll into a view offset, in Unity's anchoredPosition convention (y up).
    ///
    /// The axes do NOT share a sign, which is not an oversight. Unity reports a scroll's Y in the
    /// document convention (positive = toward the start), so revealing what is below means moving
    /// the content up: Y negates. X arrives already in the direction the fingers went, so it does
    /// not. The two were flipped together once and read as vertically inverted.
    ///
    /// Honouring X is the whole of trackpad support. A trackpad has no middle button and cannot
    /// practically be right-dragged, so a two-finger scroll is its only navigation gesture — and it
    /// carries both axes.
    ///
    /// Shift maps a mouse's single axis onto X, matching DM Note. It defers to a true X delta so a
    /// trackpad already scrolling sideways is never overridden.
    /// </summary>
    internal static (float X, float Y) WheelPan(float deltaX, float deltaY, bool shift, float speed) {
        if(shift && Math.Abs(deltaX) <= WheelAxisEpsilon) return (deltaY * speed, 0f);
        return (deltaX * speed, -deltaY * speed);
    }
    /// <summary>
    /// Zoom delta for one scroll event, proportional to how far the wheel actually moved.
    ///
    /// A fixed step per event is what makes a trackpad unusable: it streams dozens of small
    /// events per gesture and each one would apply a full <see cref="ZoomStep"/>, so a single
    /// two-finger swipe crosses the whole zoom range. Scaling by the delta leaves a mouse's
    /// one-tick |1| at exactly <see cref="ZoomStep"/> while a trackpad's fractional deltas
    /// produce proportionally small steps. Clamped at one tick so a fast flick cannot exceed
    /// what a wheel click does.
    /// </summary>
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
