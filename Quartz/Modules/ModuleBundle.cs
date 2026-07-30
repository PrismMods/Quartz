using Quartz.Async;
using Quartz.Core;
namespace Quartz.Modules;
public static class ModuleBundle {
    public static string Path => System.IO.Path.Combine(MainCore.Paths.RootPath, ModuleMigration.BundledFolderName);
    public static bool Has(string id) => id != null && File.Exists(BinaryOf(id)) && File.Exists(ManifestOf(id));
    private static string BinaryOf(string id) => System.IO.Path.Combine(Path, id + ModuleService.ModuleExtension);
    private static string ManifestOf(string id) => System.IO.Path.Combine(Path, id + ModuleService.ManifestExtension);
    public static string VersionAt(string manifestPath) {
        try {
            if(manifestPath == null || !File.Exists(manifestPath)) return null;
            return ModuleManifest.Parse(File.ReadAllText(manifestPath), out _)?.Version;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public static string VersionOf(string id) => Has(id) ? VersionAt(ManifestOf(id)) : null;
    private static List<ModuleManifest> scanned;
    private static string scannedRoot;
    private static DateTime scannedStamp;
    private static List<ModuleManifest> Scan() {
        string root = Path;
        DateTime stamp;
        try {
            if(!Directory.Exists(root)) return scanned = [];
            stamp = Directory.GetLastWriteTimeUtc(root);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Modules] couldn't read the bundled folder: {e.Message}");
            return scanned = [];
        }
        if(scanned != null && scannedRoot == root && scannedStamp == stamp) return scanned;
        List<ModuleManifest> found = [];
        string[] files;
        try {
            files = Directory.GetFiles(root, "*" + ModuleService.ManifestExtension);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Modules] couldn't read the bundled folder: {e.Message}");
            return scanned = [];
        }
        foreach(string file in files) {
            string id = System.IO.Path.GetFileName(file);
            id = id[..^ModuleService.ManifestExtension.Length];
            if(!Has(id)) continue;
            ModuleManifest manifest;
            try {
                manifest = ModuleManifest.Parse(File.ReadAllText(file), out _);
            } catch(Exception e) {
                Diag.Ignore(e);
                continue;
            }
            if(manifest == null || manifest.Id != id) continue;
            if(manifest.CoreAbi != Info.ModuleAbi) continue;
            found.Add(manifest);
        }
        found.Sort(static (a, b) => {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });
        scannedRoot = root;
        scannedStamp = stamp;
        return scanned = found;
    }
    public static IReadOnlyList<ModuleManifest> Available() {
        List<ModuleManifest> all = Scan();
        List<ModuleManifest> found = new(all.Count);
        foreach(ModuleManifest manifest in all)
            if(ModuleService.Find(manifest.Id) == null) found.Add(manifest);
        return found;
    }
    public static bool Copy(string id) {
        if(!Has(id)) return false;
        try {
            string root = MainCore.Paths.ModulePath;
            Directory.CreateDirectory(root);
            File.Copy(BinaryOf(id), System.IO.Path.Combine(root, id + ModuleService.ModuleExtension), true);
            File.Copy(ManifestOf(id), System.IO.Path.Combine(root, id + ModuleService.ManifestExtension), true);
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] couldn't install bundled '{id}': {e.Message}");
            return false;
        }
    }
    public static void Install(string id) {
        List<string> installed = [];
        foreach(string needed in WithDeps(id)) {
            if(ModuleService.Find(needed) is { Loaded: true }) continue;
            if(!Copy(needed)) continue;
            ModuleState.Entry entry = ModuleService.State.For(needed);
            entry.Enabled = true;
            entry.Source = "bundled";
            installed.Add(needed);
        }
        if(installed.Count == 0) return;
        ModuleService.State.Save();
        MainCore.Log.Msg($"[Modules] installed from the bundle: {string.Join(", ", installed)}");
        MainThread.Enqueue(() => ModuleService.LoadInstalled(installed));
    }
    private static List<string> WithDeps(string id) {
        List<string> ordered = [];
        Walk(id, ordered, new HashSet<string>(StringComparer.Ordinal));
        return ordered;
    }
    private static void Walk(string id, List<string> ordered, HashSet<string> seen) {
        if(id == null || !seen.Add(id) || !Has(id)) return;
        ModuleManifest manifest;
        try {
            manifest = ModuleManifest.Parse(File.ReadAllText(ManifestOf(id)), out _);
        } catch(Exception e) {
            Diag.Ignore(e);
            return;
        }
        if(manifest == null) return;
        foreach(string dep in manifest.Deps) Walk(dep, ordered, seen);
        if(!ordered.Contains(id)) ordered.Add(id);
    }
}
