using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.Utility;
public static class KeyCaptureService {
    private static KeyCaptureRunner runner;
    private static Action<KeyCode> onKey;
    private static Action onEnded;
    public static bool IsCapturing { get; private set; }
    public static void Start(Action<KeyCode> captured, Action ended) {
        Cancel();
        if(!EnsureRunner()) {
            ended?.Invoke();
            return;
        }
        onKey = captured;
        onEnded = ended;
        IsCapturing = true;
    }
    public static void Cancel() => End(KeyCode.None);
    private static void End(KeyCode key) {
        if(!IsCapturing) return;
        IsCapturing = false;
        Action<KeyCode> captured = onKey;
        Action ended = onEnded;
        onKey = null;
        onEnded = null;
        try {
            captured?.Invoke(key);
        } catch(Exception e) {
            MainCore.Log.Err($"[Input] key capture callback threw: {e.Message}");
        }
        try {
            ended?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[Input] key capture end callback threw: {e.Message}");
        }
    }
    private static bool EnsureRunner() {
        if(runner != null) return true;
        GameObject root = MainCore.Root;
        if(root == null) return false;
        runner = root.AddComponent<KeyCaptureRunner>();
        runner.IsListening = () => IsCapturing;
        runner.OnCaptured = End;
        runner.OnCancelled = Cancel;
        return true;
    }
}
