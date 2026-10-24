using Quartz.Compat.Interface;
using Quartz.Core;
namespace Quartz.Features.Tuf;
public sealed partial class TufService : IRuntimeService {
    private readonly Dictionary<int, int> snapshotCounts = [];
    public int SnapshotCount(int id) => snapshotCounts.TryGetValue(id, out int count) ? count : 0;
    public IReadOnlyList<TufSnapshot> Snapshots(int id) => TufRollback.List(RollbackRoot, id);
    private string RollbackRoot => downloads?.ActiveRoot().Path ?? MainCore.Paths.TufLevelsPath;
    internal void RefreshSnapshotCounts() {
        if(disposed) return;
        Dictionary<int, int> counts = TufRollback.Counts(RollbackRoot);
        snapshotCounts.Clear();
        foreach(KeyValuePair<int, int> pair in counts) snapshotCounts[pair.Key] = pair.Value;
    }
    internal void LabelNewSnapshot(int id, string fileId, string song, long installedAtUtc, bool hadInstall) {
        if(disposed || !hadInstall) return;
        string root = RollbackRoot;
        foreach(TufSnapshot snapshot in TufRollback.List(root, id)) {
            if(snapshot.HasMeta) continue;
            TufRollback.WriteMeta(root, snapshot.Stamp, id, fileId, song, installedAtUtc);
            break;
        }
        TufRollback.Prune(root, id);
        RefreshSnapshotCounts();
        InfoRevision++;
    }
    public void RestoreSnapshot(TufLevel level, long stamp) {
        if(disposed || level == null || index == null || IsBusy || stamp <= 0) return;
        TufInstallEntry entry = index.Data.Find(level.Id);
        if(entry == null) return;
        string root = RollbackRoot;
        string target = downloads.LevelFolder(level.Id);
        TufSnapshot restored = TufRollback.Restore(root, stamp, level.Id, target, out long archived, out string error);
        if(archived > 0)
            TufRollback.WriteMeta(root, archived, level.Id, entry.FileId, entry.Song, entry.InstalledAtUtc);
        if(restored == null) {
            level.Error = error == "missing"
                ? MainCore.Tr.Get("TUF_ROLLBACK_MISSING", "That rollback copy is no longer on disk.")
                : MainCore.Tr.Get("TUF_ROLLBACK_FAILED", "Could not roll this level back; see the log.");
            RefreshSnapshotCounts();
            InfoRevision++;
            Notify();
            return;
        }
        MainCore.Log.Msg($"[TUF] rolled level {level.Id} back to {stamp} ({restored.Folder})");
        index.Data.SetFolder(level.Id, restored.Folder);
        entry.FileId = restored.FileId;
        entry.SizeBytes = 0;
        sizeProbed.Remove(level.Id);
        index.RequestSave();
        level.InstallFolder = restored.Folder;
        level.SizeBytes = 0;
        level.Error = "";
        string remote = TufUpdateCheck.FileIdOf(entry.DownloadUrl);
        updateStates[level.Id] = entry.FileId.Length > 0 && remote.Length > 0
            && !string.Equals(entry.FileId, remote, StringComparison.Ordinal)
            ? TufUpdateState.Available : TufUpdateState.Unknown;
        TufRollback.Prune(root, level.Id);
        RefreshSnapshotCounts();
        ApplyUpdateStates();
        InfoRevision++;
        ScanSizes();
        if(ShowInstalled) LoadInstalled();
        else Notify();
    }
}
