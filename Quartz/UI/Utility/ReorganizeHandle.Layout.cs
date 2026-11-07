using Quartz.Overlay;
using UnityEngine;
namespace Quartz.UI.Utility;
// Handles tagged with a layout id are what Quartz offers PrismLib's shared layout editor (Quartz/Prism/QuartzLayout).
// Their own registry: Reorganizer's list only holds live drag surfaces, which are off during a shared session.
public sealed partial class ReorganizeHandle {
    internal string LayoutId;
    /// <summary>Null means always movable; false hides the element from the shared editor (a line-style progress bar).</summary>
    internal Func<bool> LayoutMovable;
    internal Action<bool> SetClosest;
    /// <summary>What the element belongs to when its id is only a position (a panel's PanelConfig): found by this instead.</summary>
    internal object LayoutOwner;
    private static readonly List<ReorganizeHandle> layoutHandles = [];
    private static int layoutVersion;
    /// <summary>Changes with every tag and every tagged handle destroyed, so a reader knows to describe the list again.</summary>
    internal static int LayoutVersion {
        get {
            if(layoutHandles.RemoveAll(static h => h == null) > 0) layoutVersion++;
            return layoutVersion;
        }
    }
    internal static void SetLayoutTarget(GameObject drag, string id, Func<bool> movable = null, object owner = null) {
        if(drag == null || drag.GetComponent<ReorganizeHandle>() is not { } handle) return;
        handle.LayoutId = id;
        handle.LayoutMovable = movable;
        handle.LayoutOwner = owner;
        // a rebuilt overlay replaces its old handle
        layoutHandles.RemoveAll(h => h == null || h == handle || h.LayoutId == id || (owner != null && h.LayoutOwner == owner));
        layoutHandles.Add(handle);
        layoutVersion++;
    }
    /// <summary>
    /// The live handle tagged <paramref name="id"/>, or the one <paramref name="owner"/> owns when given; null when there is
    /// none or it was destroyed.
    /// </summary>
    internal static ReorganizeHandle FindLayout(string id, object owner = null) {
        foreach(ReorganizeHandle h in layoutHandles)
            if(h != null && h.Target != null && (owner != null ? h.LayoutOwner == owner : h.LayoutId == id)) return h;
        return null;
    }
    /// <summary>Every live tagged handle, in LayoutIds order (panels last, by number).</summary>
    internal static List<ReorganizeHandle> LayoutHandles() =>
        [.. layoutHandles.Where(h => h != null && h.Target != null).OrderBy(h => LayoutIds.Order(h.LayoutId))];
    internal static void SetAnchorSource(GameObject drag, Func<bool> closest, Action<OverlayAnchor> setAnchor, Action<bool> setClosest) {
        SetAnchorSource(drag, closest, setAnchor);
        if(drag != null && drag.GetComponent<ReorganizeHandle>() is { } handle) handle.SetClosest = setClosest;
    }
    internal bool LayoutShown => Target.gameObject.activeInHierarchy && LayoutMovable?.Invoke() != false;
    /// <summary>
    /// PrismLib's box: {centre x, centre y, width, height} in screen pixels with the origin bottom-left, size before rotation.
    /// Overlay canvases are screen-space overlay, so world space is screen space (the canvas scaler is in lossyScale).
    /// Null while the element is hidden or cannot move.
    /// </summary>
    internal float[] LayoutBox() {
        if(!LayoutShown) return null;
        RectTransform m = MeasureRect;
        Rect r = m.rect;
        Vector3 c = m.TransformPoint(r.center);
        Vector3 s = m.lossyScale;
        return [c.x, c.y, r.width * Mathf.Abs(s.x), r.height * Mathf.Abs(s.y)];
    }
    /// <summary>
    /// Puts the box centre on a screen point by moving the RectTransform, as Reorganizer.MoveSelected does: while arranging,
    /// the overlay copies its RectTransform into its settings every frame, so a settings-only write would be overwritten.
    /// </summary>
    internal void LayoutMoveTo(float x, float y) {
        if(!LayoutShown) return;
        RectTransform m = MeasureRect;
        Vector3 c = m.TransformPoint(m.rect.center);
        Target.position += new Vector3(x - c.x, y - c.y, 0f);
        OnMoved?.Invoke();
    }
}
