using UnityEngine;
namespace Quartz.UI.Utility;
internal sealed class KeyCaptureRunner : MonoBehaviour {
    public Func<bool> IsListening;
    public Func<bool> ShouldCancel;
    public Action<KeyCode> OnCaptured;
    public Action OnCancelled;
    public Action OnDestroyed;
    private static readonly KeyCode[] allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
    private bool prevHookRAlt;
    private bool prevHookRCtrl;
    private void Update() {
        bool hookRAlt = Features.KeyLimiter.KeyLimiter.HookKeyHeld(KeyCode.RightAlt);
        bool hookRCtrl = Features.KeyLimiter.KeyLimiter.HookKeyHeld(KeyCode.RightControl);
        bool rAltEdge = hookRAlt && !prevHookRAlt;
        bool rCtrlEdge = hookRCtrl && !prevHookRCtrl;
        prevHookRAlt = hookRAlt;
        prevHookRCtrl = hookRCtrl;
        if(IsListening == null || !IsListening()) return;
        if(Input.GetKeyDown(KeyCode.Escape) || (ShouldCancel?.Invoke() ?? false)) {
            OnCancelled?.Invoke();
            return;
        }
        if(rCtrlEdge) {
            OnCaptured?.Invoke(KeyCode.RightControl);
            return;
        }
        if(rAltEdge) {
            OnCaptured?.Invoke(KeyCode.RightAlt);
            return;
        }
        if(!Input.anyKeyDown) return;
        if(Input.GetKeyDown(KeyCode.KeypadEnter)) {
            OnCaptured?.Invoke(KeyCode.KeypadEnter);
            return;
        }
        foreach(KeyCode key in allKeys) {
            if(key == KeyCode.None || (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)) continue;
            if(Input.GetKeyDown(key)) {
                OnCaptured?.Invoke(key);
                return;
            }
        }
    }
    private void OnDestroy() => OnDestroyed?.Invoke();
}
