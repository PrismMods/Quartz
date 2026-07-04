using Quartz.IO;

using static Asserts;

static class AtomicFileTests {
    public static void TestAtomicFile() {
        string root = Path.Combine(Path.GetTempPath(), "koren-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "settings.json");
        try {
            AtomicFile.WriteAllText(path, "one");
            AtomicFile.WriteAllText(path, "two");
            Assert(File.ReadAllText(path) == "two", "replacement content");
            Assert(Directory.GetFiles(root, "*.tmp").Length == 0, "temporary files cleaned");
        } finally {
            if(Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
