using Quartz.UI.Utility;
using static Asserts;
static class UndoTimelineTests {
    public static void TestInterleavedStepsUndoInOrderAndSkipDead() {
        UndoTimeline.Clear();
        List<string> log = [];
        bool aliveA = true;
        UndoTimeline.Push(() => { if(!aliveA) return false; log.Add("-a"); return true; }, () => { log.Add("+a"); return true; });
        UndoTimeline.Push(() => { log.Add("-b"); return true; }, () => { log.Add("+b"); return true; });
        Assert(UndoTimeline.Undo() && log[^1] == "-b", "undo runs the newest step first");
        Assert(UndoTimeline.Redo() && log[^1] == "+b", "redo replays it");
        UndoTimeline.Undo();
        aliveA = false;
        Assert(!UndoTimeline.Undo(), "a dead step is dropped and nothing else is left");
        Assert(UndoTimeline.UndoCount == 0 && UndoTimeline.RedoCount == 1, "the dead step is gone, the live redo stays");
        UndoTimeline.Push(() => true, () => true);
        Assert(UndoTimeline.RedoCount == 0, "a new edit discards redo");
        UndoTimeline.Clear();
    }
}
