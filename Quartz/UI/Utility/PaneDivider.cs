using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Quartz.UI.Utility;

public enum PaneDividerAxis {
    Horizontal, // resizes Target.sizeDelta.x — Target's left edge is pinned
    Vertical,   // resizes Target.sizeDelta.y — Target's bottom edge is pinned
}

// Generic drag-to-resize divider for an internal split (e.g. the settings
// window's bottom context band), as opposed to ResizeHandle which resizes the
// whole outer Panel and persists straight to UICore.SavePanelSize(). This
// widens/narrows or grows/shrinks a single caller-supplied RectTransform and
// leaves persistence entirely to the caller via OnResizeEnd, so it isn't tied
// to any one CoreSettings field.
//
// Horizontal assumes Target has a left-edge pivot (pivot.x == 0) with its left
// edge pinned elsewhere; Vertical assumes a bottom-edge pivot (pivot.y == 0)
// with its bottom edge pinned elsewhere. Either way, growing the relevant
// sizeDelta component extends Target away from its pinned edge without
// needing a matching anchoredPosition adjustment — unlike ResizeHandle, which
// has to handle all 8 directions and re-center the pivot on every resize.
public sealed class PaneDivider : MonoBehaviour {
    // The RectTransform this divider resizes (its sizeDelta.x or .y, per Axis).
    public RectTransform Target;
    // Ancestor RectTransform used to convert screen-space mouse movement into
    // local units consistent with Target's parent hierarchy (usually the same
    // parent passed to ResizeHandle.CreateResizeHandles for the outer panel).
    public RectTransform CoordinateSpace;
    public PaneDividerAxis Axis = PaneDividerAxis.Horizontal;
    public float MinSize = 160f;
    public float MaxSize = 640f;
    // Invoked on pointer-up with the final size, only when it actually
    // changed (not on a bare click) — mirrors ResizeHandle's persist-on-change
    // guard so the caller can save it.
    public Action<float> OnResizeEnd;
    // Invoked every drag frame with the newly applied size, after Target's
    // sizeDelta is written — for callers whose surrounding layout must follow
    // live (e.g. Page yielding height to the bottom band during the drag).
    public Action<float> OnResized;

    private float startMouseCoord;
    private float startSize;
    private bool hovered;
    private bool dragging;

    private ResizeCursorShape CursorShape =>
        Axis == PaneDividerAxis.Horizontal ? ResizeCursorShape.Horizontal : ResizeCursorShape.Vertical;

    private void Awake() {
        var trigger = gameObject.AddComponent<EventTrigger>();

        UnityUtils.AddEvent(EventTriggerType.PointerDown, _ => { dragging = true; OnPointerDownInternal(); }, trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, _ => OnDragInternal(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, _ => {
            dragging = false;
            if(Target != null && !Mathf.Approximately(CurrentSize(), startSize)) {
                OnResizeEnd?.Invoke(CurrentSize());
            }
            if(!hovered) NativeCursor.Reset();
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => { hovered = true; NativeCursor.Apply(CursorShape); }, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => {
            hovered = false;
            if(!dragging) NativeCursor.Reset();
        }, trigger);
    }

    // The OS reclaims the cursor on every mouse-move message, so re-assert it
    // each frame while this handle is hovered or being dragged.
    private void Update() {
        if(hovered || dragging) NativeCursor.Apply(CursorShape);
    }

    private void OnDisable() {
        if(hovered || dragging) {
            hovered = false;
            dragging = false;
            NativeCursor.Reset();
        }
    }

    private float CurrentSize() => Axis == PaneDividerAxis.Horizontal ? Target.sizeDelta.x : Target.sizeDelta.y;

    private void OnPointerDownInternal() {
        if(Target == null || CoordinateSpace == null) return;
        startSize = CurrentSize();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(CoordinateSpace, Input.mousePosition, null, out Vector2 local);
        startMouseCoord = Axis == PaneDividerAxis.Horizontal ? local.x : local.y;
    }

    private void OnDragInternal() {
        if(Target == null || CoordinateSpace == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(CoordinateSpace, Input.mousePosition, null, out Vector2 local);
        float coord = Axis == PaneDividerAxis.Horizontal ? local.x : local.y;
        float delta = coord - startMouseCoord;

        // The divider sits on Target's own outer edge (right for Horizontal,
        // top for Vertical); Target's opposite edge is pinned elsewhere, so
        // moving the mouse toward the outer edge (positive delta, in either
        // axis) grows Target and moving away shrinks it.
        float newSize = Mathf.Clamp(startSize + delta, MinSize, MaxSize);

        Target.sizeDelta = Axis == PaneDividerAxis.Horizontal
            ? new Vector2(newSize, Target.sizeDelta.y)
            : new Vector2(Target.sizeDelta.x, newSize);

        OnResized?.Invoke(newSize);
    }
}
