using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features.Tuf;
public sealed partial class TufService : IRuntimeService {
    private const int UpdateNotifyEvery = 10;
    private readonly Dictionary<int, TufUpdateState> updateStates = [];
    private readonly HashSet<int> sizeProbed = [];
    private CancellationTokenSource updateRequest;
    private bool sizeScanRunning;
    private int updatingId;
    private string updatingFolder = "";
    public int UpdatesAvailable { get; private set; }
    public int UpdateCheckDone { get; private set; }
    public int UpdateCheckTotal { get; private set; }
    public bool CheckingUpdates => UpdateCheckTotal > 0;
    public long LibraryBytes { get; private set; }
    public TufUpdateState UpdateStateOf(int id) =>
        updateStates.TryGetValue(id, out TufUpdateState state) ? state : TufUpdateState.Unknown;
    public void CheckUpdates() {
        if(index == null) return;
        StartUpdateCheck(index.Data.Entries.Select(e => e.Id).ToList());
    }
    public void CheckUpdate(TufLevel level) {
        if(level == null || index?.Data.Find(level.Id) == null) return;
        StartUpdateCheck([level.Id]);
    }
    private void StartUpdateCheck(List<int> ids) {
        if(disposed || index == null || CheckingUpdates || ids.Count == 0) return;
        updateRequest?.Cancel();
        updateRequest?.Dispose();
        updateRequest = new CancellationTokenSource();
        UpdateCheckDone = 0;
        UpdateCheckTotal = ids.Count;
        foreach(int id in ids)
            if(UpdateStateOf(id) != TufUpdateState.Updating) updateStates[id] = TufUpdateState.Checking;
        ApplyUpdateStates();
        InfoRevision++;
        Notify();
        RunUpdateCheck(ids, updateRequest.Token);
    }
    private async void RunUpdateCheck(List<int> ids, CancellationToken token) {
        foreach(int id in ids) {
            if(disposed || token.IsCancellationRequested) break;
            TufLevel remote = null;
            try {
                remote = await api.FetchLevelAsync(id, token).ConfigureAwait(false);
            } catch(OperationCanceledException e) {
                Diag.Ignore(e);
                break;
            } catch(Exception e) {
                MainCore.Log.Wrn($"[TUF] could not check level {id} for updates: {e.Message}");
            }
            TufLevel fetched = remote;
            MainThread.Enqueue(() => ApplyUpdateResult(id, fetched, token));
            try {
                await Task.Delay(60, token).ConfigureAwait(false);
            } catch(OperationCanceledException e) {
                Diag.Ignore(e);
                break;
            }
        }
        MainThread.Enqueue(() => FinishUpdateCheck(token));
    }
    private void ApplyUpdateResult(int id, TufLevel remote, CancellationToken token) {
        if(disposed || index == null || token.IsCancellationRequested) return;
        UpdateCheckDone++;
        TufInstallEntry entry = index.Data.Find(id);
        if(entry != null && UpdateStateOf(id) != TufUpdateState.Updating) {
            TufUpdateState state = TufUpdateCheck.Decide(entry, remote);
            updateStates[id] = state;
            string fileId = TufUpdateCheck.RemoteFileId(remote);
            bool changed = false;
            if(state == TufUpdateState.UpToDate && entry.FileId.Length == 0 && fileId.Length > 0) {
                entry.FileId = fileId;
                changed = true;
            }
            string link = remote?.DownloadUri?.ToString() ?? "";
            if(link.Length > 0 && !string.Equals(entry.DownloadUrl, link, StringComparison.Ordinal)) {
                entry.DownloadUrl = link;
                changed = true;
            }
            if(changed) index.RequestSave();
            foreach(TufLevel level in levels)
                if(level.Id == id) level.SetSource(remote?.DownloadUri, fileId);
        }
        ApplyUpdateStates();
        if(UpdateCheckDone % UpdateNotifyEvery != 0) return;
        InfoRevision++;
        Notify();
    }
    private void FinishUpdateCheck(CancellationToken token) {
        if(disposed || token.IsCancellationRequested) return;
        UpdateCheckTotal = 0;
        UpdateCheckDone = 0;
        ApplyUpdateStates();
        InfoRevision++;
        Notify();
    }
    private void ApplyUpdateStates() {
        if(index == null) return;
        UpdatesAvailable = CountAvailable();
        foreach(TufLevel level in levels) level.UpdateState = UpdateStateOf(level.Id);
    }
    private int CountAvailable() {
        if(index == null) return 0;
        int available = 0;
        foreach(KeyValuePair<int, TufUpdateState> pair in updateStates)
            if(pair.Value == TufUpdateState.Available && index.Data.Find(pair.Key) != null) available++;
        return available;
    }
    internal void NoteUpdateState(TufInstallEntry entry, TufLevel remote) {
        if(entry == null || remote == null) return;
        if(UpdateStateOf(entry.Id) == TufUpdateState.Updating) {
            remote.UpdateState = TufUpdateState.Updating;
            return;
        }
        TufUpdateState state = TufUpdateCheck.Decide(entry, remote);
        if(state != TufUpdateState.Unknown) updateStates[entry.Id] = state;
        remote.UpdateState = state;
        UpdatesAvailable = CountAvailable();
    }
    public void UpdateLevel(TufLevel level) {
        if(disposed || level == null || index == null || IsBusy || actions == null) return;
        TufInstallEntry entry = index.Data.Find(level.Id);
        if(entry == null || level.DownloadUri == null) return;
        updatingId = level.Id;
        updatingFolder = entry.Folder;
        updateStates[level.Id] = TufUpdateState.Updating;
        level.UpdateState = TufUpdateState.Updating;
        actions.UpdateLevel(level);
    }
    private void OnActionFinished(TufLevel level, bool success) {
        if(disposed || level == null || level.Id != updatingId) return;
        int id = updatingId;
        string previous = updatingFolder;
        updatingId = 0;
        updatingFolder = "";
        if(!success) {
            updateStates[id] = TufUpdateState.Unknown;
            ApplyUpdateStates();
            return;
        }
        updateStates[id] = TufUpdateState.UpToDate;
        string installed = level.InstallFolder ?? "";
        if(previous.Length > 0 && installed.Length > 0
            && !string.Equals(Path.GetFullPath(previous), Path.GetFullPath(installed), PathComparison))
            downloads.DeleteLevel(id, previous, settings?.Data.KnownRoots);
        sizeProbed.Remove(id);
        TufInstallEntry entry = index?.Data.Find(id);
        if(entry != null) entry.SizeBytes = 0;
        ApplyUpdateStates();
        ScanSizes();
    }
    public void EnsureLibraryMeasured() => ScanSizes();
    private void ScanSizes() {
        if(disposed || index == null) return;
        RecomputeLibraryBytes();
        if(sizeScanRunning) return;
        List<(int Id, string Folder)> pending = index.Data.Entries
            .Where(e => e.SizeBytes <= 0 && sizeProbed.Add(e.Id))
            .Select(e => (e.Id, e.Folder))
            .ToList();
        if(pending.Count == 0) return;
        sizeScanRunning = true;
        MeasureSizes(pending);
    }
    private async void MeasureSizes(List<(int Id, string Folder)> pending) {
        List<(int Id, long Size)> sizes = await Task.Run(() => {
            List<(int, long)> measured = [];
            foreach((int id, string folder) in pending) measured.Add((id, TufUpdateCheck.FolderSize(folder)));
            return measured;
        }).ConfigureAwait(false);
        MainThread.Enqueue(() => ApplySizes(sizes));
    }
    private void ApplySizes(List<(int Id, long Size)> sizes) {
        sizeScanRunning = false;
        if(disposed || index == null) return;
        bool changed = false;
        foreach((int id, long size) in sizes) {
            TufInstallEntry entry = index.Data.Find(id);
            if(entry == null || size <= 0 || entry.SizeBytes == size) continue;
            entry.SizeBytes = size;
            changed = true;
        }
        RecomputeLibraryBytes();
        foreach(TufLevel level in levels) level.SizeBytes = index.Data.Find(level.Id)?.SizeBytes ?? 0;
        if(changed) {
            index.RequestSave();
            InfoRevision++;
        }
        Notify();
    }
    private void RecomputeLibraryBytes() {
        long total = 0;
        foreach(TufInstallEntry entry in index.Data.Entries) total += Math.Max(0, entry.SizeBytes);
        LibraryBytes = total;
    }
}
