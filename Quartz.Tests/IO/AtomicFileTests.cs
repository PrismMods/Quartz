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
            Assert(Directory.GetFiles(root, "*.bak").Length == 0, "backup files cleaned");

            string staged = Path.Combine(root, "staged.tmp");
            File.WriteAllText(staged, "three");
            AtomicFile.ReplaceWithBackup(staged, path);
            Assert(File.ReadAllText(path) == "three", "backup fallback installs replacement");

            bool failed = false;
            try {
                AtomicFile.ReplaceWithBackup(Path.Combine(root, "missing.tmp"), path);
            } catch(IOException) {
                failed = true;
            }
            Assert(failed, "backup fallback surfaces commit failure");
            Assert(File.ReadAllText(path) == "three", "backup fallback restores original after failure");
            Assert(Directory.GetFiles(root, "*.bak").Length == 0, "restored fallback leaves no backup debris");

            string reserved = Path.Combine(root, "reserved" + AtomicFile.BackupSuffix);
            bool reservedRejected = false;
            try { AtomicFile.WriteAllText(reserved, "ambiguous"); }
            catch(ArgumentException) { reservedRejected = true; }
            Assert(reservedRejected && !File.Exists(reserved),
                "atomic destinations cannot collide with the deterministic recovery suffix");
            string reservedStage = Path.Combine(root, "reserved-stage.tmp");
            File.WriteAllText(reservedStage, "staged");
            reservedRejected = false;
            try { AtomicFile.ReplaceWithBackup(reservedStage, reserved); }
            catch(ArgumentException) { reservedRejected = true; }
            Assert(reservedRejected && File.Exists(reservedStage),
                "backup fallback rejects a reserved destination before moving its staged file");
            File.Delete(reservedStage);

            string nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(nested);
            string interrupted = Path.Combine(nested, "interrupted.json");
            string interruptedBackup = AtomicFile.BackupPath(interrupted);
            File.WriteAllText(interruptedBackup, "last durable value");
            string committed = Path.Combine(nested, "committed.json");
            string committedBackup = AtomicFile.BackupPath(committed);
            File.WriteAllText(committed, "committed value");
            File.WriteAllText(committedBackup, "old value");
            AtomicFile.RecoverTree(root);
            Assert(File.ReadAllText(interrupted) == "last durable value",
                "startup recovery restores a backup-only crash state");
            Assert(!File.Exists(interruptedBackup), "restored crash backup is consumed");
            Assert(File.ReadAllText(committed) == "committed value",
                "startup recovery keeps a committed destination");
            Assert(!File.Exists(committedBackup), "startup recovery cleans a committed backup");

            string brokenDestination = Path.Combine(nested, "broken.json");
            Directory.CreateDirectory(brokenDestination);
            string brokenBackup = AtomicFile.BackupPath(brokenDestination);
            File.WriteAllText(brokenBackup, "cannot restore over a directory");
            string healthyDestination = Path.Combine(nested, "healthy.json");
            File.WriteAllText(AtomicFile.BackupPath(healthyDestination), "healthy backup");
            AtomicFile.RecoverTree(root);
            Assert(File.ReadAllText(healthyDestination) == "healthy backup",
                "one damaged recovery artifact does not block a healthy sibling");
            Assert(File.Exists(brokenBackup), "a damaged recovery artifact is preserved for inspection");

            string excluded = Path.Combine(root, "large-library");
            Directory.CreateDirectory(excluded);
            string excludedDestination = Path.Combine(excluded, "ignored.json");
            string excludedBackup = AtomicFile.BackupPath(excludedDestination);
            File.WriteAllText(excludedBackup, "excluded backup");
            AtomicFile.RecoverTree(root, excluded);
            Assert(!File.Exists(excludedDestination) && File.Exists(excludedBackup),
                "startup recovery skips explicitly excluded user-library trees");

            string laterWriteBackup = AtomicFile.BackupPath(path);
            File.Move(path, laterWriteBackup);
            AtomicFile.WriteAllText(path, "four");
            Assert(File.ReadAllText(path) == "four", "a later write recovers before replacing");
            Assert(!File.Exists(laterWriteBackup), "later-write recovery leaves no backup");

            TestRecoverySkipsSymlinkDirectories(root);
        } finally {
            if(Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    private static void TestRecoverySkipsSymlinkDirectories(string root) {
        if(OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(Path.GetTempPath(), "quartz-atomic-outside-" + Guid.NewGuid().ToString("N"));
        string link = Path.Combine(root, "linked-outside");
        try {
            Directory.CreateDirectory(outside);
            string destination = Path.Combine(outside, "settings.json");
            string backup = AtomicFile.BackupPath(destination);
            File.WriteAllText(backup, "outside backup");
            try {
                Directory.CreateSymbolicLink(link, outside);
            } catch(Exception e) when(e is PlatformNotSupportedException or UnauthorizedAccessException or IOException) {
                return;
            }
            AtomicFile.RecoverTree(root);
            Assert(!File.Exists(destination), "startup recovery does not enter symlink directories");
            Assert(File.Exists(backup), "startup recovery leaves external backups untouched");
            Directory.Delete(link);
        } finally {
            if(Directory.Exists(link)) Directory.Delete(link);
            if(Directory.Exists(outside)) Directory.Delete(outside, true);
        }
    }
}
