using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    private const float FitPadding = 40f;
    private bool panning;
    private Vector2 panLast;
    internal void PanDown(Vector2 screen) {
        if(root == null) return;
        panning = true;
        panLast = ScreenToOverlay(screen);
    }
    internal void PanUp() => panning = false;
    private void UpdatePan() {
        if(!panning) return;
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
    private void PanBy(Vector2 delta) {
        if(delta.sqrMagnitude <= 0f) return;
        content.anchoredPosition += delta;
        SyncOverlay();
    }
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
    internal void ZoomBy(bool inward) {
        if(root == null) return;
        ZoomToward(ViewCenter(), inward ? KvSnap.ZoomStep : -KvSnap.ZoomStep);
    }
    internal void ResetView() {
        if(root == null) return;
        zoom = 1f;
        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
        SyncOverlay();
    }
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
    internal void CentreView() {
        if(root == null || doc == null || viewport == null || content == null) return;
        if(!ContentBounds(out float minX, out float minY, out float maxX, out float maxY)) {
            ResetView();
            return;
        }
        CentreOn(minX, minY, maxX, maxY);
    }
    private void CentreOn(float minX, float minY, float maxX, float maxY) {
        Vector2 view = viewport.rect.size;
        Vector2 mid = new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        content.anchoredPosition = new Vector2(
            (view.x * 0.5f) - (mid.x * zoom),
            (-view.y * 0.5f) + (mid.y * zoom)
        );
        SyncOverlay();
    }
    private bool ViewportReady() =>
        viewport != null && viewport.rect.size.x > 1f && viewport.rect.size.y > 1f;
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
