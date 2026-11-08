using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.Utility;
public sealed class KeyCaptureRunner : MonoBehaviour {
    public Func<bool> IsListening;
    public Func<bool> ShouldCancel;
    public Action<KeyCode> OnCaptured;
    public Action OnCancelled;
    private static readonly KeyCode[] allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    // Unity never reports Caps Lock on macOS; the hook does, as a press per toggle.
    private static int hookCapsPresses;
    static KeyCaptureRunner() {
        if(!Quartz.Game.HookInput.IsMacOSRuntime) return;
        Quartz.Game.HookKeys.KeyEvent += static (key, down) => {
            if(down && key == KeyCode.CapsLock) System.Threading.Interlocked.Increment(ref hookCapsPresses);
        };
    }
    private int prevHookCaps;
    private bool prevHookRAlt;
    private bool prevHookRCtrl;
    private bool wasListening;
    private Action poll;
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
        // Past PrismLib's input gate: an open blocking window (the new menu, a Prism window) hides every key from code outside it.
        Quartz.Prism.PrismBridge.Exempt(poll ??= Poll);
    }
    private void Poll() {
        // First, every frame, whatever ends this one: each key's edge must be current when Option goes down.
        KeyCode chord = Keybind.IsMac ? MacOptionChord() : KeyCode.None;
        bool hookRAlt = Quartz.Game.HookKeys.Held(KeyCode.RightAlt);
        bool hookRCtrl = Quartz.Game.HookKeys.Held(KeyCode.RightControl);
        bool firstListeningFrame = !wasListening;
        wasListening = true;
        bool rAltEdge = !firstListeningFrame && hookRAlt && !prevHookRAlt;
        bool rCtrlEdge = !firstListeningFrame && hookRCtrl && !prevHookRCtrl;
        prevHookRAlt = hookRAlt;
        prevHookRCtrl = hookRCtrl;
        int hookCaps = System.Threading.Volatile.Read(ref hookCapsPresses);
        bool capsEdge = !firstListeningFrame && hookCaps != prevHookCaps;
        prevHookCaps = hookCaps;
        bool escape = Input.GetKeyDown(KeyCode.Escape);
        // Spent here: the window being bound in stays open when another mod offers this Escape to Facet afterwards.
        if(escape) Quartz.UI.FacetUi.FacetBridge.ConsumeEscape();
        if(escape || (ShouldCancel?.Invoke() ?? false)) {
            CancelCapture();
            return;
        }
        if(rCtrlEdge) {
            CompleteCapture(KeyCode.RightControl);
            return;
        }
        if(capsEdge) {
            CompleteCapture(KeyCode.CapsLock);
            return;
        }
        if(rAltEdge) {
            CompleteCapture(KeyCode.RightAlt);
            return;
        }
        if(chord != KeyCode.None) {
            CompleteCapture(chord);
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
    // macOS: under Option, Unity reports no letter's keydown (anyKeyDown shows only Option), so the chord's key is read through
    // Keybind, from the window server, as the classic capture does. Every key is read every frame, Option or not, so each
    // key's edge is current when Option goes down.
    private static KeyCode MacOptionChord() {
        KeyCode down = KeyCode.None;
        foreach(KeyCode key in allKeys)
            if(key != KeyCode.None && key < KeyCode.Mouse0 && !Keybind.IsModifier(key) && Keybind.KeyDown(key) && down == KeyCode.None) down = key;
        return Keybind.ModifierHeld(Keybind.KeyModifier.Alt) ? down : KeyCode.None;
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
    private static int releasedFrame = -1;
    /// <summary>A capture ended this frame: the key it took is still down, and must not also act as a hotkey.</summary>
    public static bool ReleasedThisFrame => releasedFrame == Time.frameCount;
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
        releasedFrame = Time.frameCount;
    }
}
