using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Quartz.UI.Utility;
public enum PaneDividerAxis {
    Horizontal, 
    Vertical,   
}
public sealed class PaneDivider : MonoBehaviour {
    public RectTransform Target;
    public RectTransform CoordinateSpace;
    public PaneDividerAxis Axis = PaneDividerAxis.Horizontal;
    /// <summary>
    /// Set when the pane grows away from the pointer's direction of travel — a right-docked pane
    /// widens as the divider is dragged left, where a bottom-docked one grows as it is dragged up.
    /// Opt-in so a pane that never asked for it keeps the sign it was written against.
    /// </summary>
    public bool Invert;
    public float MinSize = 160f;
    public float MaxSize = 640f;
    public Action<float> OnResizeEnd;
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
        if(Invert) delta = -delta;
        float newSize = Mathf.Clamp(startSize + delta, MinSize, MaxSize);
        Target.sizeDelta = Axis == PaneDividerAxis.Horizontal
            ? new Vector2(newSize, Target.sizeDelta.y)
            : new Vector2(Target.sizeDelta.x, newSize);
        OnResized?.Invoke(newSize);
    }
}
