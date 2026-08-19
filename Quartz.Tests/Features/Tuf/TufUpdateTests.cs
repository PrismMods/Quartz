#nullable enable
using Quartz.Features.Tuf;
using static Asserts;
using Quartz.Core;
static class TufUpdateTests {
    private static TufLevel Remote(string fileId, string updatedAt) =>
        new(42, "Song", "Artist", "Creator", "G9", "#FFFFFF", 0, 0,
            fileId.Length == 0 ? null : new Uri("https://api.tuforums.com/cdn/" + fileId)) {
            FileId = fileId,
            UpdatedAtUtc = TufUpdateCheck.ParseStamp(updatedAt)
        };
    public static void TestFileIdAndStampParsing() {
        Assert(TufUpdateCheck.FileIdOf("https://api.tuforums.com/cdn/abc-123") == "abc-123", "file id read off the cdn link");
        Assert(TufUpdateCheck.FileIdOf("https://api.tuforums.com/cdn/abc%20123") == "abc 123", "escaped file id decoded");
        Assert(TufUpdateCheck.FileIdOf("") == "", "empty link has no file id");
        Assert(TufUpdateCheck.FileIdOf("not a url") == "", "junk link has no file id");
        long stamp = TufUpdateCheck.ParseStamp("2026-08-19T03:32:30.000Z");
        Assert(stamp == new DateTime(2026, 8, 19, 3, 32, 30, DateTimeKind.Utc).Ticks, "ISO stamp parsed as UTC ticks");
        Assert(TufUpdateCheck.ParseStamp("") == 0, "empty stamp is zero");
        Assert(TufUpdateCheck.ParseStamp("whenever") == 0, "unparsable stamp is zero");
    }
    public static void TestUpdateDecision() {
        long installed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        TufInstallEntry known = new() { Id = 42, Folder = "/library/42", FileId = "old-file", InstalledAtUtc = installed };
        Assert(TufUpdateCheck.Decide(known, Remote("old-file", "2026-08-19T00:00:00Z")) == TufUpdateState.UpToDate,
            "same file id is up to date even when the record was edited later");
        Assert(TufUpdateCheck.Decide(known, Remote("new-file", "2026-08-19T00:00:00Z")) == TufUpdateState.Available,
            "a re-uploaded file id means an update");
        TufInstallEntry adopted = new() { Id = 42, Folder = "/library/42", InstalledAtUtc = installed };
        Assert(TufUpdateCheck.Decide(adopted, Remote("", "2026-08-19T00:00:00Z")) == TufUpdateState.Available,
            "without a baseline, a later remote edit counts as an update");
        Assert(TufUpdateCheck.Decide(adopted, Remote("", "2025-12-01T00:00:00Z")) == TufUpdateState.UpToDate,
            "without a baseline, an older remote edit is up to date");
        Assert(TufUpdateCheck.Decide(adopted, Remote("", "")) == TufUpdateState.Unknown,
            "no baseline and no remote stamp stays unknown");
        Assert(TufUpdateCheck.Decide(null, Remote("x", "")) == TufUpdateState.Unknown, "unknown level stays unknown");
        Assert(TufUpdateCheck.Decide(known, null) == TufUpdateState.Unknown, "a failed fetch stays unknown");
    }
    public static void TestBaselineSurvivesMetadataRefresh() {
        TufInstallIndex index = new();
        TufLevel installed = Remote("first-file", "2026-01-01T00:00:00Z");
        index.Record(installed, "/library/42");
        Assert(index.Find(42)!.FileId == "first-file", "install records the file it downloaded");
        TufLevel refreshed = Remote("second-file", "2026-08-19T00:00:00Z");
        index.Record(refreshed, "/library/42", false);
        Assert(index.Find(42)!.FileId == "first-file", "a metadata refresh must not adopt the newer file as the baseline");
        Assert(TufUpdateCheck.Decide(index.Find(42), refreshed) == TufUpdateState.Available,
            "the pending update is still visible after the refresh");
        index.Record(refreshed, "/library/42");
        Assert(index.Find(42)!.FileId == "second-file", "installing the update moves the baseline forward");
        Assert(TufUpdateCheck.Decide(index.Find(42), refreshed) == TufUpdateState.UpToDate, "and the level reads as current");
    }
    public static void TestFolderSize() {
        string temp = NewTemp();
        try {
            Directory.CreateDirectory(Path.Combine(temp, "nested"));
            File.WriteAllBytes(Path.Combine(temp, "chart.adofai"), new byte[100]);
            File.WriteAllBytes(Path.Combine(temp, "nested", "song.ogg"), new byte[540]);
            Assert(TufUpdateCheck.FolderSize(temp) == 640, "folder size counts nested files");
            Assert(TufUpdateCheck.FolderSize(Path.Combine(temp, "missing")) == 0, "a missing folder measures zero");
            Assert(TufUpdateCheck.FolderSize("") == 0, "an empty path measures zero");
        } finally { Cleanup(temp); }
    }
    static string NewTemp() {
        string temp = Path.Combine(Path.GetTempPath(), "quartz-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }
    static void Cleanup(string temp) {
        try { Directory.Delete(temp, true); } catch(Exception e) { Diag.Ignore(e); }
    }
}
