using UnityEngine;
using UnityEngine.EventSystems;
namespace Quartz.UI.Utility;
public class DragHandler : MonoBehaviour {
    private RectTransform rect;
    private Vector2 offset;
    private static readonly Vector3[] selfCorners = new Vector3[4];
    private static readonly Vector3[] parentCorners = new Vector3[4];
    private void Awake() {
        rect = transform.parent?.GetComponent<RectTransform>();
        SetupEvents();
    }
    private void SetupEvents() {
        var trigger = gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerDown, _ => OnPointerDownInternal(), trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, _ => OnDragInternal(), trigger);
    }
    private void OnPointerDownInternal() {
        if(rect == null) rect = transform.parent?.GetComponent<RectTransform>();
        if(rect == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            Input.mousePosition,
            null,
            out Vector2 localPoint
        );
        offset = rect.anchoredPosition - localPoint;
    }
    private void OnDragInternal() {
        if(rect == null) rect = transform.parent?.GetComponent<RectTransform>();
        if(rect == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            Input.mousePosition,
            null,
            out Vector2 localPoint
        );
        rect.anchoredPosition = localPoint + offset;
        ClampToParent(rect);
    }
    public static void ClampToParent(RectTransform rect) {
        if(rect == null || rect.parent is not RectTransform parent) return;
        rect.GetWorldCorners(selfCorners);
        parent.GetWorldCorners(parentCorners);
        Vector2 shift = new(
            AxisShift(selfCorners[0].x, selfCorners[2].x, parentCorners[0].x, parentCorners[2].x),
            AxisShift(selfCorners[0].y, selfCorners[2].y, parentCorners[0].y, parentCorners[2].y)
        );
        if(shift == Vector2.zero) return;
        Vector3 scale = parent.lossyScale;
        Vector2 position = rect.anchoredPosition;
        if(!Mathf.Approximately(scale.x, 0f)) position.x += shift.x / scale.x;
        if(!Mathf.Approximately(scale.y, 0f)) position.y += shift.y / scale.y;
        rect.anchoredPosition = position;
    }
    private static float AxisShift(float min, float max, float boundsMin, float boundsMax) {
        if(max - min <= boundsMax - boundsMin) {
            if(min < boundsMin) return boundsMin - min;
            if(max > boundsMax) return boundsMax - max;
            return 0f;
        }
        if(min > boundsMin) return boundsMin - min;
        if(max < boundsMax) return boundsMax - max;
        return 0f;
    }
}
