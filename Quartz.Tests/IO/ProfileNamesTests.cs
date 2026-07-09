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
    }
}
