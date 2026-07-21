using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
namespace Quartz.UI.Utility;
public sealed class ReorganizeHandle : MonoBehaviour {
    public RectTransform Target;
    public RectTransform Bounds;
    internal RectTransform MeasureRect => Bounds != null ? Bounds : Target;
    public Func<string> GetName;
    public Action OnMoved;
    private Vector2 grabOffset;
    private bool armed;
    private bool moved;
    private void Awake() {
        EventTrigger trigger = gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerDown, OnPointerDown, trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, OnDragInternal, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, OnPointerUp, trigger);
    }
    private void OnEnable() => Reorganizer.Register(this);
    private void OnDisable() => Reorganizer.Unregister(this);
    private void OnPointerDown(PointerEventData e) {
        if(e.button != PointerEventData.InputButton.Left || Target == null) return;
        armed = Reorganizer.Selected == this;
        moved = false;
        if(!armed) Reorganizer.Select(this);
        grabOffset = (Vector2)Target.position - e.position;
    }
    private void OnDragInternal(PointerEventData e) {
        if(!armed || Target == null) return;
        Vector3 pos = Target.position;
        Vector2 next = e.position + grabOffset;
        pos.x = next.x;
        pos.y = next.y;
        Target.position = pos;
        Reorganizer.SnapDuringDrag(this);
        Reorganizer.SyncSelectedSliders();
        moved = true;
    }
    private void OnPointerUp(PointerEventData e) {
        if(moved) {
            moved = false;
            OnMoved?.Invoke();
        }
    }
    public static GameObject CreateDragSurface(RectTransform target, Func<string> getName, Action onMoved, bool ignoreLayout = false) {
        GameObject drag = new("Drag");
        drag.transform.SetParent(target, false);
        RectTransform dragRect = drag.AddComponent<RectTransform>();
        dragRect.anchorMin = Vector2.zero;
        dragRect.anchorMax = Vector2.one;
        dragRect.offsetMin = Vector2.zero;
        dragRect.offsetMax = Vector2.zero;
        if(ignoreLayout) drag.AddComponent<LayoutElement>().ignoreLayout = true;
        drag.AddComponent<EmptyGraphic>().raycastTarget = true;
        ReorganizeHandle handle = drag.AddComponent<ReorganizeHandle>();
        handle.Target = target;
        handle.GetName = getName;
        handle.OnMoved = onMoved;
        drag.SetActive(false);
        return drag;
    }
}
public static class Reorganizer {
    private const float VirtualW = 1920f;
    private const float VirtualH = 1080f;
    private const float SnapThreshold = 8f;
    private static readonly List<ReorganizeHandle> handles = [];
    public static ReorganizeHandle Selected { get; private set; }
    private static GameObject canvasObj;
    private static GameObject panelObj;
    private static TextMeshProUGUI nameLabel;
    private static UISlider xSlider;
    private static UISlider ySlider;
    private static GameObject outlineObj;
    internal static void Register(ReorganizeHandle handle) {
        if(!handles.Contains(handle)) handles.Add(handle);
    }
    internal static void Unregister(ReorganizeHandle handle) {
        handles.Remove(handle);
        if(Selected == handle) Deselect();
    }
    public static void Select(ReorganizeHandle handle) {
        if(Selected == handle) return;
        ClearOutline();
        Selected = handle;
        if(handle?.Target == null) {
            Selected = null;
            Deselect();
            return;
        }
        BuildOutline(handle.MeasureRect);
        EnsurePanel();
        nameLabel.text = handle.GetName?.Invoke() ?? handle.Target.name;
        SyncSelectedSliders();
        panelObj.SetActive(true);
    }
    public static void Deselect() {
        ClearOutline();
        Selected = null;
        if(panelObj != null) panelObj.SetActive(false);
    }
    public static void Dispose() {
        Deselect();
        handles.Clear();
        if(canvasObj != null) Object.Destroy(canvasObj);
        canvasObj = null;
        panelObj = null;
        nameLabel = null;
        xSlider = null;
        ySlider = null;
    }
    private static void BuildOutline(RectTransform target) {
        outlineObj = new GameObject("ReorganizeOutline");
        outlineObj.transform.SetParent(target, false);
        RectTransform rect = outlineObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-6f, -6f);
        rect.offsetMax = new Vector2(6f, 6f);
        outlineObj.AddComponent<LayoutElement>().ignoreLayout = true;
        Image img = outlineObj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        img.type = Image.Type.Sliced;
        img.color = UIColors.ObjectActive;
        img.raycastTarget = false;
    }
    private static void ClearOutline() {
        if(outlineObj != null) Object.Destroy(outlineObj);
        outlineObj = null;
    }
    private static void EnsurePanel() {
        if(panelObj != null) return;
        canvasObj = new GameObject("QuartzReorganizeCanvas");
        canvasObj.transform.SetParent(MainCore.Root.transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32765;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(VirtualW, VirtualH);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        panelObj = new GameObject("PositionPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(18f, 18f);
        rect.sizeDelta = new Vector2(640f, 0f);
        Image bg = panelObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.PanelBG;
        bg.raycastTarget = false;
        CanvasGroup group = panelObj.AddComponent<CanvasGroup>();
        group.alpha = 0.5f;
        group.blocksRaycasts = true;
        VerticalLayoutGroup layout = GenerateUI.FitVertical(panelObj, 8f);
        layout.padding = new RectOffset(14, 14, 12, 12);
        RectTransform nameRow = GenerateUI.Row(panelObj.transform, 34f);
        nameLabel = GenerateUI.AddText(nameRow, true);
        nameLabel.fontSize = 22f;
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.raycastTarget = false;
        xSlider = MakeAxisSlider("X Position", "reorganize_x", VirtualW, v => MoveSelected(v, null));
        ySlider = MakeAxisSlider("Y Position", "reorganize_y", VirtualH, v => MoveSelected(null, v));
        panelObj.SetActive(false);
    }
    private static UISlider MakeAxisSlider(string text, string id, float max, Action<float> apply) {
        UISlider slider = GenerateUI.Slider(
            GenerateUI.Row(panelObj.transform),
            max * 0.5f, 0f, max, max * 0.5f,
            v => Mathf.Round(v), null, null,
            text, id
        );
        slider.Format = "0";
        slider.Rect.offsetMin = Vector2.zero;
        slider.Rect.offsetMax = Vector2.zero;
        slider.OnChanged = v => apply(v);
        slider.OnComplete = v => {
            apply(v);
            Selected?.OnMoved?.Invoke();
        };
        return slider;
    }
    internal static void SyncSelectedSliders() {
        if(Selected?.Target == null || xSlider == null) return;
        Vector2 center = ScreenToVirtual(ScreenCenter(Selected.MeasureRect));
        xSlider.SetOnlyValue(center.x, true);
        ySlider.SetOnlyValue(center.y, true);
    }
    private static void MoveSelected(float? virtualX, float? virtualY) {
        if(Selected?.Target == null) return;
        RectTransform rt = Selected.Target;
        Vector2 center = ScreenCenter(Selected.MeasureRect);
        Vector2 target = center;
        if(virtualX.HasValue) target.x = virtualX.Value / VirtualW * Screen.width;
        if(virtualY.HasValue) target.y = virtualY.Value / VirtualH * Screen.height;
        Vector3 pos = rt.position;
        pos.x += target.x - center.x;
        pos.y += target.y - center.y;
        rt.position = pos;
    }
    internal static void SnapDuringDrag(ReorganizeHandle handle) {
        if(handle?.Target == null) return;
        if(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return;
        GetScreenRect(handle.MeasureRect, out float left, out float right, out float bottom, out float top);
        float cx = (left + right) * 0.5f;
        float cy = (bottom + top) * 0.5f;
        float threshold = SnapThreshold * Screen.height / VirtualH;
        float bestDx = float.MaxValue;
        float bestDy = float.MaxValue;
        void TryX(float candidate) {
            Consider(candidate - left, ref bestDx);
            Consider(candidate - cx, ref bestDx);
            Consider(candidate - right, ref bestDx);
        }
        void TryY(float candidate) {
            Consider(candidate - bottom, ref bestDy);
            Consider(candidate - cy, ref bestDy);
            Consider(candidate - top, ref bestDy);
        }
        TryX(0f);
        TryX(Screen.width * 0.5f);
        TryX(Screen.width);
        TryY(0f);
        TryY(Screen.height * 0.5f);
        TryY(Screen.height);
        foreach(ReorganizeHandle other in handles) {
            if(other == handle || other.Target == null || !other.isActiveAndEnabled) continue;
            GetScreenRect(other.MeasureRect, out float l, out float r, out float b, out float t);
            TryX(l);
            TryX((l + r) * 0.5f);
            TryX(r);
            TryY(b);
            TryY((b + t) * 0.5f);
            TryY(t);
        }
        float dx = Mathf.Abs(bestDx) <= threshold ? bestDx : 0f;
        float dy = Mathf.Abs(bestDy) <= threshold ? bestDy : 0f;
        if(dx != 0f || dy != 0f) {
            Vector3 pos = handle.Target.position;
            pos.x += dx;
            pos.y += dy;
            handle.Target.position = pos;
        }
    }
    private static void Consider(float delta, ref float best) {
        if(Mathf.Abs(delta) < Mathf.Abs(best)) best = delta;
    }
    private static readonly Vector3[] corners = new Vector3[4];
    private static void GetScreenRect(RectTransform rt, out float left, out float right, out float bottom, out float top) {
        rt.GetWorldCorners(corners);
        left = corners[0].x;
        bottom = corners[0].y;
        right = corners[2].x;
        top = corners[2].y;
    }
    private static Vector2 ScreenCenter(RectTransform rt) {
        GetScreenRect(rt, out float left, out float right, out float bottom, out float top);
        return new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
    }
    private static Vector2 ScreenToVirtual(Vector2 screen) => new(
        screen.x / Mathf.Max(1f, Screen.width) * VirtualW,
        screen.y / Mathf.Max(1f, Screen.height) * VirtualH
    );
}
