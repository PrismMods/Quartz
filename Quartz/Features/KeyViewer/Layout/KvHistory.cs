namespace Quartz.Features.KeyViewer.Layout;
/// <summary>
/// Undo/redo for the layout editor.
///
/// A snapshot is the serialized document. That is deliberately coarse — DM Note does the same
/// (`JSON.parse(JSON.stringify(...))` per step, no diffing) and it is what makes undo correct
/// for free: every element, every unmodelled passthrough field, and the z-order all rewind
/// together, with no per-operation inverse to get wrong.
///
/// Depth matches DM Note's MAX_HISTORY_SIZE.
/// </summary>
internal sealed class KvHistory(int maxDepth = KvHistory.DefaultDepth) {
    internal const int DefaultDepth = 50;
    /// <summary>
    /// Coalescing window for repeated nudges. DM Note pushes at most one entry per 500ms of
    /// held arrow key, so a held key is one undo step rather than fifty.
    /// </summary>
    internal const float NudgeCoalesceSeconds = 0.5f;
    private readonly List<string> past = [];
    private readonly List<string> future = [];
    private readonly int depth = Math.Max(1, maxDepth);
    private float lastNudgeAt = float.NegativeInfinity;
    internal bool CanUndo => past.Count > 0;
    internal bool CanRedo => future.Count > 0;
    internal int UndoDepth => past.Count;
    /// <summary>
    /// Record <paramref name="snapshot"/> as the state to return to. Call BEFORE mutating.
    /// Any redo stack is discarded, matching every editor's branch-on-edit behaviour.
    /// </summary>
    internal void Push(string snapshot) {
        if(snapshot == null) return;
        past.Add(snapshot);
        // Overflow drops the oldest entry, so the depth bound is on memory, not on recency.
        if(past.Count > depth) past.RemoveAt(0);
        future.Clear();
    }
    /// <summary>
    /// Push unless a nudge was already recorded within <see cref="NudgeCoalesceSeconds"/>.
    /// <paramref name="now"/> is a monotonic seconds clock (Time.unscaledTime at the call site).
    /// </summary>
    internal void PushNudge(string snapshot, float now) {
        if(now - lastNudgeAt <= NudgeCoalesceSeconds) return;
        lastNudgeAt = now;
        Push(snapshot);
    }
    /// <summary>Breaks nudge coalescing, so the next arrow press starts a fresh undo step.</summary>
    internal void EndNudge() => lastNudgeAt = float.NegativeInfinity;
    /// <summary>
    /// The snapshot to restore, with <paramref name="current"/> banked for redo.
    /// Null when there is nothing to undo.
    /// </summary>
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
