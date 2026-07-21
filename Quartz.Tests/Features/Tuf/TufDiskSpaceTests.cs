#nullable enable
using System.IO.Compression;
using Quartz.Features.Tuf;
using static Asserts;
static class TufDiskSpaceTests {
    public static void TestSpaceProbeFailsOpen() {
        string temp = Path.GetTempPath();
        long? here = TufDiskSpace.AvailableBytes(temp);
        Assert(here is > 0, "free space is readable for the temp volume");
        Assert(TufDiskSpace.AvailableBytes(null) == null, "null path is unknown, not zero");
        Assert(TufDiskSpace.AvailableBytes("") == null, "empty path is unknown, not zero");
        Assert(TufDiskSpace.AvailableBytes("   ") == null, "blank path is unknown, not zero");
        Assert(!TufDiskSpace.IsKnownInsufficient(null, long.MaxValue, out _),
            "an unknown volume never blocks a download, however large");
        Assert(!TufDiskSpace.IsKnownInsufficient("", long.MaxValue, out _),
            "an unreadable path never blocks a download");
        Assert(!TufDiskSpace.IsKnownInsufficient(temp, 0, out long free) && free > 0,
            "needing nothing always fits, and the free figure is reported");
        Assert(TufDiskSpace.IsKnownInsufficient(temp, long.MaxValue - TufDiskSpace.Headroom, out _),
            "a need no volume can meet is refused");
        Assert(TufDiskSpace.IsKnownInsufficient(temp, long.MaxValue, out _),
            "a saturated claim does not overflow into looking free");
        Assert(TufDiskSpace.IsKnownInsufficient(temp, long.MaxValue - 1, out _),
            "a near-saturated claim does not overflow either");
        Assert(!TufDiskSpace.IsKnownInsufficient(temp, -1, out _), "a negative need is nonsense, not a block");
        Assert(TufDiskSpace.IsKnownInsufficient(temp, here!.Value, out _),
            "filling the volume to the last byte is refused");
    }
    public static void TestSizeWording() {
        Assert(TufDiskSpace.Describe(0) == "0 B", "zero bytes");
        Assert(TufDiskSpace.Describe(512) == "512 B", "plain bytes");
        Assert(TufDiskSpace.Describe(1024) == "1.0 KB", "kilobytes");
        Assert(TufDiskSpace.Describe(661_303_474) == "631 MB", "level 5350 reads as 631 MB");
        Assert(TufDiskSpace.Describe(536_870_912) == "512 MB", "the old cap reads as 512 MB");
        Assert(TufDiskSpace.Describe(2L * 1024 * 1024 * 1024) == "2.0 GB", "gigabytes");
        Assert(!TufDiskSpace.Describe(1536).Contains(','), "no locale-dependent separator");
    }
    public static void TestDeclaredSize() {
        string temp = Path.Combine(Path.GetTempPath(), "quartz-declared-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            string zip = Path.Combine(temp, "level.zip");
            MakeZip(zip, archive => {
                Write(archive, "main.adofai", new string('x', 100));
                Write(archive, "bg.png", new string('y', 900));
            });
            Assert(TufArchive.DeclaredSize(zip) == 1000, "declared size sums every entry");
            string empty = Path.Combine(temp, "empty.zip");
            MakeZip(empty, _ => { });
            Assert(TufArchive.DeclaredSize(empty) == 0, "an empty archive declares nothing");
            string many = Path.Combine(temp, "many.zip");
            MakeZip(many, archive => {
                for(int i = 0; i <= TufArchive.MaxEntries; i++) archive.CreateEntry("d/" + i);
            });
            AssertThrows(() => TufArchive.DeclaredSize(many), "entry count is still capped");
            string ordinary = Path.Combine(temp, "ordinary.zip");
            MakeZip(ordinary, archive => Write(archive, "main.adofai", new string('z', 2048)));
            string outDir = Path.Combine(temp, "out");
            TufArchive.Extract(ordinary, outDir);
            Assert(File.Exists(Path.Combine(outDir, "main.adofai")), "a normal archive still extracts");
        } finally {
            try { Directory.Delete(temp, true); } catch { }
        }
    }
    static void AssertThrows(Action action, string message) {
        try { action(); }
        catch(InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }
    static void MakeZip(string path, Action<ZipArchive> build) {
        using FileStream file = new(path, FileMode.Create, FileAccess.Write);
        using ZipArchive archive = new(file, ZipArchiveMode.Create);
        build(archive);
    }
    static void Write(ZipArchive archive, string name, string content) {
        using StreamWriter writer = new(archive.CreateEntry(name).Open());
        writer.Write(content);
    }
}
