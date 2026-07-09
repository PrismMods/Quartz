using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Quartz.UI.Utility;
public class UnityUtils {
    private static readonly char[] countBuf = new char[16];
    public static void SetCount(TextMeshProUGUI tmp, int value) {
        int pos = countBuf.Length;
        if(value == 0) {
            countBuf[--pos] = '0';
        } else {
            long v = value;
            bool neg = v < 0;
            if(neg) v = -v;
            while(v > 0) {
                countBuf[--pos] = (char)('0' + (int)(v % 10));
                v /= 10;
            }
            if(neg) countBuf[--pos] = '-';
        }
        tmp.SetText(countBuf, pos, countBuf.Length - pos);
    }
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
