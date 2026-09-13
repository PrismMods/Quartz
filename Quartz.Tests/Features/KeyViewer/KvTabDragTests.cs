using Quartz.UI.Editor;
using static Asserts;

static class KvTabDragTests {
    public static void TestClickAndScrollStaySeparateFromReorder() {
        KvTabDragState drag = new();

        drag.PointerDown(primary: true);
        Assert(drag.TryPick(primary: true), "a primary click still selects its tab");

        drag.PointerDown(primary: true);
        Assert(!drag.Begin("only", 0, 1, primary: true), "a one-tab strip leaves dragging to its scroll view");
        Assert(!drag.TryPick(primary: true), "a scroll drag cannot turn into a tab selection on release");

        drag.PointerDown(primary: true);
        Assert(drag.Begin("b", 1, 3, primary: true), "a populated strip starts tab reordering");
        Assert(drag.Reordering && drag.Tab == "b" && drag.From == 1, "the reorder remembers its source");
        Assert(!drag.TryPick(primary: true), "a reorder drag cannot turn into a tab selection on release");
        Assert(drag.End(out string tab, out int from) && tab == "b" && from == 1,
            "ending a reorder returns its tab and original slot");

        drag.PointerDown(primary: true);
        Assert(drag.TryPick(primary: true), "selection works again on the click after a drag");
        Assert(!drag.Begin("b", 1, 3, primary: false), "a non-primary drag is left to scrolling");
        Assert(!drag.TryPick(primary: false), "a non-primary release never selects a tab");
    }
}
