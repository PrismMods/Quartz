using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Quartz.UI.Utility;
public static partial class Reorganizer {
    private static UISlider rotationSlider;
    private static RectTransform rotationRow;
    private const float RotateGripSize = 20f;
    private static void BuildRotationRow() {
        rotationRow = GenerateUI.Row(panelObj.transform);
        rotationSlider = GenerateUI.Slider(
            rotationRow, 0f, -180f, 180f, 0f,
            v => Mathf.Round(v), null, null,
            "Rotation", "reorganize_rotation"
        );
        rotationSlider.Format = "0°";
        rotationSlider.Rect.offsetMin = Vector2.zero;
        rotationSlider.Rect.offsetMax = Vector2.zero;
        rotationSlider.OnChanged = RotateSelected;
        rotationSlider.OnComplete = v => {
            RotateSelected(v);
            if(Selected != null) ReorganizeHistory.Moved(Selected);
        };
    }
    private static void ApplyRotationSlider(ReorganizeHandle handle) {
        if(rotationSlider == null || rotationRow == null) return;
        bool on = handle != null && handle.CanRotate;
        rotationRow.gameObject.SetActive(on);
        if(on) rotationSlider.SetDefaultValue(0f, true);
    }
    private static void RotateSelected(float degrees) {
        if(Selected == null || !Selected.CanRotate) return;
        Selected.SetRotation(degrees);
    }
    // A knob above the outline's top edge; it rotates with the overlay, so it always sits "above" it.
    private static void BuildRotateGrip() {
        if(Selected == null || !Selected.CanRotate || outlineObj == null) return;
        GameObject holder = new("ReorganizeRotateGripRoot");
        holder.transform.SetParent(outlineObj.transform, false);
        RectTransform holderRect = holder.AddComponent<RectTransform>();
        holderRect.anchorMin = holderRect.anchorMax = new Vector2(0.5f, 1f);
        holderRect.sizeDelta = Vector2.zero;
        GameObject grip = new("ReorganizeRotateGrip");
        grip.transform.SetParent(holder.transform, false);
        RectTransform rect = grip.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        // Pivot below the knob keeps a fixed screen gap above the outline at any overlay scale.
        rect.pivot = new Vector2(0.5f, -0.9f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(RotateGripSize, RotateGripSize);
        Image image = grip.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        image.type = Image.Type.Sliced;
        image.color = UIColors.ObjectActive;
        image.raycastTarget = true;
        grip.AddComponent<ReorganizeRotateGrip>().Owner = Selected;
        holder.AddComponent<ReorganizeGripScaler>().Grips = [grip.transform];
    }
}
public sealed class ReorganizeRotateGrip : MonoBehaviour {
    private const float SnapStep = 15f;
    public ReorganizeHandle Owner;
    private float startAngle;
    private float startRotation;
    private bool dragging;
    private void Awake() {
        EventTrigger trigger = gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerDown, OnPointerDown, trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, OnDragInternal, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, OnPointerUp, trigger);
    }
    private float PointerAngle(Vector2 pointer) {
        Vector2 d = pointer - (Vector2)Owner.Target.position;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }
    private void OnPointerDown(PointerEventData e) {
        dragging = false;
        if(e.button != PointerEventData.InputButton.Left) return;
        if(Owner == null || Owner.Target == null || !Owner.CanRotate) return;
        startAngle = PointerAngle(e.position);
        startRotation = Owner.GetRotation();
        dragging = true;
    }
    private void OnDragInternal(PointerEventData e) {
        if(!dragging || Owner == null || Owner.Target == null || !Owner.CanRotate) return;
        float rotation = Mathf.DeltaAngle(0f, startRotation + PointerAngle(e.position) - startAngle);
        if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            rotation = Mathf.Round(rotation / SnapStep) * SnapStep;
        Owner.SetRotation(Mathf.Round(rotation));
        Reorganizer.SyncSelectedSliders();
    }
    private void OnPointerUp(PointerEventData e) {
        if(!dragging) return;
        dragging = false;
        if(Owner != null) ReorganizeHistory.Moved(Owner);
    }
}
