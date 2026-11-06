using Quartz.Prism;
using static Asserts;
static class PrismRulesTests {
    public static void TestModsRoot() {
        string g = Path.Combine(Path.GetTempPath(), "g");
        Assert(PrismRules.ModsRoot(Path.Combine(g, "UMMMods", "Quartz") + Path.DirectorySeparatorChar, umm: true) == Path.Combine(g, "UMMMods"), "UMM: the mod folder's parent");
        Assert(PrismRules.ModsRoot(Path.Combine(g, "UMMMods", "Quartz"), umm: true) == Path.Combine(g, "UMMMods"), "UMM without a trailing separator");
        Assert(PrismRules.ModsRoot(Path.Combine(g, "Mods"), umm: false) == Path.Combine(g, "Mods"), "ML: the Mods folder itself");
    }
    public static void TestUiCopies() {
        string root = Path.Combine(Path.GetTempPath(), "quartz-uicopies-" + Guid.NewGuid().ToString("N"));
        try {
            string[] included = [
                Path.Combine(root, "UMMMods", "Topaz", "PrismLib.UI.dll"),
                Path.Combine(root, "UMMMods", "Quartz", "Runtime", "versions", "3.0.0", "PrismLib.UI.dll"),
                Path.Combine(root, "UserLibs", "PrismLib.UI.dll"),
                Path.Combine(root, "UserData", "Quartz", "Runtime", "versions", "3.0.0", "PrismLib.UI.dll"),
            ];
            string[] excluded = [
                Path.Combine(root, "Mods", "PrismLib", "PrismLib.UI.dll"),
                Path.Combine(root, "UserData", "Quartz", "PrismLib.UI.dll"),
            ];
            foreach(string f in included.Concat(excluded)) {
                Directory.CreateDirectory(Path.GetDirectoryName(f)!);
                File.WriteAllBytes(f, []);
            }
            Assert(new HashSet<string>(PrismRules.UiCopies(root)).SetEquals(included), "every copy a loader could load, nothing else");
            Assert(PrismRules.UiCopies(Path.Combine(root, "missing")).Count == 0, "a game root without those folders has no copies");
        } finally {
            Directory.Delete(root, true);
        }
    }
    public static void TestPrismMods() {
        Assert(PrismRules.ToPrismMods(0) == 0 && PrismRules.ToPrismMods(1) == 2 && PrismRules.ToPrismMods(2) == 4
            && PrismRules.ToPrismMods(3) == 1 && PrismRules.ToPrismMods(4) == 8, "Keybind None/Ctrl/Alt/Shift/Cmd -> PrismLib KeyMods bits");
    }
}
