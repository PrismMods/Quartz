using Quartz.Core;
using Quartz.Core.Service;
using static Asserts;
static class PathServiceTests {
    public static void TestEveryPathStaysUnderTheRoot() {
        string root = Path.Combine(Path.GetTempPath(), "quartz-paths-" + Guid.NewGuid().ToString("N"));
        PathService paths = new(root);
        string[] all = [
            paths.ConfigPath, paths.LangPath, paths.TempPath, paths.ModulePath, paths.FontPath,
            paths.CustomFontPath, paths.AddonsPath, paths.TufPath, paths.TufLevelsPath, paths.UserResourcePath,
        ];
        string prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        foreach(string path in all)
            Assert(Path.GetFullPath(path).StartsWith(prefix, StringComparison.Ordinal),
                $"{path} must live under the data root");
        Assert(paths.RootPath == root, "the root is kept verbatim");
        Assert(Path.GetFullPath(paths.TufLevelsPath).StartsWith(Path.GetFullPath(paths.TufPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal),
            "levels live under the TUF folder");
        Assert(all.Distinct(StringComparer.Ordinal).Count() == all.Length, "no two paths collide");
    }
    public static void TestInitializeCreatesTheFoldersButNoFiles() {
        string root = Path.Combine(Path.GetTempPath(), "quartz-paths-" + Guid.NewGuid().ToString("N"));
        PathService paths = new(root);
        try {
            paths.Initialize();
            foreach(string directory in new[] {
                paths.RootPath, paths.LangPath, paths.TempPath, paths.ModulePath,
                paths.FontPath, paths.CustomFontPath, paths.AddonsPath, paths.TufLevelsPath,
            }) Assert(Directory.Exists(directory), $"{directory} created");
            Assert(!File.Exists(paths.ConfigPath), "Initialize never writes a settings file");
            Assert(!File.Exists(paths.UserResourcePath), "Initialize never writes a user-resource file");
            paths.Initialize();
            Assert(Directory.Exists(paths.LangPath), "a second Initialize is harmless");
        } finally {
            if(Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
static class FeatureRegistryTests {
    public static void TestEnableAndDisableRunEveryRegisteredStepInOrder() {
        FeatureRegistry registry = new();
        List<string> log = [];
        registry.Register("first", () => log.Add("on:first"), () => log.Add("off:first"));
        registry.Register("second", () => log.Add("on:second"), () => log.Add("off:second"));
        registry.OnEnable("third", () => log.Add("on:third"));
        registry.OnDisable("fourth", () => log.Add("off:fourth"));
        registry.EnableAll();
        Assert(log.SequenceEqual(["on:first", "on:second", "on:third"]), "enable runs in registration order");
        log.Clear();
        registry.DisableAll();
        Assert(log.SequenceEqual(["off:first", "off:second", "off:fourth"]), "disable runs in registration order");
    }
    public static void TestNullStepsAreDroppedRatherThanInvoked() {
        FeatureRegistry registry = new();
        int enabled = 0, disabled = 0;
        registry.Register("enable only", () => enabled++, null);
        registry.Register("disable only", null, () => disabled++);
        registry.EnableAll();
        registry.DisableAll();
        Assert(enabled == 1, "the enable-only feature enabled once");
        Assert(disabled == 1, "the disable-only feature disabled once");
    }
    public static void TestAnEmptyRegistryIsSafeToDrive() {
        FeatureRegistry registry = new();
        registry.EnableAll();
        registry.DisableAll();
        Assert(true, "driving an empty registry does not throw");
    }
}
