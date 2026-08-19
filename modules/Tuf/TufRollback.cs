#nullable enable
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.Tuf;
public sealed class TufSnapshot {
    public long Stamp;
    public int Id;
    public string Folder = "";
    public string FileId = "";
    public string Song = "";
    public long SizeBytes;
    public bool HasMeta;
    public DateTime LocalTime => DateTimeOffset.FromUnixTimeSeconds(Stamp).ToLocalTime().DateTime;
}
public static partial class TufRollback {
    private const int MaxStampBump = 60;
    public static long Archive(string? levelFolder, string? levelsRoot, int id) {
        if(id <= 0 || string.IsNullOrWhiteSpace(levelFolder) || string.IsNullOrWhiteSpace(levelsRoot)) return 0;
        try {
            string source = Path.GetFullPath(levelFolder);
            if(!Directory.Exists(source)) return 0;
            if((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) return 0;
            long stamp = Now();
            string target = SnapshotFolder(levelsRoot, stamp, id);
            for(int bump = 0; bump < MaxStampBump && Directory.Exists(target); bump++)
                target = SnapshotFolder(levelsRoot, ++stamp, id);
            if(Directory.Exists(target)) return 0;
            Directory.CreateDirectory(StampFolder(levelsRoot, stamp));
            Directory.Move(source, target);
            return stamp;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] could not keep a rollback copy of level {id}: {e.Message}");
            return 0;
        }
    }
    public static void WriteMeta(string levelsRoot, long stamp, int id, string? fileId, string? song, long installedAtUtc) {
        if(stamp <= 0 || id <= 0) return;
        try {
            JObject meta = new() {
                ["FileId"] = fileId ?? "",
                ["Song"] = song ?? "",
                ["InstalledAtUtc"] = installedAtUtc
            };
            File.WriteAllText(MetaFile(levelsRoot, stamp, id), meta.ToString());
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] could not describe the rollback copy of level {id}: {e.Message}");
        }
    }
    public static List<TufSnapshot> List(string? levelsRoot, int id) {
        List<TufSnapshot> snapshots = [];
        if(string.IsNullOrWhiteSpace(levelsRoot) || id <= 0) return snapshots;
        try {
            string root = Root(levelsRoot);
            if(!Directory.Exists(root)) return snapshots;
            foreach(string stampDir in Directory.EnumerateDirectories(root)) {
                if(!IsStampName(Path.GetFileName(stampDir), out long stamp)) continue;
                string folder = Path.Combine(stampDir, id.ToString());
                if(!Directory.Exists(folder)) continue;
                TufSnapshot snapshot = new() { Stamp = stamp, Id = id, Folder = folder };
                ReadMeta(Path.Combine(stampDir, id + ".json"), snapshot);
                snapshots.Add(snapshot);
            }
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] could not read the rollback copies of level {id}: {e.Message}");
        }
        snapshots.Sort((a, b) => b.Stamp.CompareTo(a.Stamp));
        return snapshots;
    }
    public static Dictionary<int, int> Counts(string? levelsRoot) {
        Dictionary<int, int> counts = [];
        if(string.IsNullOrWhiteSpace(levelsRoot)) return counts;
        try {
            string root = Root(levelsRoot);
            if(!Directory.Exists(root)) return counts;
            foreach(string stampDir in Directory.EnumerateDirectories(root)) {
                if(!IsStampName(Path.GetFileName(stampDir), out _)) continue;
                foreach(string levelDir in Directory.EnumerateDirectories(stampDir)) {
                    if(!TufInstallPaths.IsLevelFolderName(Path.GetFileName(levelDir), out int id)) continue;
                    counts[id] = counts.TryGetValue(id, out int seen) ? seen + 1 : 1;
                }
            }
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not scan the rollback folder: " + e.Message);
        }
        return counts;
    }
    public static bool Delete(string levelsRoot, long stamp, int id) {
        string folder = SnapshotFolder(levelsRoot, stamp, id);
        if(!IsOwnedSnapshotFolder(folder, levelsRoot, id)) {
            MainCore.Log.Wrn($"[TUF] refused to delete '{folder}': not a rollback copy Quartz owns.");
            return false;
        }
        try {
            Directory.Delete(folder, true);
            CleanupMeta(levelsRoot, stamp, id);
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] could not delete the rollback copy of level {id}: {e.Message}");
            return false;
        }
    }
    public static int Prune(string levelsRoot, int id, int keep = KeepPerLevel) {
        List<TufSnapshot> snapshots = List(levelsRoot, id);
        int removed = 0;
        for(int i = keep; i < snapshots.Count; i++)
            if(Delete(levelsRoot, snapshots[i].Stamp, id)) removed++;
        return removed;
    }
    public static TufSnapshot? Restore(string levelsRoot, long stamp, int id, string installFolder,
        out long archivedStamp, out string error) {
        error = "";
        archivedStamp = 0;
        string folder = SnapshotFolder(levelsRoot, stamp, id);
        if(!IsOwnedSnapshotFolder(folder, levelsRoot, id)) {
            error = "missing";
            return null;
        }
        TufSnapshot snapshot = new() { Stamp = stamp, Id = id, Folder = folder };
        ReadMeta(MetaFile(levelsRoot, stamp, id), snapshot);
        try {
            string target = Path.GetFullPath(installFolder);
            archivedStamp = Archive(target, levelsRoot, id);
            if(Directory.Exists(target)) {
                error = "occupied";
                return null;
            }
            string parent = Path.GetDirectoryName(target) ?? "";
            if(parent.Length > 0) Directory.CreateDirectory(parent);
            try {
                Directory.Move(folder, target);
            } catch(IOException e) {
                Diag.Ignore(e);
                TufDownloadService.CopyDirectory(folder, target, CancellationToken.None);
                Directory.Delete(folder, true);
            }
            CleanupMeta(levelsRoot, stamp, id);
            snapshot.Folder = target;
            return snapshot;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] could not roll level {id} back to {stamp}: {e}");
            if(archivedStamp > 0 && PutBack(levelsRoot, archivedStamp, id, installFolder)) archivedStamp = 0;
            error = e.Message;
            return null;
        }
    }
    public static void MoveTree(string fromRoot, string toRoot) {
        try {
            string from = Root(fromRoot);
            string to = Root(toRoot);
            if(!Directory.Exists(from) || string.Equals(from, to, PathComparison)) return;
            if(Directory.Exists(to)) {
                MainCore.Log.Wrn($"[TUF] rollback copies stayed in '{from}': the new library already has some.");
                return;
            }
            Directory.CreateDirectory(Path.GetFullPath(toRoot));
            Directory.Move(from, to);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] rollback copies stayed in '{Root(fromRoot)}' ({e.Message}); "
                + "delete that folder by hand to reclaim the space.");
        }
    }
    private static bool PutBack(string levelsRoot, long stamp, int id, string installFolder) {
        try {
            string archived = SnapshotFolder(levelsRoot, stamp, id);
            string target = Path.GetFullPath(installFolder);
            if(!Directory.Exists(archived) || Directory.Exists(target)) return false;
            Directory.Move(archived, target);
            CleanupMeta(levelsRoot, stamp, id);
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] level {id} is now only in the rollback folder ({e.Message}); "
                + "roll it back by hand or download it again.");
            return false;
        }
    }
    private static void ReadMeta(string path, TufSnapshot snapshot) {
        snapshot.SizeBytes = TufUpdateCheck.FolderSize(snapshot.Folder);
        try {
            if(!File.Exists(path)) return;
            JObject meta = JObject.Parse(File.ReadAllText(path));
            snapshot.HasMeta = true;
            snapshot.FileId = TufInput.CapDisplay(meta["FileId"]?.Value<string>(), "", 64);
            snapshot.Song = TufInput.CapDisplay(meta["Song"]?.Value<string>(), "");
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void CleanupMeta(string levelsRoot, long stamp, int id) {
        try {
            string meta = MetaFile(levelsRoot, stamp, id);
            if(File.Exists(meta)) File.Delete(meta);
            string stampDir = StampFolder(levelsRoot, stamp);
            if(Directory.Exists(stampDir) && !Directory.EnumerateFileSystemEntries(stampDir).Any())
                Directory.Delete(stampDir);
        } catch(Exception e) { Diag.Ignore(e); }
    }
}
