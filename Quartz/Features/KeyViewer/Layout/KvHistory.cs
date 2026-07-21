namespace Quartz.Features.KeyViewer.Layout;
internal sealed class KvHistory(int maxDepth = KvHistory.DefaultDepth) {
    internal const int DefaultDepth = 50;
    internal const float NudgeCoalesceSeconds = 0.5f;
    private readonly List<string> past = [];
    private readonly List<string> future = [];
    private readonly int depth = Math.Max(1, maxDepth);
    private float lastNudgeAt = float.NegativeInfinity;
    internal bool CanUndo => past.Count > 0;
    internal bool CanRedo => future.Count > 0;
    internal int UndoDepth => past.Count;
    internal void Push(string snapshot) {
        if(snapshot == null) return;
        past.Add(snapshot);
        if(past.Count > depth) past.RemoveAt(0);
        future.Clear();
    }
    internal void PushNudge(string snapshot, float now) {
        if(now - lastNudgeAt <= NudgeCoalesceSeconds) return;
        lastNudgeAt = now;
        Push(snapshot);
    }
    internal void EndNudge() => lastNudgeAt = float.NegativeInfinity;
    internal string Undo(string current) {
        if(past.Count == 0) return null;
        string snapshot = past[^1];
        past.RemoveAt(past.Count - 1);
        if(current != null) future.Add(current);
        return snapshot;
    }
    internal string Redo(string current) {
        if(future.Count == 0) return null;
        string snapshot = future[^1];
        future.RemoveAt(future.Count - 1);
        if(current != null) past.Add(current);
        return snapshot;
    }
    internal void Clear() {
        past.Clear();
        future.Clear();
        EndNudge();
    }
}
