using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using KeyLimiterFeature = Quartz.Features.KeyLimiter.KeyLimiter;
namespace Quartz.UI.Editor;
/// <summary>
/// Pointer + tick pump for a <see cref="KvCanvas"/>.
///
/// Movement is polled rather than taken from IBeginDragHandler because Unity gates that on
/// EventSystem.pixelDragThreshold (10), which would silently override DM Note's threshold of 5.
/// </summary>
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
    // Unity's Input never reports Shift/Ctrl/Alt off Windows (see KeyLimiter.IsHookOnlyKey), so
    // the SkyHook physical state is consulted as a fallback. That fallback is best-effort: it
    // only carries keys while the game's hook is delivering events, it has no entry for Cmd at
    // all, and on macOS it drops a key it believes released about a second into a hold. Every
    // gesture below is therefore an enhancement over one that works without a modifier: where
    // these decide a view control, an unreadable modifier must degrade to the useful default
    // rather than lock the control away. See HandleWheel.
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
            // The EventSystem drops OnPointerUp if the surface is disabled mid-gesture.
            if(!Input.GetMouseButton(0)) PointerUp();
        }
        // Zoom follows the pointer, the way the wheel does; everything else needs focus.
        //
        // Gating zoom on focus made it unreachable in practice: focus is only taken by clicking the
        // canvas, nothing advertises that, and clicking a toolbar zoom button CLEARS it — so the
        // keys did nothing until the user happened to click the canvas first, and stopped again the
        // moment they used a button. Destructive keys keep the focus gate, since hovering the canvas
        // must not arm Delete.
        if(PointerOverViewport() || focused) HandleZoomKeys();
        if(focused) HandleKeyboard();
    }
    /// <summary>
    /// A bare scroll pans, as in DM Note. This is not a preference: a trackpad's two-finger scroll
    /// is the only navigation gesture it has, so binding the bare wheel to zoom leaves a trackpad
    /// with no way to pan at all.
    ///
    /// Zoom therefore cannot be moved behind ctrl/cmd, which is where DM Note keeps it — that
    /// modifier is unreadable on macOS (see the note above), so the binding would simply not exist
    /// there. The toolbar's zoom buttons and the +/-/0 keys are the paths that always work; this is
    /// an alias for them, taken only where the modifier happens to be readable.
    /// </summary>
    private void HandleWheel(Vector2 wheel) {
        if(CtrlOrCmdHeld()) {
            // A sideways trackpad swipe carries no zoom direction, and acting on its sign would
            // zoom out on it.
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
        // Selecting an unselected element on press (rather than on release) is what lets the
        // same gesture grab and drag it. A press on an already-selected element defers to
        // release so ctrl-click can toggle it without breaking a multi-selection drag.
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
