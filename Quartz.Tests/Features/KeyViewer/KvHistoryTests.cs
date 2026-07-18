using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static class KvHistoryTests {
    public static void TestUndoRedoWalksBothWays() {
        KvHistory h = new();
        Assert(!h.CanUndo, "a fresh history has nothing to undo");
        Assert(h.Undo("a") == null, "undoing an empty history is a no-op, not a crash");
        h.Push("a");
        h.Push("b");
        Assert(h.Undo("c") == "b", "undo returns the most recent snapshot");
        Assert(h.Undo("b") == "a", "undo walks back");
        Assert(!h.CanUndo, "history is exhausted");
        Assert(h.Redo("a") == "b", "redo walks forward again");
        Assert(h.Redo("b") == "c", "redo restores the state undo banked");
        Assert(!h.CanRedo, "redo is exhausted");
    }
    public static void TestEditAfterUndoDiscardsRedo() {
        KvHistory h = new();
        h.Push("a");
        h.Undo("b");
        Assert(h.CanRedo, "undo banked a redo");
        h.Push("c");
        Assert(!h.CanRedo, "a new edit branches, discarding the redo stack");
    }
    public static void TestDepthDropsOldestNotNewest() {
        KvHistory h = new(maxDepth: 3);
        h.Push("1");
        h.Push("2");
        h.Push("3");
        h.Push("4");
        Assert(h.UndoDepth == 3, "history is bounded");
        Assert(h.Undo("5") == "4", "the newest entry survives overflow");
        Assert(h.Undo("4") == "3", "");
        Assert(h.Undo("3") == "2", "the oldest entry was the one dropped");
        Assert(!h.CanUndo, "nothing older remains");
    }
    public static void TestHeldNudgeCoalescesToOneStep() {
        // A held arrow key fires every frame; without coalescing it would bury the undo stack.
        KvHistory h = new();
        h.PushNudge("a", 0f);
        h.PushNudge("b", 0.1f);
        h.PushNudge("c", 0.4f);
        Assert(h.UndoDepth == 1, "nudges inside the window collapse into one entry");
        h.PushNudge("d", 1.0f);
        Assert(h.UndoDepth == 2, "a nudge past the window starts a new entry");
        h.EndNudge();
        h.PushNudge("e", 1.1f);
        Assert(h.UndoDepth == 3, "releasing the key breaks coalescing immediately");
    }
}
