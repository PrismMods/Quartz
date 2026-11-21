using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static class KvMouseNamesTests {
    public static void TestRoundTripKeepsEveryButton() {
        for(int b = 0; b < KvMouseNames.Count; b++) {
            string name = KvMouseNames.Name(b);
            Assert(name == "MOUSE" + (b + 1), $"Mouse{b} exports as DM Note's MOUSE{b + 1}");
            Assert(KvMouseNames.TryButton(name, out int back) && back == b, $"{name} imports as Mouse{b}");
        }
        Assert(!KvMouseNames.TryButton("Mouse5", out _), "Unity's own enum name is left to the enum parser");
        Assert(!KvMouseNames.TryButton("MOUSE8", out _), "a button past the table is not a mouse name");
        Assert(KvMouseNames.Name(KvMouseNames.Count) == "", "a button past the table has no name");
    }
}
