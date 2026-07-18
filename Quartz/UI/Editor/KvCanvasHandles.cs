using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    // DM Note's ResizeHandles geometry: 10px corners, 8x18 edges, and a 18px hit box that
    // deliberately overhangs the drawn handle.
    private const float HandleCorner = 10f;
    private const float HandleEdgeThick = 8f;
    private const float HandleEdgeLong = 18f;
    private const float HandleHitSize = 18f;
    /// <summary>nw n ne w e sw s se, as (x, y) directions. y is down, so -1 is north.</summary>
    private static readonly (int dx, int dy)[] HandleDirs = [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    ];
    private RectTransform handleRoot;
    private RectTransform[] handles;
    private int resizeDir = -1;
    private KvRect resizeOrig;
    private Vector2 resizeStartLayout;
    private readonly List<float> siblingW = [];
    private readonly List<float> siblingH = [];
    private void BuildHandles() {
        GameObject rootObj = new("Handles");
        rootObj.transform.SetParent(overlay, false);
        handleRoot = rootObj.AddComponent<RectTransform>();
        handleRoot.anchorMin = new Vector2(0f, 1f);
        handleRoot.anchorMax = new Vector2(0f, 1f);
        handleRoot.pivot = new Vector2(0f, 1f);
        handleRoot.anchoredPosition = Vector2.zero;
        handleRoot.sizeDelta = Vector2.zero;
        handles = new RectTransform[HandleDirs.Length];
        for(int i = 0; i < HandleDirs.Length; i++) {
            GameObject obj = new("Handle" + i);
            obj.transform.SetParent(rootObj.transform, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image img = obj.AddComponent<Image>();
            img.color = UIColors.ObjectActive;
            // Hit-testing is done against the layout rect, not the raycaster, so the drawn
            // handle can stay smaller than the grab area.
            img.raycastTarget = false;
            handles[i] = rect;
        }
        rootObj.SetActive(false);
    }
    private static Vector2 HandleSize(int dx, int dy) {
        if(dx != 0 && dy != 0) return new Vector2(HandleCorner, HandleCorner);
        return dx == 0 ? new Vector2(HandleEdgeLong, HandleEdgeThick) : new Vector2(HandleEdgeThick, HandleEdgeLong);
    }
    private Vector2 HandleCenter(KvRect r, int dx, int dy) {
        float lx = dx < 0 ? r.Left : dx > 0 ? r.Right : r.CenterX;
        float ly = dy < 0 ? r.Top : dy > 0 ? r.Bottom : r.CenterY;
        return LayoutToOverlay(lx, ly);
    }
    private void SyncHandles() {
        if(handleRoot == null) return;
        bool show = selection.Count == 1;
        if(handleRoot.gameObject.activeSelf != show) handleRoot.gameObject.SetActive(show);
        if(!show) return;
        KvRect r = RectOf(selection[0]);
        for(int i = 0; i < handles.Length; i++) {
            (int dx, int dy) = HandleDirs[i];
            handles[i].anchoredPosition = HandleCenter(r, dx, dy);
            handles[i].sizeDelta = HandleSize(dx, dy);
        }
    }
    private int HandleHit(Vector2 overlayPoint) {
        if(selection.Count != 1) return -1;
        KvRect r = RectOf(selection[0]);
        for(int i = 0; i < HandleDirs.Length; i++) {
            (int dx, int dy) = HandleDirs[i];
            Vector2 c = HandleCenter(r, dx, dy);
            if(Mathf.Abs(overlayPoint.x - c.x) <= HandleHitSize * 0.5f
                && Mathf.Abs(overlayPoint.y - c.y) <= HandleHitSize * 0.5f) return i;
        }
        return -1;
    }
    private void BeginResize(int index) {
        if(selection.Count != 1) return;
        resizeDir = index;
        resizeOrig = RectOf(selection[0]);
        resizeStartLayout = pressLayout;
        siblingW.Clear();
        siblingH.Clear();
        foreach(Visual v in visuals) {
            if(v.El == selection[0]) continue;
            siblingW.Add(v.El.W);
            siblingH.Add(v.El.H);
        }
        PushHistory();
    }
    private void UpdateResize(Vector2 layout) {
        if(resizeDir < 0 || selection.Count != 1) return;
        (int dx, int dy) = HandleDirs[resizeDir];
        Vector2 d = layout - resizeStartLayout;
        float w = dx == 0 ? resizeOrig.W : dx > 0 ? resizeOrig.W + d.x : resizeOrig.W - d.x;
        float h = dy == 0 ? resizeOrig.H : dy > 0 ? resizeOrig.H + d.y : resizeOrig.H - d.y;
        w = Mathf.Max(KvElement.MinSize, w);
        h = Mathf.Max(KvElement.MinSize, h);
        if(!AltHeld()) {
            if(dx != 0) w = KvSnap.SnapSize(w, siblingW);
            if(dy != 0) h = KvSnap.SnapSize(h, siblingH);
        }
        if(ShiftHeld() && resizeOrig.W > 0f && resizeOrig.H > 0f) {
            float aspect = resizeOrig.W / resizeOrig.H;
            if(dx != 0 && dy != 0) {
                if(Mathf.Abs(w - resizeOrig.W) >= Mathf.Abs(h - resizeOrig.H)) h = w / aspect;
                else w = h * aspect;
            } else if(dx != 0) {
                h = w / aspect;
            } else if(dy != 0) {
                w = h * aspect;
            }
        }
        w = Mathf.Max(KvElement.MinSize, w);
        h = Mathf.Max(KvElement.MinSize, h);
        KvElement el = selection[0];
        el.Resize(w, h);
        el.MoveTo(dx < 0 ? resizeOrig.Right - w : resizeOrig.Left, dy < 0 ? resizeOrig.Bottom - h : resizeOrig.Top);
        SyncGeometry();
    }
    private void EndResize() {
        if(resizeDir < 0) return;
        resizeDir = -1;
        Mutated();
    }
}
