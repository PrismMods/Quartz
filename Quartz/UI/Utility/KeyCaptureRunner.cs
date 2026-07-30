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
    private void Update() {
        if(IsListening == null || !IsListening()) {
            wasListening = false;
            return;
        }
        bool hookRAlt = Quartz.Game.HookKeys.Held(KeyCode.RightAlt);
        bool hookRCtrl = Quartz.Game.HookKeys.Held(KeyCode.RightControl);
        bool firstListeningFrame = !wasListening;
        wasListening = true;
        bool rAltEdge = !firstListeningFrame && hookRAlt && !prevHookRAlt;
        bool rCtrlEdge = !firstListeningFrame && hookRCtrl && !prevHookRCtrl;
        prevHookRAlt = hookRAlt;
        prevHookRCtrl = hookRCtrl;
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
}
