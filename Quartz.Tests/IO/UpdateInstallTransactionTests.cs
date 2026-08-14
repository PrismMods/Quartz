using System.IO.Compression;
using Quartz.Update;
using static Asserts;
static class UpdateInstallTransactionTests {
    public static void Run() {
        string root = Path.Combine(Path.GetTempPath(), "quartz-update-tests-" + Guid.NewGuid().ToString("N"));
        string install = Path.Combine(root, "game");
        string workspace = Path.Combine(install, "UserData", "Quartz", "Temp", "Update");
        string zip = Path.Combine(workspace, "Quartz.zip");
        try {
            Directory.CreateDirectory(workspace);
            using(ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create)) {
                Write(archive, "Mods/Quartz.dll", "new core");
                Write(archive, "UserData/Quartz/Lang/ko-KR.json", "new lang");
            }
            string payload = Path.Combine(workspace, "Payload");
            IReadOnlyList<StagedInstallFile> staged = UpdateInstallTransaction.StageZip(zip, payload, install);
            Assert(staged.Count == 2, "update ZIP stages every file");
            Assert(staged.All(file => File.Exists(file.Source)), "update ZIP is fully extracted before commit");
            UpdateInstallTransaction.Commit(staged, payload, install);
            string core = Path.Combine(install, "Mods", "Quartz.dll");
            string lang = Path.Combine(install, "UserData", "Quartz", "Lang", "ko-KR.json");
            Assert(File.ReadAllText(core) == "new core", "update transaction installs core");
            Assert(File.ReadAllText(lang) == "new lang", "update transaction installs data");

            string rollbackStage = Path.Combine(root, "rollback-stage");
            Directory.CreateDirectory(rollbackStage);
            string coreNext = Stage(rollbackStage, "core-next", "broken core");
            string added = Stage(rollbackStage, "added", "new file");
            string never = Stage(rollbackStage, "never", "never installed");
            string addedDest = Path.Combine(install, "Mods", "Added.dll");
            bool failed = false;
            try {
                UpdateInstallTransaction.Commit([
                    new StagedInstallFile(coreNext, core),
                    new StagedInstallFile(added, addedDest),
                    new StagedInstallFile(never, lang),
                ], rollbackStage, install, index => {
                    if(index == 2) throw new IOException("injected commit failure");
                });
            } catch(IOException) {
                failed = true;
            }
            Assert(failed, "update transaction surfaces commit failure");
            Assert(File.ReadAllText(core) == "new core", "rollback restores replaced file");
            Assert(!File.Exists(addedDest), "rollback removes newly installed file");
            Assert(File.ReadAllText(lang) == "new lang", "rollback leaves untouched file unchanged");
            Assert(Directory.GetFiles(install, "*.bak", SearchOption.AllDirectories).Length == 0,
                "successful rollback leaves no backup debris");

            string badZip = Path.Combine(root, "bad.zip");
            using(ZipArchive archive = ZipFile.Open(badZip, ZipArchiveMode.Create))
                Write(archive, "../escape.txt", "escape");
            bool rejected = false;
            try {
                UpdateInstallTransaction.StageZip(badZip, Path.Combine(root, "bad-stage"), install);
            } catch(InvalidDataException) {
                rejected = true;
            }
            Assert(rejected, "update ZIP rejects traversal before commit");
            Assert(!File.Exists(Path.Combine(root, "escape.txt")), "traversal never writes outside install root");
            TestSymlinkBoundaries(root, install);
        } finally {
            if(Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    private static string Stage(string root, string name, string contents) {
        string path = Path.Combine(root, name);
        File.WriteAllText(path, contents);
        return path;
    }
    private static void Write(ZipArchive archive, string name, string contents) {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using StreamWriter writer = new(entry.Open());
        writer.Write(contents);
    }
    private static void TestSymlinkBoundaries(string root, string install) {
        if(OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(root, "outside");
        string link = Path.Combine(install, "Linked");
        string stage = Path.Combine(root, "link-stage");
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(stage);
        try {
            Directory.CreateSymbolicLink(link, outside);
        } catch(Exception e) when(e is PlatformNotSupportedException or UnauthorizedAccessException or IOException) {
            return;
        }
        string stageOutside = Path.Combine(outside, "stage-target");
        string stageLink = Path.Combine(root, "stage-link");
        string linkZip = Path.Combine(root, "stage-link.zip");
        Directory.CreateDirectory(stageOutside);
        Directory.CreateSymbolicLink(stageLink, stageOutside);
        using(ZipArchive archive = ZipFile.Open(linkZip, ZipArchiveMode.Create))
            Write(archive, "Mods/Escaped.dll", "must not extract through a symlink");
        bool rejected = false;
        try {
            UpdateInstallTransaction.StageZip(linkZip, stageLink, install);
        } catch(Exception e) when(e is IOException or InvalidDataException) { rejected = true; }
        Assert(rejected, "update staging rejects a symlink root");
        Assert(Directory.GetFileSystemEntries(stageOutside).Length == 0,
            "stage-root symlink cannot redirect extraction");
        Directory.Delete(stageLink);

        string source = Stage(stage, "payload", "must stay contained");
        rejected = false;
        try {
            UpdateInstallTransaction.Commit([
                new StagedInstallFile(source, Path.Combine(link, "escaped.dll"))
            ], stage, install);
        } catch(Exception e) when(e is IOException or InvalidDataException) { rejected = true; }
        Assert(rejected, "update commit rejects a symlink destination ancestor");
        Assert(!File.Exists(Path.Combine(outside, "escaped.dll")), "destination symlink cannot escape install root");

        Directory.Delete(link);
        string raceParent = Path.Combine(install, "Race");
        Directory.CreateDirectory(raceParent);
        string raceSource = Stage(stage, "race-payload", "must also stay contained");
        rejected = false;
        try {
            UpdateInstallTransaction.Commit([
                new StagedInstallFile(raceSource, Path.Combine(raceParent, "escaped.dll"))
            ], stage, install, _ => {
                Directory.Delete(raceParent);
                Directory.CreateSymbolicLink(raceParent, outside);
            });
        } catch(Exception e) when(e is IOException or InvalidDataException) { rejected = true; }
        Assert(rejected, "update commit rechecks destination ancestors immediately before moving");
        Assert(!File.Exists(Path.Combine(outside, "escaped.dll")), "swapped destination symlink cannot escape install root");
        Directory.Delete(raceParent);
    }
}
