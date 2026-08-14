using Quartz.IO;
using static Asserts;
static class ProfileNamesTests {
    public static void TestImportedModProfileNames() {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) {
            "Imported - JipperKeyViewer",
            "Imported - JipperKeyViewer (2)",
        };
        string first = ProfileNames.ImportedModName("JipperKeyViewer");
        string unique = ProfileNames.Unique(first, existing.Contains);
        Assert(first == "Imported - JipperKeyViewer", "import base profile name");
        Assert(unique == "Imported - JipperKeyViewer (3)", "import profile name uniquified");
        Assert(ProfileNames.ImportedModName("<b>Bad/Name?</b>") == "Imported - bBadNameb", "import profile name sanitized");
        string root = Path.Combine(Path.GetTempPath(), "quartz-profile-root");
        Assert(ProfileNames.TryResolveDirectory(root, "Default", out string resolved), "valid profile path accepted");
        Assert(resolved == Path.GetFullPath(Path.Combine(root, "Default")), "valid profile stays under root");
        Assert(!ProfileNames.TryResolveDirectory(root, "../victim", out _), "parent traversal rejected");
        Assert(!ProfileNames.TryResolveDirectory(root, "nested/profile", out _), "nested path rejected");
        Assert(!ProfileNames.TryResolveDirectory(root, ".", out _), "root alias rejected");
    }
}
