using System.IO.Compression;
using Quartz.Bootstrap;
using Quartz.UpdateEngine;
using static Asserts;
static class BootstrapRuntimeTests {
    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "quartz-bootstrap-" + Guid.NewGuid().ToString("N"));
    private static void MakeRuntime(string versionsRoot, string version, bool complete = true) {
        string dir = Path.Combine(versionsRoot, version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, BootstrapInfo.PayloadFileName), "payload");
        if(complete) File.WriteAllText(Path.Combine(dir, BootstrapInfo.EngineFileName), "engine");
        File.WriteAllText(Path.Combine(dir, "runtime.json"), $"{{\"Version\": \"{version}\"}}");
    }
    public static void TestStoreSeedsFromDisk() {
        string root = NewRoot();
        try {
            string versions = Path.Combine(root, "versions");
            MakeRuntime(versions, "9.9.7");
            MakeRuntime(versions, "9.9.8");
            Directory.CreateDirectory(Path.Combine(versions, "not-a-version"));
            List<string> warnings = [];
            RuntimeStore store = new(root, warnings.Add);
            RuntimeState state = store.LoadAndRepair();
            Assert(state.Current == "9.9.8", "seeding picks the highest valid runtime");
            Assert(File.Exists(Path.Combine(root, "state.json")), "seeding persists the state");
            RuntimeCandidate current = store.GetCandidate(state.Current);
            Assert(current.PayloadPath.EndsWith(BootstrapInfo.PayloadFileName, StringComparison.Ordinal), "candidate points at the payload");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestInterruptedTrialIsHeldBack() {
        string root = NewRoot();
        try {
            string versions = Path.Combine(root, "versions");
            MakeRuntime(versions, "9.9.7");
            MakeRuntime(versions, "9.9.8");
            File.WriteAllText(Path.Combine(root, "state.json"),
                "{\"SchemaVersion\": 1, \"Current\": \"9.9.7\", \"Trial\": \"9.9.8\"}");
            List<string> warnings = [];
            RuntimeStore store = new(root, warnings.Add);
            RuntimeState state = store.LoadAndRepair();
            Assert(state.Current == "9.9.7", "the interrupted trial never becomes current");
            Assert(state.Trial == null, "the trial marker is cleared");
            Assert(state.Failed == "9.9.8", "the trial version is held back as failed");
            Assert(!Directory.Exists(Path.Combine(versions, "9.9.8")), "the failed trial runtime is removed");
            Assert(warnings.Count > 0, "the demotion is reported");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestPromoteKeepsPreviousDropsOlder() {
        string root = NewRoot();
        try {
            string versions = Path.Combine(root, "versions");
            MakeRuntime(versions, "9.9.6");
            MakeRuntime(versions, "9.9.7");
            MakeRuntime(versions, "9.9.8");
            File.WriteAllText(Path.Combine(root, "state.json"),
                "{\"SchemaVersion\": 1, \"Current\": \"9.9.7\", \"Previous\": \"9.9.6\", \"Failed\": \"9.9.5\"}");
            RuntimeStore store = new(root, _ => { });
            RuntimeState state = store.LoadAndRepair();
            store.Promote(state, "9.9.8");
            Assert(state.Current == "9.9.8", "promote moves current");
            Assert(state.Previous == "9.9.7", "promote keeps the old current as rollback");
            Assert(state.Failed == null, "promote clears the held-back version");
            Assert(!Directory.Exists(Path.Combine(versions, "9.9.6")), "promote drops runtimes beyond the pair");
            Assert(Directory.Exists(Path.Combine(versions, "9.9.7")), "the rollback runtime stays");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestBrokenCurrentFallsBackToPrevious() {
        string root = NewRoot();
        try {
            string versions = Path.Combine(root, "versions");
            MakeRuntime(versions, "9.9.7");
            MakeRuntime(versions, "9.9.8", complete: false);
            File.WriteAllText(Path.Combine(root, "state.json"),
                "{\"SchemaVersion\": 1, \"Current\": \"9.9.8\", \"Previous\": \"9.9.7\"}");
            List<string> warnings = [];
            RuntimeStore store = new(root, warnings.Add);
            RuntimeState state = store.LoadAndRepair();
            Assert(state.Current == "9.9.7", "a broken current falls back to previous");
            Assert(warnings.Count > 0, "the fallback is reported");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestInstallerRoutesRuntimeAndDataSync() {
        string root = NewRoot();
        try {
            Directory.CreateDirectory(root);
            string zipPath = Path.Combine(root, "package.zip");
            using(FileStream stream = File.Create(zipPath))
            using(ZipArchive zip = new(stream, ZipArchiveMode.Create)) {
                AddEntry(zip, "Mods/Quartz.Bootstrap.dll", "bootstrap");
                AddEntry(zip, "UserData/Quartz/Runtime/state.json", "{}");
                AddEntry(zip, "UserData/Quartz/Runtime/versions/9.9.9/" + EngineInfo.PayloadFileName, "payload");
                AddEntry(zip, "UserData/Quartz/Runtime/versions/9.9.9/" + EngineInfo.EngineFileName, "engine");
                AddEntry(zip, "UserData/Quartz/Runtime/versions/9.9.9/runtime.json", "{\"Version\": \"9.9.9\"}");
                AddEntry(zip, "UserData/Quartz/Lang/en-US.json", "{\"HI\": \"hi\"}");
            }
            string runtimePath = PackageInstaller.Install(zipPath, root, "9.9.9");
            Assert(runtimePath == Path.Combine(root, "versions", "9.9.9"), "the runtime lands in versions/<version>");
            Assert(File.Exists(Path.Combine(runtimePath, EngineInfo.PayloadFileName)), "the payload is extracted");
            Assert(File.Exists(Path.Combine(runtimePath, PackageInstaller.DataSyncDirName, "Lang", "en-US.json")), "data files stage into .data-sync");
            Assert(!File.Exists(Path.Combine(runtimePath, PackageInstaller.DataSyncDirName, "Runtime", "state.json")), "the zip's runtime store is not treated as data");
            Assert(!Directory.Exists(Path.Combine(runtimePath, "Mods")), "the bootstrap DLL is dropped");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestInstallerRejectsTraversalAndEmptyPackages() {
        string root = NewRoot();
        try {
            Directory.CreateDirectory(root);
            string evil = Path.Combine(root, "evil.zip");
            using(FileStream stream = File.Create(evil))
            using(ZipArchive zip = new(stream, ZipArchiveMode.Create)) {
                AddEntry(zip, "UserData/Quartz/Runtime/versions/9.9.9/../../../../evil.dll", "boom");
            }
            Assert(Throws(() => PackageInstaller.Install(evil, root, "9.9.9")), "a traversal path is rejected");
            string empty = Path.Combine(root, "empty.zip");
            using(FileStream stream = File.Create(empty))
            using(ZipArchive zip = new(stream, ZipArchiveMode.Create)) {
                AddEntry(zip, "Mods/whatever.dll", "x");
            }
            Assert(Throws(() => PackageInstaller.Install(empty, root, "9.9.9")), "a package without the runtime is rejected");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestDataSyncCopiesAndRetires() {
        string root = NewRoot();
        try {
            string runtime = Path.Combine(root, "runtime");
            string dataRoot = Path.Combine(root, "data");
            string staged = Path.Combine(runtime, DataSync.DirName, "Lang");
            Directory.CreateDirectory(staged);
            Directory.CreateDirectory(dataRoot);
            File.WriteAllText(Path.Combine(staged, "en-US.json"), "fresh");
            File.WriteAllText(Path.Combine(dataRoot, "Settings.json"), "user");
            DataSync.Apply(runtime, dataRoot, _ => { });
            Assert(File.ReadAllText(Path.Combine(dataRoot, "Lang", "en-US.json")) == "fresh", "staged files land in the data root");
            Assert(File.ReadAllText(Path.Combine(dataRoot, "Settings.json")) == "user", "user files are untouched");
            Assert(!Directory.Exists(Path.Combine(runtime, DataSync.DirName)), "the staging folder is consumed");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestLegacyRetireSignalsTheHandoffLaunch() {
        string root = NewRoot();
        try {
            string mods = Path.Combine(root, "Mods");
            string userLibs = Path.Combine(root, "UserLibs");
            Directory.CreateDirectory(mods);
            Directory.CreateDirectory(userLibs);
            Assert(!LegacyCleanup.RetireMelonLeftovers(mods, userLibs, _ => { }), "a clean install reports no legacy mod");
            File.WriteAllText(Path.Combine(mods, BootstrapInfo.PayloadFileName), "old payload");
            File.WriteAllText(Path.Combine(userLibs, "Quartz.dll"), "old lib");
            Assert(LegacyCleanup.RetireMelonLeftovers(mods, userLibs, _ => { }), "a legacy payload in Mods signals the handoff launch");
            Assert(!File.Exists(Path.Combine(mods, BootstrapInfo.PayloadFileName)), "the legacy payload is retired");
            Assert(!File.Exists(Path.Combine(userLibs, "Quartz.dll")), "the legacy UserLibs copy is retired");
            Assert(!LegacyCleanup.RetireMelonLeftovers(mods, userLibs, _ => { }), "the next launch is bootstrap-only");
            File.WriteAllText(Path.Combine(mods, BootstrapInfo.PayloadFileName) + ".old", "locked leftover");
            Assert(!LegacyCleanup.RetireMelonLeftovers(mods, userLibs, _ => { }), "a .old leftover alone never re-triggers the handoff");
            Assert(!File.Exists(Path.Combine(mods, BootstrapInfo.PayloadFileName) + ".old"), "the .old leftover is swept");
        } finally { Directory.Delete(root, true); }
    }
    public static void TestRecoveryRestoresTheShippedRuntime() {
        string root = NewRoot();
        try {
            Directory.CreateDirectory(root);
            string zipPath = Path.Combine(root, "release.zip");
            string prefix = "UserData/Quartz/Runtime/versions/" + Quartz.Bootstrap.BootstrapInfo.Version + "/";
            using(FileStream stream = File.Create(zipPath))
            using(ZipArchive zip = new(stream, ZipArchiveMode.Create)) {
                AddEntry(zip, "Mods/Quartz.Bootstrap.dll", "bootstrap");
                AddEntry(zip, prefix + Quartz.Bootstrap.BootstrapInfo.PayloadFileName, "payload");
                AddEntry(zip, prefix + Quartz.Bootstrap.BootstrapInfo.EngineFileName, "engine");
                AddEntry(zip, prefix + "runtime.json", $"{{\"Version\": \"{Quartz.Bootstrap.BootstrapInfo.Version}\"}}");
                AddEntry(zip, "UserData/Quartz/Lang/en-US.json", "{}");
            }
            string restored = RecoveryInstaller.InstallArchive(zipPath, root);
            Assert(restored == Path.Combine(root, "versions", Quartz.Bootstrap.BootstrapInfo.Version), "recovery restores its own version");
            RuntimeStore store = new(root, _ => { });
            RuntimeState state = store.LoadAndRepair();
            Assert(state.Current == Quartz.Bootstrap.BootstrapInfo.Version, "the restored runtime seeds the store");
            string incomplete = Path.Combine(root, "incomplete.zip");
            using(FileStream stream = File.Create(incomplete))
            using(ZipArchive zip = new(stream, ZipArchiveMode.Create)) {
                AddEntry(zip, prefix + Quartz.Bootstrap.BootstrapInfo.PayloadFileName, "payload only");
            }
            Assert(Throws(() => RecoveryInstaller.InstallArchive(incomplete, root)), "an incomplete recovery package is rejected");
        } finally { Directory.Delete(root, true); }
    }
    private static void AddEntry(ZipArchive zip, string name, string contents) {
        using Stream entry = zip.CreateEntry(name).Open();
        using StreamWriter writer = new(entry);
        writer.Write(contents);
    }
    private static bool Throws(Action action) {
        try {
            action();
            return false;
        } catch(InvalidDataException) {
            return true;
        }
    }
}
