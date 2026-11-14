using Quartz.UI.FacetUi;
using static Asserts;
static class ShellModelTests {
    static bool Near(float a, float b) => MathF.Abs(a - b) <= 0.01f;
    public static void TestRailGroups() {
        Assert(ShellModel.RailGroup("home") == 0 && ShellModel.RailGroup("features") == 0, "home and features lead");
        Assert(ShellModel.RailGroup("overlay") == 1 && ShellModel.RailGroup("tuf") == 1 && ShellModel.RailGroup("myaddon") == 1, "feature and addon categories in the middle");
        Assert(ShellModel.RailGroup("settings") == 2 && ShellModel.RailGroup("developer") == 2, "app pages last");
        Assert(ShellModel.RailGroup("credits") == -1 && ShellModel.RailGroup(null) == -1, "credits leaves the rail");
    }
    public static void TestSwitches() {
        var known = new HashSet<string> { "keyviewer", "combo", "keylimiter", "effectremover", "progressbar", "ottoicon" };
        Assert(ShellModel.ParseSwitches(null, known).SequenceEqual(new[] { "keyviewer", "combo", "keylimiter", "effectremover", "progressbar" }), "missing: the defaults that exist");
        Assert(ShellModel.ParseSwitches("", known).Count == 0, "empty: none chosen");
        Assert(ShellModel.ParseSwitches(" ottoicon,nope,combo,ottoicon ", known).SequenceEqual(new[] { "ottoicon", "combo" }), "unknown and repeats drop, order kept");
        Assert(ShellModel.ParseSwitches(" ", known).Count == 0 && ShellModel.ParseSwitches(",", known).Count == 0, "blank or bare commas: none");
        Assert(ShellModel.ParseSwitches("Combo", known).Count == 0, "ids are case-sensitive");
        Assert(ShellModel.ParseSwitches("combo,combo", known).SequenceEqual(new[] { "combo" }), "a repeat counts once");
        Assert(ShellModel.FormatSwitches(new[] { "a", "b" }) == "a,b", "comma-joined");
    }
    public static void TestInitialRectAndLayout() {
        var r = ShellModel.InitialRect(1920, 1080);
        Assert(Near(r.X, 192f) && Near(r.Y, 108f) && Near(r.W, 1536f) && Near(r.H, 864f), "80% of the panel, centred");
        Assert(ShellModel.HasLayout("combo") && ShellModel.HasLayout("panels") && !ShellModel.HasLayout("effectremover"), "on-screen features only");
        Assert(new[] { "keyviewer", "combo", "judgement", "songtitle", "progressbar", "practice", "calibration", "panels" }.All(ShellModel.HasLayout),
            "every feature with a layout target");
        Assert(!ShellModel.HasLayout("keylimiter"), "a feature with nothing on screen");
    }
}
