using Quartz.UI.FacetUi;
using static Asserts;
static class PageRulesTests {
    public static void TestRestrictModes() {
        Assert(PageRules.RestrictModes(true).SequenceEqual(new[] { 0, 1, 2, 3, 4 }) && PageRules.RestrictModes(false).SequenceEqual(new[] { 0, 1, 3, 4 }),
            "X-Perfect only (2) is offered only with XPerfect");
        Assert(PageRules.ShownMode(2, false) == 1 && PageRules.ShownMode(2, true) == 2 && PageRules.ShownMode(3, false) == 3,
            "a stored 2 shows as 1 without XPerfect");
    }
    public static void TestDeafenModes() {
        Assert(PageRules.DeafenModes(true, true).SequenceEqual(new[] { "shortcut", "bot", "discord" }), "all three");
        Assert(PageRules.DeafenModes(false, false).SequenceEqual(new[] { "bot" }) && PageRules.DeafenModes(false, true).SequenceEqual(new[] { "bot", "discord" }),
            "bot always, the others where they work");
    }
    public static void TestSplitChord() {
        Assert(PageRules.SplitChord("Ctrl + F8", out var m, out var k) && m == "Ctrl" && k == "F8", "modifier and key");
        Assert(PageRules.SplitChord("F8", out m, out k) && m == "" && k == "F8", "a bare key");
        Assert(PageRules.SplitChord("Ctrl + Keypad +", out m, out k) && m == "Ctrl" && k == "Keypad +", "the first plus splits");
        Assert(!PageRules.SplitChord("", out _, out _) && !PageRules.SplitChord(null, out _, out _), "nothing to split");
    }
    public static void TestStaysClassic() {
        Assert(PageRules.StaysClassic("credits.main", false, false) != null, "credits stays classic");
        Assert(PageRules.StaysClassic("addon.foo.BAR", true, false) != null, "a custom addon page stays classic");
        Assert(PageRules.StaysClassic("addon.foo.SETTINGS", true, true) == null, "an addon settings tab does not");
        Assert(PageRules.StaysClassic("profiles.main", false, false) == null, "a core page does not");
        Assert(PageRules.StaysClassic("overlay.panels", false, false) == null, "a feature's page with no settings tab does not");
    }
}
