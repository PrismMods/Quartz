using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using KeyLimiterFeature = Quartz.Features.KeyLimiter.KeyLimiter;
namespace Quartz.UI.Editor;
internal sealed class KvCanvasDriver : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    internal KvCanvas Owner;
    private void Update() => Owner?.Tick();
    private void OnDestroy() {
        Owner?.OnDriverDestroyed();
        Owner = null;
    }
    public void OnPointerDown(PointerEventData eventData) {
        if(eventData.button == PointerEventData.InputButton.Left) Owner?.PointerDown(eventData.position);
        else Owner?.PanDown(eventData.position);
    }
    public void OnPointerUp(PointerEventData eventData) {
        if(eventData.button == PointerEventData.InputButton.Left) Owner?.PointerUp();
        else Owner?.PanUp();
    }
}
internal sealed partial class KvCanvas {
    private enum Gesture { None, Pending, Drag, Marquee, Resize }
    private const float DoubleClickSeconds = 0.3f;
    private Gesture gesture = Gesture.None;
    private bool pointerDown;
    private bool focused;
    private Vector2 pressOverlay;
    private Vector2 pressLayout;
    private KvElement pressElement;
    private bool pressAdditive;
    private bool pressSelectedOnDown;
    private int pressHandle = -1;
    private KvElement lastClickEl;
    private float lastClickAt;
    private readonly List<KvElement> selectionAtPress = [];
    private static bool ModifierOrHook(Keybind.KeyModifier mod, KeyCode a, KeyCode b) =>
        Keybind.ModifierHeld(mod) || KeyLimiterFeature.HookKeyHeld(a) || KeyLimiterFeature.HookKeyHeld(b);
    private static bool ShiftHeld() =>
        ModifierOrHook(Keybind.KeyModifier.Shift, KeyCode.LeftShift, KeyCode.RightShift);
    private static bool AltHeld() =>
        ModifierOrHook(Keybind.KeyModifier.Alt, KeyCode.LeftAlt, KeyCode.RightAlt);
    private static bool CtrlOrCmdHeld() =>
        Keybind.ModifierHeld(Keybind.KeyModifier.Cmd)
        || ModifierOrHook(Keybind.KeyModifier.Ctrl, KeyCode.LeftControl, KeyCode.RightControl);
    private bool needsCentre;
    internal void Tick() {
        if(root == null) return;
        if(needsCentre && ViewportReady()) {
            needsCentre = false;
            CentreView();
        }
        Vector2 wheel = Input.mouseScrollDelta;
        if(KvSnap.WheelMoved(wheel.x, wheel.y) && PointerOverViewport()) HandleWheel(wheel);
        if(Input.GetMouseButtonDown(0) && !PointerOverViewport()) focused = false;
        UpdatePan();
        if(pointerDown) {
            UpdateGesture();
            if(!Input.GetMouseButton(0)) PointerUp();
        }
        if(PointerOverViewport() || focused) HandleZoomKeys();
        if(focused) HandleKeyboard();
    }
    private void HandleWheel(Vector2 wheel) {
        if(CtrlOrCmdHeld()) {
            if(Mathf.Abs(wheel.y) > KvSnap.WheelDeadzone)
                ZoomAt(Input.mousePosition, KvSnap.ZoomStepFor(wheel.y));
            return;
        }
        (float x, float y) = KvSnap.WheelPan(wheel.x, wheel.y, ShiftHeld(), KvSnap.WheelPanSpeed);
        PanBy(new Vector2(x, y));
    }
    internal void PointerDown(Vector2 screen) {
        if(doc == null) return;
        focused = true;
        pointerDown = true;
        gesture = Gesture.Pending;
        pressOverlay = ScreenToOverlay(screen);
        pressLayout = ScreenToLayout(screen);
        pressAdditive = CtrlOrCmdHeld() || ShiftHeld();
        pressSelectedOnDown = false;
        selectionAtPress.Clear();
        selectionAtPress.AddRange(selection);
        pressHandle = HandleHit(pressOverlay);
        if(pressHandle >= 0) {
            pressElement = null;
            return;
        }
        pressElement = HitTest(pressLayout);
        if(pressElement != null && !selection.Contains(pressElement)) {
            Select(pressElement, pressAdditive);
            pressSelectedOnDown = true;
        }
    }
    private void UpdateGesture() {
        Vector2 screen = Input.mousePosition;
        if(gesture == Gesture.Pending) {
            if((ScreenToOverlay(screen) - pressOverlay).magnitude < KvSnap.DragThreshold) return;
            if(pressHandle >= 0) {
                gesture = Gesture.Resize;
                BeginResize(pressHandle);
            } else if(pressElement != null) {
                gesture = Gesture.Drag;
                BeginDrag();
            } else {
                gesture = Gesture.Marquee;
            }
        }
        Vector2 layout = ScreenToLayout(screen);
        switch(gesture) {
            case Gesture.Resize:
                UpdateResize(layout);
                break;
            case Gesture.Drag:
                UpdateDrag(layout);
                break;
            case Gesture.Marquee:
                UpdateMarquee(layout);
                break;
        }
    }
    internal void PointerUp() {
        if(!pointerDown) return;
        pointerDown = false;
        switch(gesture) {
            case Gesture.Resize:
                EndResize();
                break;
            case Gesture.Drag:
                EndDrag();
                break;
            case Gesture.Marquee:
                HideMarquee();
                break;
            case Gesture.Pending:
                Click();
                break;
        }
        gesture = Gesture.None;
        pressHandle = -1;
    }
    private void Click() {
        if(!pressSelectedOnDown) {
            if(pressElement != null) Select(pressElement, pressAdditive);
            else if(!pressAdditive) ClearSelection();
        }
        if(pressElement == null) {
            lastClickEl = null;
            return;
        }
        float now = Time.unscaledTime;
        if(pressElement == lastClickEl && now - lastClickAt <= DoubleClickSeconds) {
            lastClickEl = null;
            ElementActivated?.Invoke(pressElement);
            return;
        }
        lastClickEl = pressElement;
        lastClickAt = now;
    }
}
