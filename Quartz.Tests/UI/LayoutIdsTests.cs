using Quartz.UI.Utility;
using static Asserts;
static class LayoutIdsTests {
    public static void TestOrderListsFixedIdsThenPanelsByIndex() {
        string[] ids = [LayoutIds.Panel(10), LayoutIds.Calibration, LayoutIds.Panel(2), LayoutIds.KeyViewer, LayoutIds.Combo, LayoutIds.KeyViewerFoot, LayoutIds.Tabub];
        string[] sorted = [.. ids.OrderBy(LayoutIds.Order)];
        Assert(sorted.SequenceEqual(["keyviewer", "keyviewer.foot", "keyviewer.tabub", "combo", "calibration", "panel.2", "panel.10"]),
            "fixed ids in their order, then panels by number, not by text: " + string.Join(",", sorted));
        Assert(LayoutIds.Order("panel.x") == int.MaxValue && LayoutIds.Order(null) == int.MaxValue && LayoutIds.Order("panel.-1") == int.MaxValue,
            "anything else sorts last");
    }
}
