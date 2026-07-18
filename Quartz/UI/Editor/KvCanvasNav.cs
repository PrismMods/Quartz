using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
namespace Quartz.UI.Editor;
/// <summary>
/// View navigation for <see cref="KvCanvas"/>: pan, zoom and framing.
///
/// Nothing here may require a modifier key. Unity's Input never reports Shift/Ctrl/Alt on
/// macOS (see KeyLimiter.IsHookOnlyKey), and the SkyHook physical-state fallback that stands
/// in for it is only populated while the game's own hook is delivering events — and drops a
/// key it believes released roughly a second into a hold. Modifiers are therefore an
/// enhancement on this surface and never the only way to reach a view control.
/// </summary>
internal sealed partial class KvCanvas {
    /// <summary>Margin left around the elements when framing them, in viewport pixels.</summary>
    private const float FitPadding = 40f;
    private bool panning;
    private Vector2 panLast;
    /// <summary>Grab-and-drag pan, bound to the middle and right buttons.</summary>
    internal void PanDown(Vector2 screen) {
        if(root == null) return;
        panning = true;
        panLast = ScreenToOverlay(screen);
    }
    internal void PanUp() => panning = false;
    private void UpdatePan() {
        if(!panning) return;
        // Polled rather than left to OnPointerUp, which the EventSystem drops when the pointer
        // leaves the surface or the surface is disabled mid-gesture.
        if(!Input.GetMouseButton(1) && !Input.GetMouseButton(2)) {
            panning = false;
            return;
        }
        Vector2 now = ScreenToOverlay(Input.mousePosition);
        Vector2 delta = now - panLast;
        if(delta.sqrMagnitude <= 0f) return;
        panLast = now;
        PanBy(delta);
    }
    /// <summary>Shift the view by an overlay-space offset. Overlay space is unscaled and pinned to
    /// the viewport corner, so an offset maps onto the content's position 1:1 at any zoom.</summary>
    private void PanBy(Vector2 delta) {
        if(delta.sqrMagnitude <= 0f) return;
        content.anchoredPosition += delta;
        SyncOverlay();
    }
    /// <summary>Zoom about a layout-space anchor: that point must not move on screen, so the pan
    /// is corrected by the scale difference at it.</summary>
    private void ZoomToward(Vector2 anchor, float step) {
        float from = zoom;
        float to = KvSnap.ClampZoom(from + step);
        if(Mathf.Approximately(from, to)) return;
        zoom = to;
        content.localScale = new Vector3(to, to, 1f);
        content.anchoredPosition += new Vector2(anchor.x, -anchor.y) * (from - to);
        SyncOverlay();
    }
    private void ZoomAt(Vector2 screen, float step) => ZoomToward(ScreenToLayout(screen), step);
    /// <summary>Button-driven zoom. Anchored on the view centre rather than the cursor because
    /// the cursor is over the toolbar, not the canvas.</summary>
    internal void ZoomBy(bool inward) {
        if(root == null) return;
        ZoomToward(ViewCenter(), inward ? KvSnap.ZoomStep : -KvSnap.ZoomStep);
    }
    /// <summary>Back to 1:1 at the layout origin — the state <see cref="Bind"/> starts in.</summary>
    internal void ResetView() {
        if(root == null) return;
        zoom = 1f;
        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
        SyncOverlay();
    }
    /// <summary>
    /// Frame every element on the tab. This is the guaranteed way back to the layout when a pan
    /// or a zoom has left it off screen, so it must work from any view state.
    /// </summary>
    internal void FitToContent() {
        if(root == null || doc == null || viewport == null || content == null) return;
        if(!ContentBounds(out float minX, out float minY, out float maxX, out float maxY)) {
            ResetView();
            return;
        }
        Vector2 view = viewport.rect.size;
        float bw = Mathf.Max(1f, maxX - minX);
        float bh = Mathf.Max(1f, maxY - minY);
        zoom = KvSnap.ClampZoom(Mathf.Min(
            Mathf.Max(1f, view.x - (FitPadding * 2f)) / bw,
            Mathf.Max(1f, view.y - (FitPadding * 2f)) / bh
        ));
        content.localScale = new Vector3(zoom, zoom, 1f);
        CentreOn(minX, minY, maxX, maxY);
    }
    /// <summary>
    /// Frame the layout without changing the zoom — the opening view.
    ///
    /// A layout does not start near the layout origin (imports and generated presets both sit
    /// wherever their author left them), so opening at the origin puts the keys in a corner or
    /// off-screen entirely with nothing on screen to say which way to pan.
    /// </summary>
    internal void CentreView() {
        if(root == null || doc == null || viewport == null || content == null) return;
        if(!ContentBounds(out float minX, out float minY, out float maxX, out float maxY)) {
            ResetView();
            return;
        }
        CentreOn(minX, minY, maxX, maxY);
    }
    /// <summary>Put the elements' centre on the viewport's: LayoutToOverlay inverted at that point.</summary>
    private void CentreOn(float minX, float minY, float maxX, float maxY) {
        Vector2 view = viewport.rect.size;
        Vector2 mid = new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        content.anchoredPosition = new Vector2(
            (view.x * 0.5f) - (mid.x * zoom),
            (-view.y * 0.5f) + (mid.y * zoom)
        );
        SyncOverlay();
    }
    /// <summary>True once the viewport has a real size to centre against.</summary>
    private bool ViewportReady() =>
        viewport != null && viewport.rect.size.x > 1f && viewport.rect.size.y > 1f;
    /// <summary>Hidden elements are included: they still occupy the layout and still draw on the
    /// canvas, so framing without them would push them off screen.</summary>
    private bool ContentBounds(out float minX, out float minY, out float maxX, out float maxY) {
        minX = minY = maxX = maxY = 0f;
        bool any = false;
        foreach(KvElement el in doc.AllElements(tab)) {
            if(!any) {
                minX = el.X;
                minY = el.Y;
                maxX = el.X + el.W;
                maxY = el.Y + el.H;
                any = true;
                continue;
            }
            minX = Mathf.Min(minX, el.X);
            minY = Mathf.Min(minY, el.Y);
            maxX = Mathf.Max(maxX, el.X + el.W);
            maxY = Mathf.Max(maxY, el.Y + el.H);
        }
        return any;
    }
}
