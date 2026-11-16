using Quartz.UI.FacetUi;
using static Asserts;
static class PageRulesTests {
    public static void TestRestrictModes() {
        Assert(PageRules.RestrictModes(true).SequenceEqual(new[] { 0, 1, 2, 3, 4 }) && PageRules.RestrictModes(false).SequenceEqual(new[] { 0, 1, 3, 4 }),
            "X-Perfect only (2) is offered only with XPerfect");
        Assert(PageRules.ShownMode(2, false) == 1 && PageRules.ShownMode(2, true) == 2 && PageRules.ShownMode(3, false) == 3,
            "a stored 2 shows as 1 without XPerfect");
    }
    public static void TestBarBits() {
        Assert(PageRules.BarBits(false, false).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6 }) && PageRules.BarBits(false, true).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6 }),
            "without X, Perfect sits between the early and late perfects");
        Assert(PageRules.BarBits(true, true).SequenceEqual(new[] { 0, 1, 2, 14, 12, 13, 4, 5, 6 }), "the game's X: − Perfect is early");
        Assert(PageRules.BarBits(true, false).SequenceEqual(new[] { 0, 1, 2, 13, 12, 14, 4, 5, 6 }), "the XPerfect mod: + Perfect is early");
    }
    public static void TestRestrictionBlocked() {
        Assert(Enumerable.Range(0, 14).All(b => !PageRules.Blocked(0, 0, b)), "accuracy mode blocks nothing");
        Assert(!PageRules.Blocked(1, 0, 3) && PageRules.Blocked(1, 0, 2) && PageRules.Blocked(1, 0, 8) && PageRules.Blocked(1, 0, 12),
            "pure perfect: only Perfect passes (12 is a failed floor as a hit)");
        Assert(!PageRules.Blocked(2, 0, 3) && !PageRules.Blocked(2, 0, 12) && PageRules.Blocked(2, 0, 13) && PageRules.Blocked(2, 0, 14) && PageRules.Blocked(2, 0, 0),
            "x pure perfect: Perfect and X pass, the ± perfects do not");
        int mask = 1 << 3 | 1 << 8;
        Assert(!PageRules.Blocked(3, mask, 3) && !PageRules.Blocked(3, mask, 8) && PageRules.Blocked(3, mask, 2), "custom: the mask's bits pass");
        Assert(Enumerable.Range(0, 14).All(b => !PageRules.Blocked(3, 0, b)), "custom with mask 0 blocks nothing");
        Assert(PageRules.Blocked(4, 0, 0) && Enumerable.Range(1, 13).All(b => !PageRules.Blocked(4, 0, b)), "no too early: Too Early only");
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
