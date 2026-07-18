using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static class KvSnapTests {
    public static void TestGridStepTracksZoom() {
        // DM Note shrinks the grid step as you zoom in so the snap feels constant on screen.
        Assert(KvSnap.GridStepFor(1f) == 5f, "grid step at 1x is the raw snap size");
        Assert(KvSnap.GridStepFor(4f) == 1f, "grid step floors at 1 unit when zoomed in");
        Assert(KvSnap.GridStepFor(0.3f) == 17f, "grid step grows when zoomed out");
        Assert(KvSnap.GridStepFor(0f) == 5f, "a zero zoom cannot divide by zero");
    }
    public static void TestZoomClamps() {
        Assert(KvSnap.ClampZoom(99f) == KvSnap.MaxZoom, "zoom clamps to max");
        Assert(KvSnap.ClampZoom(0.01f) == KvSnap.MinZoom, "zoom clamps to min");
    }
    public static void TestShiftAxisLockMatchesDmNote() {
        // Deliberately reproduces DM Note's raw-signed comparison rather than an abs() one.
        Assert(KvSnap.ShiftLocksToX(10f, 3f), "a mostly-horizontal drag locks to X");
        Assert(!KvSnap.ShiftLocksToX(3f, 10f), "a mostly-vertical drag locks to Y");
        // The quirk: dragging left (negative X) and down by less locks to Y, because -20 < 5.
        Assert(!KvSnap.ShiftLocksToX(-20f, 5f), "signed comparison is preserved, not abs()");
    }
    public static void TestMoveSnapsToGridWithoutNeighbours() {
        KvRect moving = new(103f, 47f, 60f, 60f);
        KvSnapResult r = KvSnap.SnapMove(moving, [], 1f, alignment: false);
        Assert(r.X == 105f, "x snaps to the 5-unit grid");
        Assert(r.Y == 45f, "y snaps to the 5-unit grid");
        Assert(r.Guides.Count == 0, "no guides without alignment");
    }
    public static void TestAlignmentBeatsGridAndEmitsGuides() {
        // A neighbour's left edge at 200 is 3 units away — inside the 8-unit align threshold
        // but not on the 5-unit grid. Alignment must win, landing flush at exactly 200.
        KvRect neighbour = new(200f, 0f, 60f, 60f);
        KvRect moving = new(203f, 0f, 60f, 60f);
        KvSnapResult r = KvSnap.SnapMove(moving, [neighbour], 1f);
        Assert(r.X == 200f, "left edge snaps flush to the neighbour's left edge, not to the grid");
        bool vertical = false;
        foreach(KvGuide g in r.Guides) if(g.Vertical && g.Position == 200f) vertical = true;
        Assert(vertical, "a vertical guide is emitted at the aligned edge");
    }
    public static void TestAlignmentIgnoresDistantNeighbours() {
        KvRect neighbour = new(500f, 0f, 60f, 60f);
        KvRect moving = new(203f, 0f, 60f, 60f);
        KvSnapResult r = KvSnap.SnapMove(moving, [neighbour], 1f);
        Assert(r.X == 205f, "a neighbour beyond the threshold falls back to grid snap");
    }
    public static void TestMoveClampsToLayoutBounds() {
        KvRect moving = new(99999f, -99999f, 60f, 60f);
        KvSnapResult r = KvSnap.SnapMove(moving, [], 1f, alignment: false);
        Assert(r.X == KvElement.MaxPosition, "x clamps to DM Note's max grid position");
        Assert(r.Y == KvElement.MinPosition, "y clamps to DM Note's min grid position");
    }
    public static void TestSizeSnapMatchesSiblingsAndFloors() {
        Assert(KvSnap.SnapSize(62f, []) == 60f, "size snaps to the grid");
        // 62 is 2 away from a sibling's 60 — inside the 4-unit size-match threshold.
        Assert(KvSnap.SnapSize(62f, [60f]) == 60f, "size matches a near sibling");
        // 73 is 7 from 80 and 7 from 66: outside the threshold, so grid wins.
        Assert(KvSnap.SnapSize(73f, [80f]) == 75f, "a distant sibling does not capture the size");
        Assert(KvSnap.SnapSize(1f, []) >= KvElement.MinSize, "size never drops below the minimum");
    }
    public static void TestMarqueeSelectsOnIntersection() {
        // DM Note's marquee selects anything the rect touches, not only what it fully contains.
        KvRect marquee = new(0f, 0f, 50f, 50f);
        KvRect straddling = new(40f, 40f, 60f, 60f);
        KvRect outside = new(200f, 200f, 10f, 10f);
        Assert(marquee.Intersects(straddling), "a partially covered element is selected");
        Assert(!marquee.Intersects(outside), "an untouched element is not selected");
    }
    public static void TestWheelPanHonoursBothAxes() {
        // The trackpad fix: a two-finger scroll carries X, and it has to pan rather than be dropped.
        (float x, float y) = KvSnap.WheelPan(1f, 0f, shift: false, speed: 80f);
        Assert(x == 80f, "a horizontal scroll pans horizontally");
        Assert(y == 0f, "a horizontal scroll does not pan vertically");
        (float dx, float dy) = KvSnap.WheelPan(0.5f, -0.25f, shift: false, speed: 80f);
        Assert(dx == 40f, "a diagonal scroll pans on x");
        Assert(dy == 20f, "a diagonal scroll pans on y at the same time");
    }
    public static void TestWheelPanAxesDoNotShareASign() {
        // Not symmetric, and not an oversight. Unity reports Y in the document convention
        // (positive = toward the start), so it negates; X already arrives in the direction the
        // fingers went, so it does not. Flipping them together read as vertically inverted.
        (float _, float y) = KvSnap.WheelPan(0f, 1f, shift: false, speed: 80f);
        Assert(y == -80f, "vertical opposes the raw delta");
        (float x, float _) = KvSnap.WheelPan(1f, 0f, shift: false, speed: 80f);
        Assert(x == 80f, "horizontal tracks it");
    }
    public static void TestPanSpeedIsNotThePageScrollSpeed() {
        // The offset lands 1:1 on screen and a trackpad streams many events per swipe, so the
        // page's per-wheel-click step throws the canvas across the viewport.
        Assert(KvSnap.WheelPanSpeed < 80f, "canvas pan is gentler than a page wheel-click step");
    }
    public static void TestShiftWheelPansHorizontallyButDefersToATrackpad() {
        // A mouse reports only Y, so shift maps it onto X — DM Note's behaviour.
        (float x, float y) = KvSnap.WheelPan(0f, 1f, shift: true, speed: 80f);
        Assert(x == 80f, "shift maps a mouse's only axis onto x");
        Assert(y == 0f, "shift-wheel does not also pan vertically");
        // A trackpad already scrolling sideways must not have its own X overridden.
        (float tx, float ty) = KvSnap.WheelPan(0.5f, 1f, shift: true, speed: 80f);
        Assert(tx == 40f, "a true horizontal delta wins over the shift alias");
        Assert(ty == -80f, "and the vertical axis keeps panning vertically");
    }
    public static void TestZoomStepScalesWithScrollDistance() {
        // A mouse's one click is a full tick and must keep stepping exactly as it always did.
        Assert(KvSnap.ZoomStepFor(1f) == KvSnap.ZoomStep, "a full wheel tick is one zoom step");
        Assert(KvSnap.ZoomStepFor(-1f) == -KvSnap.ZoomStep, "and the other way out");
        // A trackpad streams many small deltas per gesture; a fixed step per event crossed the
        // whole 0.3-4.0 range in one swipe.
        Assert(Math.Abs(KvSnap.ZoomStepFor(0.05f)) < KvSnap.ZoomStep * 0.1f,
            "a trackpad's fractional delta zooms proportionally less than a wheel click");
        Assert(KvSnap.ZoomStepFor(0f) == 0f, "an idle wheel does not zoom");
        // A flick must not out-zoom a click.
        Assert(KvSnap.ZoomStepFor(12f) == KvSnap.ZoomStep, "a fast scroll is capped at one step");
    }
    public static void TestWheelDeadzoneIgnoresNoiseButNotASlowTrackpad() {
        Assert(!KvSnap.WheelMoved(0f, 0f), "an idle wheel is not a gesture");
        Assert(KvSnap.WheelMoved(0.02f, 0f), "a horizontal-only scroll counts as movement");
        // A slow trackpad scroll reports in hundredths of a tick and still has to pan.
        Assert(KvSnap.WheelMoved(0f, 0.005f), "a slow trackpad scroll is not swallowed as noise");
    }
}
