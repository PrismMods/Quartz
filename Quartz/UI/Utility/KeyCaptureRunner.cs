using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.Utility;
public sealed class KeyCaptureRunner : MonoBehaviour {
    public Func<bool> IsListening;
    public Func<bool> ShouldCancel;
    public Action<KeyCode> OnCaptured;
    public Action OnCancelled;
    private static readonly KeyCode[] allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    private bool prevHookRAlt;
    private bool prevHookRCtrl;
    private bool wasListening;
    public bool BeginCapture() {
        if(IsListening == null || !IsListening()) {
            EndCapture();
            return false;
        }
        if(KeyCaptureCoordinator.Owns(this)) return true;
        wasListening = false;
        return KeyCaptureCoordinator.Claim(this, CancelForTakeover);
    }
    public void EndCapture() {
        KeyCaptureCoordinator.Release(this);
        wasListening = false;
    }
    private void Update() {
        if(IsListening == null || !IsListening()) {
            EndCapture();
            return;
        }
        if(!KeyCaptureCoordinator.Owns(this) && !BeginCapture()) return;
        bool hookRAlt = Quartz.Game.HookKeys.Held(KeyCode.RightAlt);
        bool hookRCtrl = Quartz.Game.HookKeys.Held(KeyCode.RightControl);
        bool firstListeningFrame = !wasListening;
        wasListening = true;
        bool rAltEdge = !firstListeningFrame && hookRAlt && !prevHookRAlt;
        bool rCtrlEdge = !firstListeningFrame && hookRCtrl && !prevHookRCtrl;
        prevHookRAlt = hookRAlt;
        prevHookRCtrl = hookRCtrl;
        if(Input.GetKeyDown(KeyCode.Escape) || (ShouldCancel?.Invoke() ?? false)) {
            CancelCapture();
            return;
        }
        if(rCtrlEdge) {
            CompleteCapture(KeyCode.RightControl);
            return;
        }
        if(rAltEdge) {
            CompleteCapture(KeyCode.RightAlt);
            return;
        }
        if(!Input.anyKeyDown) return;
        if(Input.GetKeyDown(KeyCode.KeypadEnter)) {
            CompleteCapture(KeyCode.KeypadEnter);
            return;
        }
        foreach(KeyCode key in allKeys) {
            if(key == KeyCode.None || (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)) continue;
            if(Input.GetKeyDown(key)) {
                CompleteCapture(key);
                return;
            }
        }
    }
    private void CompleteCapture(KeyCode key) {
        if(!KeyCaptureCoordinator.Owns(this)) return;
        EndCapture();
        OnCaptured?.Invoke(key);
    }
    private void CancelCapture() {
        if(!KeyCaptureCoordinator.Owns(this)) return;
        EndCapture();
        OnCancelled?.Invoke();
    }
    private void CancelForTakeover() {
        wasListening = false;
        OnCancelled?.Invoke();
    }
    private void OnDisable() {
        if(KeyCaptureCoordinator.Owns(this)) CancelCapture();
        else wasListening = false;
    }
}
public static class KeyCaptureCoordinator {
    private static object owner;
    private static Action cancelOwner;
    public static bool Claim(object claimant, Action cancel) {
        if(claimant == null || cancel == null) return false;
        if(ReferenceEquals(owner, claimant)) {
            cancelOwner = cancel;
            Keybind.Capturing = true;
            return true;
        }
        Action previousCancel = cancelOwner;
        owner = claimant;
        cancelOwner = cancel;
        Keybind.Capturing = true;
        try {
            previousCancel?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[Input] cancelling the previous key capture threw: {e.Message}");
        }
        return ReferenceEquals(owner, claimant);
    }
    public static bool Owns(object claimant) => ReferenceEquals(owner, claimant);
    public static void Release(object claimant) {
        if(!ReferenceEquals(owner, claimant)) return;
        owner = null;
        cancelOwner = null;
        Keybind.Capturing = false;
    }
}
