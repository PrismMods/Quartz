namespace Quartz.UI.Utility;
// One Ctrl+Z order across reorganize moves and KeyViewer edits. A step returns false when its target is gone; it is dropped and the next one runs.
internal static class UndoTimeline {
    internal const int Limit = 200;
    private sealed class Step(Func<bool> undo, Func<bool> redo) {
        internal readonly Func<bool> Undo = undo;
        internal readonly Func<bool> Redo = redo;
    }
    private static readonly List<Step> past = [];
    private static readonly List<Step> future = [];
    internal static int UndoCount => past.Count;
    internal static int RedoCount => future.Count;
    internal static void Push(Func<bool> undo, Func<bool> redo) {
        past.Add(new Step(undo, redo));
        if(past.Count > Limit) past.RemoveAt(0);
        future.Clear();
    }
    internal static bool Undo() => Move(past, future, true);
    internal static bool Redo() => Move(future, past, false);
    private static bool Move(List<Step> from, List<Step> to, bool back) {
        while(from.Count > 0) {
            Step step = from[^1];
            from.RemoveAt(from.Count - 1);
            if(!(back ? step.Undo() : step.Redo())) continue;
            to.Add(step);
            return true;
        }
        return false;
    }
    internal static void Clear() {
        past.Clear();
        future.Clear();
    }
}
