#nullable enable
using Newtonsoft.Json.Linq;
using Quartz.Features.Tuf;
using static Asserts;

static class TufInstallTests {
    public static void TestLevelFolderNaming() {
        Assert(TufInstallPaths.IsLevelFolderName("123", out int plain) && plain == 123, "plain id folder");
        Assert(TufInstallPaths.IsLevelFolderName("tuf-456", out int linked) && linked == 456, "linked id folder");
        Assert(!TufInstallPaths.IsLevelFolderName("", out _), "empty rejected");
        Assert(!TufInstallPaths.IsLevelFolderName("0", out _), "zero rejected");
        Assert(!TufInstallPaths.IsLevelFolderName("Documents", out _), "word rejected");
        Assert(!TufInstallPaths.IsLevelFolderName("12a", out _), "mixed rejected");
        Assert(!TufInstallPaths.IsLevelFolderName("tuf-", out _), "bare prefix rejected");
        Assert(!TufInstallPaths.IsLevelFolderName("-5", out _), "negative rejected");
        Assert(!TufInstallPaths.IsLevelFolderName("1234567890123", out _), "overflowing id rejected");
        Assert(!TufInstallPaths.IsLevelFolderName("../123", out _), "traversal rejected");
        Assert(TufInstallPaths.LevelFolderName(7, false) == "7", "plain name built");
        Assert(TufInstallPaths.LevelFolderName(7, true) == "tuf-7", "linked name built");
    }

    // The delete guard is what stands between a corrupt install index and a
    // recursive delete of something we never created.
    public static void TestDeleteGuard() {
        string temp = NewTemp();
        try {
            string root = Path.Combine(temp, "library");
            string level = Path.Combine(root, "123");
            Directory.CreateDirectory(level);
            string outside = Path.Combine(temp, "elsewhere", "123");
            Directory.CreateDirectory(outside);
            string nested = Path.Combine(root, "sub", "123");
            Directory.CreateDirectory(nested);
            string notALevel = Path.Combine(root, "Documents");
            Directory.CreateDirectory(notALevel);
            string[] roots = [root];

            Assert(TufInstallPaths.IsOwnedLevelFolder(level, roots), "level folder under root accepted");
            Assert(!TufInstallPaths.IsOwnedLevelFolder(outside, roots), "level folder outside every root rejected");
            Assert(!TufInstallPaths.IsOwnedLevelFolder(nested, roots), "grandchild rejected: only direct children are ours");
            Assert(!TufInstallPaths.IsOwnedLevelFolder(notALevel, roots), "non-level name rejected");
            Assert(!TufInstallPaths.IsOwnedLevelFolder(root, roots), "the root itself rejected");
            Assert(!TufInstallPaths.IsOwnedLevelFolder(Path.Combine(root, "999"), roots), "missing folder rejected");
            Assert(!TufInstallPaths.IsOwnedLevelFolder(level, []), "no trusted roots means no delete");
            Assert(!TufInstallPaths.IsOwnedLevelFolder(null, roots), "null rejected");
            Assert(!TufInstallPaths.IsOwnedLevelFolder("", roots), "empty rejected");
        } finally { Cleanup(temp); }
    }

    public static void TestLibraryRootValidation() {
        string temp = NewTemp();
        try {
            string empty = Path.Combine(temp, "empty");
            Directory.CreateDirectory(empty);
            Assert(TufInstallPaths.IsUsableLibraryRoot(empty, out _), "empty folder usable");

            string withLevels = Path.Combine(temp, "levels");
            Directory.CreateDirectory(Path.Combine(withLevels, "42"));
            File.WriteAllText(Path.Combine(withLevels, ".layout-v2"), "2");
            Assert(TufInstallPaths.IsUsableLibraryRoot(withLevels, out _),
                "a folder holding only level folders is already ours");

            // The rule that keeps a pick of Documents or Desktop from becoming a
            // directory Quartz manages.
            string userFiles = Path.Combine(temp, "documents");
            Directory.CreateDirectory(userFiles);
            File.WriteAllText(Path.Combine(userFiles, "taxes.pdf"), "x");
            Assert(!TufInstallPaths.IsUsableLibraryRoot(userFiles, out string reason), "folder with user files rejected");
            Assert(reason == "not-empty", "rejection reason reported: " + reason);

            Assert(!TufInstallPaths.IsUsableLibraryRoot(Path.Combine(temp, "nope"), out string missing), "missing folder rejected");
            Assert(missing == "missing", "missing reason reported: " + missing);
            Assert(!TufInstallPaths.IsUsableLibraryRoot("", out _), "empty path rejected");
            string volumeRoot = Path.GetPathRoot(Path.GetFullPath(temp))!;
            Assert(!TufInstallPaths.IsUsableLibraryRoot(volumeRoot, out string vol), "volume root rejected");
            Assert(vol == "volume-root", "volume-root reason reported: " + vol);
        } finally { Cleanup(temp); }
    }

    public static void TestNestedRoots() {
        string a = Path.Combine(Path.GetTempPath(), "quartz-nest", "outer");
        string inner = Path.Combine(a, "inner");
        Assert(TufInstallPaths.IsSameOrNested(a, a), "same path detected");
        Assert(TufInstallPaths.IsSameOrNested(a, inner), "child detected");
        Assert(TufInstallPaths.IsSameOrNested(inner, a), "parent detected");
        Assert(!TufInstallPaths.IsSameOrNested(a, Path.Combine(Path.GetTempPath(), "quartz-nest", "sibling")),
            "sibling is independent");
        Assert(!TufInstallPaths.IsSameOrNested(a, ""), "empty is not nested");
    }

    public static void TestInstallIndexRoundTrip() {
        TufInstallIndex index = new();
        TufLevel level = new(11, "Song", "Artist", "Creator", "G15", "#AABBCC", 3, 4,
            new Uri("https://cdn.tuforums.com/zips/x.zip"));
        index.Record(level, "/tmp/library/11");
        Assert(index.Count == 1, "install recorded");
        TufInstallEntry? entry = index.Find(11);
        Assert(entry != null && entry.Song == "Song" && entry.Difficulty == "G15", "metadata stored");
        Assert(entry!.InstalledAtUtc > 0, "install time stamped");

        long first = entry.InstalledAtUtc;
        index.Record(level, "/tmp/library/11");
        Assert(index.Count == 1, "re-download does not duplicate");
        Assert(index.Find(11)!.InstalledAtUtc == first, "re-download keeps the original install time");

        index.SetFolder(11, "/tmp/moved/11");
        Assert(index.Find(11)!.Folder == Path.GetFullPath("/tmp/moved/11"), "folder updated after a move");

        TufInstallIndex restored = new();
        restored.Deserialize(index.Serialize());
        Assert(restored.Count == 1, "index round-trips");
        TufInstallEntry back = restored.Find(11)!;
        Assert(back.Song == "Song" && back.Clears == 3 && back.Likes == 4, "fields survive a round trip");
        Assert(back.ToLevel().DownloadUri != null, "download url round-trips");

        Assert(restored.Remove(11) && restored.Count == 0, "entry removed");
        Assert(!restored.Remove(11), "removing twice is a no-op");
    }

    // The index is a file on disk: it can be hand-edited or corrupted, and nothing
    // in it may be trusted as-is.
    public static void TestInstallIndexRejectsJunk() {
        TufInstallIndex index = new();
        index.Deserialize(JToken.Parse("""
            {"Version":1,"Entries":[
              {"Id":0,"Folder":"/tmp/a"},
              {"Id":5},
              {"Id":6,"Folder":""},
              {"Id":7,"Folder":"/tmp/b","Clears":-9,"DifficultyColor":"nonsense"},
              {"Id":7,"Folder":"/tmp/dup"}
            ]}
            """));
        Assert(index.Find(0) == null, "id 0 dropped");
        Assert(index.Find(5) == null, "entry without a folder dropped");
        Assert(index.Find(6) == null, "entry with an empty folder dropped");
        TufInstallEntry? kept = index.Find(7);
        Assert(kept != null, "valid entry kept");
        Assert(kept!.Clears == 0, "negative clears clamped");
        Assert(kept.DifficultyColor == "#FFFFFF", "bad colour normalized");
        Assert(kept.Folder == "/tmp/b", "first record wins on duplicate ids");
        Assert(index.Count == 1, "only the valid entry survived");

        index.Deserialize(JToken.Parse("""{"Version":1}"""));
        Assert(index.Count == 0, "missing entries array is not an error");

        TufInstallEntry adopted = new() { Id = 9, Folder = "/tmp/c", DownloadUrl = "https://evil.example.com/x.zip" };
        Assert(adopted.ToLevel().DownloadUri == null, "an index url outside the TUF CDN is not trusted");
    }

    // Levels installed before the index existed still need to be listed and deleted.
    public static void TestAdoptAndPrune() {
        string temp = NewTemp();
        try {
            string root = Path.Combine(temp, "library");
            string gone = Path.Combine(root, "1");
            Directory.CreateDirectory(gone);
            TufInstallIndex index = new();
            index.Adopt(1, gone, 100);
            index.Adopt(2, Path.Combine(root, "2"), 200);
            Assert(index.Count == 2, "orphans adopted");
            Assert(index.Entries[0].Id == 2, "newest install sorts first");
            Assert(index.Find(1)!.Song == "", "adopted entry has no metadata");

            // Unknown metadata must survive a save/load as unknown. If it comes back
            // as a placeholder, the browser stops rendering the "we don't know this
            // level yet" card and shows a made-up title instead.
            TufInstallIndex reloaded = new();
            reloaded.Deserialize(index.Serialize());
            Assert(reloaded.Find(1)!.Song == "", "adopted entry stays metadata-less across a round trip");
            Assert(reloaded.Find(1)!.Artist == "", "adopted artist stays empty across a round trip");

            Assert(index.PruneMissing(), "a missing folder is pruned");
            Assert(index.Count == 1 && index.Find(1) != null, "the folder that exists is kept");
            Assert(!index.PruneMissing(), "pruning again reports no change");
        } finally { Cleanup(temp); }
    }

    static string NewTemp() {
        string temp = Path.Combine(Path.GetTempPath(), "quartz-install-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }

    static void Cleanup(string temp) {
        try { Directory.Delete(temp, true); } catch { }
    }
}
