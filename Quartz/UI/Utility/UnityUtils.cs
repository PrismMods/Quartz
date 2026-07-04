using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Quartz.UI.Utility;

public class UnityUtils {
    // Standard overlay HUD canvas: screen-space overlay, 1920x1080-reference
    // scaling, and a disabled GraphicRaycaster (overlay canvases only need
    // raycasts while Reorganize mode is active — keeping the raycaster off
    // keeps them out of the EventSystem's raycast list during gameplay).
    public static GameObject CreateOverlayCanvas(string name, Transform parent, int sortingOrder, out GraphicRaycaster raycaster) {
        GameObject canvasObj = new(name);
        canvasObj.transform.SetParent(parent, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;
        return canvasObj;
    }

    public static void AddEvent(EventTriggerType type, Action<PointerEventData> cb, EventTrigger trigger) {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(e => cb((PointerEventData)e));
        trigger.triggers.Add(entry);
    }

    // Click = PointerUp released over the pressed element (or a child of it).
    //
    // Deliberately NOT EventTriggerType.PointerClick: EventTrigger implements
    // IDragHandler, so the moment the cursor moves past the EventSystem's
    // drag threshold between press and release, Unity marks the press
    // ineligible for click and PointerClick never fires. At low frame rates
    // the cursor easily covers that distance during a normal click, which
    // made buttons randomly eat clicks. PointerUp always reaches the pressed
    // object; the raycast check keeps press-here-release-elsewhere from
    // counting as a click.
    public static void AddClickEvent(EventTrigger trigger, Action<PointerEventData> cb) {
        AddEvent(EventTriggerType.PointerUp, e => {
            if(ReleasedInside(e, trigger.transform)) cb(e);
        }, trigger);
    }

    public static bool ReleasedInside(PointerEventData e, Transform root) {
        GameObject over = e.pointerCurrentRaycast.gameObject;
        return over != null && (over.transform == root || over.transform.IsChildOf(root));
    }
}
