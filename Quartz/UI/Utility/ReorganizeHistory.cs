using UnityEngine;
namespace Quartz.UI.Utility;
// Records reorganize moves and resizes onto the shared UndoTimeline. Each edit diffs against the state seen at selection
// or the last commit, so drags, grips and the X/Y/size sliders all land here through Moved().
internal static class ReorganizeHistory {
    private readonly struct State(Vector2 pos, float size, float rotation, int anchor) {
        public readonly Vector2 Pos = pos;
        public readonly int Anchor = anchor;
        public readonly float Size = size;
        public readonly float Rotation = rotation;
        public bool Same(State o) =>
            Pos == o.Pos && Anchor == o.Anchor && Mathf.Approximately(Size, o.Size) && Mathf.Approximately(Rotation, o.Rotation);
    }
    private static ReorganizeHandle baselineHandle;
    private static State baseline;
    private static State Capture(ReorganizeHandle h) => new(
        h.Target.anchoredPosition, h.CanResize ? h.GetSize() : 0f, h.CanRotate ? h.GetRotation() : 0f,
        h.HasAnchor ? (int)h.CurrentAnchor : -1);
    internal static void Baseline(ReorganizeHandle handle) {
        baselineHandle = handle;
        if(handle?.Target != null) baseline = Capture(handle);
    }
    internal static void Moved(ReorganizeHandle handle) {
        if(handle?.Target == null) return;
        State now = Capture(handle);
        if(handle == baselineHandle && !baseline.Same(now)) {
            State before = baseline;
            UndoTimeline.Push(() => Apply(handle, before), () => Apply(handle, now));
        }
        Baseline(handle);
        handle.OnMoved?.Invoke();
    }
    // False once the overlay was rebuilt and this handle died.
    private static bool Apply(ReorganizeHandle handle, State s) {
        if(handle == null || handle.Target == null) return false;
        if(s.Anchor >= 0 && handle.SetAnchor != null && handle.HasAnchor && s.Anchor != (int)handle.CurrentAnchor)
            handle.SetAnchor((Quartz.Overlay.OverlayAnchor)s.Anchor);
        // Size and rotation setters re-apply the overlay from its saved offsets, so the position goes last.
        if(handle.CanResize) handle.SetSize(s.Size);
        if(handle.CanRotate) handle.SetRotation(s.Rotation);
        handle.Target.anchoredPosition = s.Pos;
        if(Reorganizer.Selected == handle) Baseline(handle);
        handle.OnMoved?.Invoke();
        Reorganizer.SyncSelectedSliders();
        return true;
    }
}
