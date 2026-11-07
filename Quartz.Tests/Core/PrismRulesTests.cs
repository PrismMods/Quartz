using System.Text.Json;
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
    public static void TestLayoutSession() {
        var s = new PrismRules.LayoutSession();
        Assert(!s.Open(0), "closed before any read");
        s.Touch(10);
        Assert(s.Open(10) && s.Open(12), "open on the read frame and for Grace frames after");
        Assert(!s.Open(13), "closed once Grace frames pass with no read");
    }
    public static void TestPrismMods() {
        Assert(PrismRules.ToPrismMods(0) == 0 && PrismRules.ToPrismMods(1) == 2 && PrismRules.ToPrismMods(2) == 4
            && PrismRules.ToPrismMods(3) == 1 && PrismRules.ToPrismMods(4) == 8, "Keybind None/Ctrl/Alt/Shift/Cmd -> PrismLib KeyMods bits");
    }
    public static void TestHotkeyRefusals() {
        const int None = 0, Ctrl = 1, Alt = 2;
        Assert(!PrismRules.HotkeyRefused(Alt, 107), "Alt+K is Quartz's default and allowed");
        foreach(int k in new[] { 100, 112, 116, 99 }) Assert(PrismRules.HotkeyRefused(Alt, k), "Alt+D/P/T/C are Prism's chords");
        Assert(!PrismRules.HotkeyRefused(Ctrl, 100) && !PrismRules.HotkeyRefused(None, 100), "D without Alt is not a Prism chord");
        foreach(int k in new[] { 0, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 323, 329 })
            Assert(PrismRules.HotkeyRefused(Alt, k), "no key, a modifier key or a mouse button is never a hotkey");
    }
    public static void TestPrismWordsMatchLang() {
        string lang = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../")), "Quartz", "Resource", "Export", "Lang");
        using JsonDocument en = JsonDocument.Parse(File.ReadAllText(Path.Combine(lang, "en-US.json")));
        using JsonDocument ko = JsonDocument.Parse(File.ReadAllText(Path.Combine(lang, "ko-KR.json")));
        JsonElement enBlock = en.RootElement.GetProperty("en-US"), koBlock = ko.RootElement.GetProperty("ko-KR");
        foreach(var (english, key) in PrismRules.EnglishToKey) {
            Assert(enBlock.GetProperty(key).GetString() == english, $"{key}: en-US value is the English the bridge hands PrismLib");
            Assert(!string.IsNullOrEmpty(koBlock.GetProperty(key).GetString()), $"{key}: authored in ko-KR");
        }
        foreach(string key in new[] { "PRISM_KEY_MENU", "PRISM_DEBUG_LOG", "PRISM_DEBUG_STATE" }) {
            Assert(enBlock.TryGetProperty(key, out _) && koBlock.TryGetProperty(key, out _), $"{key}: in both files");
        }
        Assert(PrismRules.Translate("Menu hotkey", (k, d) => k) == "PRISM_MENU_HOTKEY" && PrismRules.Translate("Undo", (k, d) => k) == null,
            "known English maps to its key; anything else is not ours");
    }
}
