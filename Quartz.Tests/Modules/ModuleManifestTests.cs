using Quartz.Modules;
using static Asserts;
static class ModuleManifestTests {
    private const string Good = """
        { "schema": 1, "id": "keyviewer", "entry": "Quartz.Features.KeyViewer.KeyViewerModule",
          "name": "Key Viewer", "group": "overlay", "order": 20,
          "version": "2.0.0-alpha-90", "coreAbi": 1, "minCoreVersion": "2.0.0-alpha-88",
          "deps": ["progressbar"], "settingsFiles": ["KeyViewer.json"], "langPrefixes": ["KVI_"] }
        """;
    public static void TestManifestParsesAndValidates() {
        ModuleManifest m = ModuleManifest.Parse(Good, out string error);
        Assert(m != null && error == null, "a well-formed manifest parses: " + error);
        Assert(m.Id == "keyviewer" && m.Order == 20 && m.CoreAbi == 1, "scalar fields are read");
        Assert(m.Deps.Length == 1 && m.Deps[0] == "progressbar", "deps are read");
        Assert(m.SettingsFiles[0] == "KeyViewer.json" && m.LangPrefixes[0] == "KVI_", "string arrays are read");
        Assert(ModuleManifest.Parse(Good.Replace("\"schema\": 1", "\"schema\": 2"), out _) == null,
            "a future schema is refused rather than half-read");
        Assert(ModuleManifest.Parse(Good.Replace("\"coreAbi\": 1,", ""), out _) == null, "coreAbi is required");
        Assert(ModuleManifest.Parse(Good.Replace("\"version\": \"2.0.0-alpha-90\",", ""), out _) == null,
            "version is required");
        Assert(ModuleManifest.Parse("{ not json", out _) == null, "garbage is refused");
    }
    public static void TestManifestRejectsUnsafeIds() {
        foreach(string bad in new[] { "Key-Viewer", "../evil", "key viewer", "", "key_viewer" })
            Assert(!ModuleManifest.IsValidId(bad), $"'{bad}' is not a valid id");
        foreach(string ok in new[] { "keyviewer", "progress-bar", "tuf2" })
            Assert(ModuleManifest.IsValidId(ok), $"'{ok}' is a valid id");
        Assert(ModuleManifest.Parse(Good.Replace("\"keyviewer\"", "\"../evil\""), out _) == null,
            "a path-traversal id never reaches the loader");
        Assert(ModuleManifest.Parse(Good.Replace("[\"progressbar\"]", "[\"keyviewer\"]"), out _) == null,
            "a module cannot depend on itself");
    }
    public static void TestModuleStateRoundTrips() {
        ModuleState state = new();
        state.For("keyviewer").Enabled = true;
        state.For("keyviewer").Version = "2.0.0";
        state.For("combo").Enabled = false;
        state.PendingInstall.Add("tuf");
        ModuleState back = ModuleState.Parse(state.ToJson());
        Assert(back.Modules["keyviewer"].Enabled && back.Modules["keyviewer"].Version == "2.0.0", "entries round-trip");
        Assert(!back.Modules["combo"].Enabled, "a disabled module stays disabled");
        Assert(back.PendingInstall.Count == 1 && back.PendingInstall[0] == "tuf", "pending installs round-trip");
        state.Categories["overlay"] = false;
        state.Migrated = true;
        ModuleState again = ModuleState.Parse(state.ToJson());
        Assert(again.Categories["overlay"] == false, "a hidden tab round-trips");
        Assert(again.Migrated, "the migration marker round-trips");
        Assert(!again.Categories.ContainsKey("gameplay"), "tabs left alone are not written out, so they default to shown");
        Assert(ModuleState.Parse("{ broken").Modules.Count == 0, "a corrupt state file reads as empty, not a crash");
        Assert(!ModuleState.Parse("""{"modules":{"../evil":{"enabled":true}}}""").Modules.ContainsKey("../evil"),
            "an unsafe id in the state file is dropped");
    }
}
