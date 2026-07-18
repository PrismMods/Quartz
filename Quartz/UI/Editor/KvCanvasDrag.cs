using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    private readonly List<KvElement> dragTargets = [];
    private readonly List<Vector2> dragOrigins = [];
    private readonly List<KvRect> dragOthers = [];
    private readonly List<KvElement> marqueeHits = [];
    private KvElement dragPrimary;
    private Vector2 dragPrimaryOrigin;
    private bool axisLatched;
    private bool lockToX;
    private void BeginDrag() {
        PushHistory();
        dragTargets.Clear();
        dragOrigins.Clear();
        dragOthers.Clear();
        foreach(KvElement el in selection) {
            dragTargets.Add(el);
            dragOrigins.Add(new Vector2(el.X, el.Y));
        }
        dragPrimary = pressElement != null && selection.Contains(pressElement)
            ? pressElement
            : selection.Count > 0 ? selection[0] : null;
        if(dragPrimary != null) dragPrimaryOrigin = new Vector2(dragPrimary.X, dragPrimary.Y);
        // Snapping targets exclude the whole selection, not just the primary, or a multi-drag
        // would snap to the elements travelling with it. Cached here because the set cannot
        // change mid-drag.
        foreach(Visual v in visuals)
            if(!selection.Contains(v.El)) dragOthers.Add(RectOf(v.El));
        axisLatched = false;
    }
    private void UpdateDrag(Vector2 layout) {
        if(dragPrimary == null || dragTargets.Count == 0) return;
        Vector2 raw = layout - pressLayout;
        // Latched once, on the first movement past the threshold, and never re-evaluated: the
        // axis is a property of the gesture's initial direction. Deltas are layout-space (y
        // down) because KvSnap.ShiftLocksToX reproduces a DM Note quirk that compares signed
        // deltas, so a y-up delta would invert it.
        if(!axisLatched) {
            lockToX = KvSnap.ShiftLocksToX(raw.x, raw.y);
            axisLatched = true;
        }
        if(ShiftHeld()) {
            if(lockToX) raw.y = 0f;
            else raw.x = 0f;
        }
        Vector2 applied;
        if(AltHeld()) {
            applied = raw;
            ClearGuides();
        } else {
            KvRect desired = new(
                dragPrimaryOrigin.x + raw.x, dragPrimaryOrigin.y + raw.y, dragPrimary.W, dragPrimary.H
            );
            KvSnapResult snapped = KvSnap.SnapMove(desired, dragOthers, zoom);
            applied = new Vector2(snapped.X - dragPrimaryOrigin.x, snapped.Y - dragPrimaryOrigin.y);
            ShowGuides(snapped.Guides);
        }
        for(int i = 0; i < dragTargets.Count; i++)
            dragTargets[i].MoveTo(dragOrigins[i].x + applied.x, dragOrigins[i].y + applied.y);
        SyncGeometry();
    }
    private void EndDrag() {
        ClearGuides();
        dragTargets.Clear();
        dragOrigins.Clear();
        dragOthers.Clear();
        dragPrimary = null;
        Mutated();
    }
    private void UpdateMarquee(Vector2 layout) {
        KvRect band = FromCorners(pressLayout, layout);
        ShowMarquee(band);
        marqueeHits.Clear();
        if(pressAdditive) marqueeHits.AddRange(selectionAtPress);
        foreach(Visual v in visuals) {
            if(v.El.Hidden) continue;
            if(band.Intersects(RectOf(v.El)) && !marqueeHits.Contains(v.El)) marqueeHits.Add(v.El);
        }
        SetSelectionIfChanged(marqueeHits);
    }
    private void SetSelectionIfChanged(List<KvElement> next) {
        if(next.Count == selection.Count) {
            bool same = true;
            for(int i = 0; i < next.Count; i++) {
                if(selection[i] == next[i]) continue;
                same = false;
                break;
            }
            if(same) return;
        }
        SetSelection(next);
    }
    private static KvRect FromCorners(Vector2 a, Vector2 b) => new(
        Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)
    );
}
