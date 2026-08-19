#nullable enable
using Quartz.Features.Tuf;
using static Asserts;
using Quartz.Core;
static class TufRollbackTests {
    public static void TestSnapshotLayout() {
        string root = Path.Combine("library", "Levels");
        Assert(TufRollback.Root(root).EndsWith(Path.Combine("Levels", "rollback")), "rollback lives inside the levels folder");
        string snapshot = TufRollback.SnapshotFolder(root, 1755600000L, 7106);
        Assert(snapshot.EndsWith(Path.Combine("rollback", "1755600000", "7106")),
            "a snapshot is rollback/<unix stamp>/<level id>");
        Assert(TufRollback.MetaFile(root, 1755600000L, 7106).EndsWith(Path.Combine("1755600000", "7106.json")),
            "the description sits beside the level folder, not inside it");
        Assert(TufRollback.DescribeStamp(0L).Length == 16, "a stamp reads back as a human date and time");
    }
    public static void TestStampNames() {
        Assert(TufRollback.IsStampName("1755600000", out long stamp) && stamp == 1755600000L, "unix seconds accepted");
        Assert(!TufRollback.IsStampName("0", out _), "zero rejected");
        Assert(!TufRollback.IsStampName("", out _), "empty rejected");
        Assert(!TufRollback.IsStampName(null, out _), "null rejected");
        Assert(!TufRollback.IsStampName("-1755600000", out _), "negative rejected");
        Assert(!TufRollback.IsStampName("17556000o0", out _), "letters rejected");
        Assert(!TufRollback.IsStampName("1755600000000", out _), "overlong rejected");
        Assert(!TufRollback.IsStampName("..", out _), "traversal rejected");
    }
    public static void TestSnapshotDeleteGuard() {
        string temp = NewTemp();
        try {
            string root = Path.Combine(temp, "Levels");
            string good = TufRollback.SnapshotFolder(root, 1755600000L, 7106);
            Directory.CreateDirectory(good);
            Directory.CreateDirectory(Path.Combine(root, "7106"));
            Directory.CreateDirectory(Path.Combine(TufRollback.Root(root), "notastamp", "7106"));
            Directory.CreateDirectory(Path.Combine(root, "rollbackish", "1755600000", "7106"));
            Directory.CreateDirectory(Path.Combine(TufRollback.StampFolder(root, 1755600000L), "notalevel"));
            Assert(TufRollback.IsOwnedSnapshotFolder(good, root, 7106), "a well-formed snapshot is ours");
            Assert(!TufRollback.IsOwnedSnapshotFolder(good, root, 999), "the id must match the folder");
            Assert(!TufRollback.IsOwnedSnapshotFolder(Path.Combine(root, "7106"), root, 7106),
                "a live install is not a snapshot");
            Assert(!TufRollback.IsOwnedSnapshotFolder(Path.Combine(TufRollback.Root(root), "notastamp", "7106"), root, 7106),
                "the middle folder must be a unix stamp");
            Assert(!TufRollback.IsOwnedSnapshotFolder(Path.Combine(root, "rollbackish", "1755600000", "7106"), root, 7106),
                "a lookalike folder beside rollback is rejected");
            Assert(!TufRollback.IsOwnedSnapshotFolder(TufRollback.StampFolder(root, 1755600000L), root, 7106),
                "the stamp folder itself is rejected");
            Assert(!TufRollback.IsOwnedSnapshotFolder(TufRollback.SnapshotFolder(root, 1755600001L, 7106), root, 7106),
                "a missing snapshot is rejected");
            Assert(!TufRollback.IsOwnedSnapshotFolder(good, Path.Combine(temp, "OtherLevels"), 7106),
                "a snapshot under another library is rejected");
            Assert(!TufRollback.IsOwnedSnapshotFolder(null, root, 7106), "null rejected");
            Assert(!TufRollback.IsOwnedSnapshotFolder(good, null, 7106), "no library root means no delete");
        } finally { Cleanup(temp); }
    }
    static string NewTemp() {
        string temp = Path.Combine(Path.GetTempPath(), "quartz-rollback-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }
    static void Cleanup(string temp) {
        try { Directory.Delete(temp, true); } catch(Exception e) { Diag.Ignore(e); }
    }
}
