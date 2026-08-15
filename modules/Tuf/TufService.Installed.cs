using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.UI;
namespace Quartz.Features.Tuf;
public sealed partial class TufService : IRuntimeService {
    internal void RecordInstalledLevel(TufLevel level) {
        if(disposed || index == null || level?.InstallFolder == null) return;
        index.Data.Record(level, level.InstallFolder);
        index.RequestSave();
        settings?.Data.RememberRoot(Path.GetDirectoryName(Path.GetFullPath(level.InstallFolder)));
        settings?.RequestSave();
    }
    private void AdoptOrphans() {
        if(index == null) return;
        TufInstallRoot root = downloads.ActiveRoot();
        bool changed = false;
        try {
            foreach(string dir in Directory.EnumerateDirectories(root.Path)) {
                if(!TufInstallPaths.IsLevelFolderName(Path.GetFileName(dir), out int id)) continue;
                if(index.Data.Find(id) != null) continue;
                string chart = TufArchive.SelectChart(dir);
                if(chart == null) continue;
                long stamp;
                try { stamp = Directory.GetCreationTimeUtc(dir).Ticks; } catch(Exception e) { Diag.Ignore(e); stamp = 0; }
                index.Data.Adopt(id, dir, stamp);
                changed = true;
            }
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not scan the level library: " + e.Message);
        }
        if(!changed) return;
        index.Data.SortEntries();
        settings?.Data.RememberRoot(root.Path);
        settings?.RequestSave();
        index.RequestSave();
    }
    private void LoadInstalled() {
        if(index == null) return;
        bool pruned = index.Data.PruneMissing();
        AdoptOrphans();
        if(pruned) index.RequestSave();
        BackfillInstalledInfo();
        RebuildInstalledList();
    }
    private void BackfillInstalledInfo() {
        if(index == null) return;
        List<(int Id, string Folder)> charts = [];
        List<int> missing = [];
        foreach(TufInstallEntry entry in index.Data.Entries) {
            if(string.IsNullOrEmpty(entry.Song) && chartProbed.Add(entry.Id)) charts.Add((entry.Id, entry.Folder));
            if(entry.NeedsInfo && missing.Count < MaxInfoProbes && infoProbed.Add(entry.Id)) missing.Add(entry.Id);
        }
        if(charts.Count > 0) ReadChartInfo(charts);
        if(missing.Count > 0) FetchMissingInfo(missing);
    }
    private async void ReadChartInfo(List<(int Id, string Folder)> pending) {
        List<(int Id, TufChartInfo Info)> found = await Task.Run(() => {
            List<(int, TufChartInfo)> results = [];
            foreach((int id, string folder) in pending) {
                TufChartInfo info = TufChartInfo.Read(TufArchive.SelectChart(folder));
                if(info != null) results.Add((id, info));
            }
            return results;
        }).ConfigureAwait(false);
        if(found.Count == 0) return;
        MainThread.Enqueue(() => ApplyChartInfo(found));
    }
    private void ApplyChartInfo(List<(int Id, TufChartInfo Info)> found) {
        if(disposed || index == null) return;
        bool changed = false;
        foreach((int id, TufChartInfo info) in found) {
            TufInstallEntry entry = index.Data.Find(id);
            if(entry != null && entry.ApplyChart(info)) changed = true;
        }
        if(!changed) return;
        InfoRevision++;
        index.RequestSave();
        if(ShowInstalled) RebuildInstalledList();
        else Notify();
    }
    private async void FetchMissingInfo(List<int> ids) {
        infoRequest ??= new CancellationTokenSource();
        CancellationToken token = infoRequest.Token;
        List<TufLevel> fetched = [];
        foreach(int id in ids) {
            if(disposed || token.IsCancellationRequested) return;
            try {
                TufLevel level = await api.FetchLevelAsync(id, token).ConfigureAwait(false);
                if(level != null) fetched.Add(level);
            } catch(OperationCanceledException e) {
                Diag.Ignore(e);
                return;
            } catch(Exception e) {
                MainCore.Log.Wrn($"[TUF] could not fetch info for level {id}: {e.Message}");
                int failed = id;
                MainThread.Enqueue(() => {
                    foreach(int pending in ids) if(pending != failed) infoProbed.Remove(pending);
                });
                break;
            }
        }
        if(fetched.Count == 0) return;
        MainThread.Enqueue(() => ApplyFetchedInfo(fetched));
    }
    private void ApplyFetchedInfo(List<TufLevel> fetched) {
        if(disposed || index == null) return;
        bool changed = false;
        foreach(TufLevel level in fetched) {
            TufInstallEntry entry = index.Data.Find(level.Id);
            if(entry != null && entry.ApplyLevel(level)) changed = true;
        }
        if(!changed) return;
        InfoRevision++;
        index.RequestSave();
        if(ShowInstalled) RebuildInstalledList();
        else Notify();
    }
    private void RebuildInstalledList() {
        if(index == null) return;
        InvalidateListRequest();
        levels.Clear();
        foreach(TufInstallEntry entry in index.Data.Entries) {
            if(!MatchesInstalledFilters(entry)) continue;
            TufLevel level = entry.ToLevel();
            level.InstallFolder = entry.Folder;
            level.InstalledAtUtc = entry.InstalledAtUtc;
            level.State = TufItemState.Load;
            levels.Add(level);
        }
        SortInstalled();
        HasMore = false;
        LoadingMore = false;
        appendFailed = false;
        Error = "";
        OfflineError = false;
        State = levels.Count == 0 ? TufListState.Empty : TufListState.Ready;
        Notify();
    }
    private bool MatchesInstalledFilters(TufInstallEntry entry) {
        if(!string.IsNullOrEmpty(Query)) {
            string needle = Query;
            bool hit = Contains(entry.Song, needle) || Contains(entry.Artist, needle)
                || Contains(entry.Creator, needle) || entry.Id.ToString() == needle;
            if(!hit) return false;
        }
        int rank = TufDifficultyFilter.RankOf(entry.Difficulty);
        if(rank >= 0) return rank >= DifficultyFilter.MinIndex && rank <= DifficultyFilter.MaxIndex;
        return DifficultyFilter.SelectedDifficulties.Count == 0
            || !TufDifficultyFilter.IsSpecialName(entry.Difficulty)
            || DifficultyFilter.IsSelected(entry.Difficulty);
    }
    private static bool Contains(string haystack, string needle) =>
        !string.IsNullOrEmpty(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    private void SortInstalled() {
        Comparison<TufLevel> compare = Sort switch {
            TufSort.Clears => (a, b) => a.Clears.CompareTo(b.Clears),
            TufSort.Likes => (a, b) => a.Likes.CompareTo(b.Likes),
            TufSort.Difficulty => (a, b) => InstalledRank(a).CompareTo(InstalledRank(b)),
            _ => (a, b) => a.InstalledAtUtc.CompareTo(b.InstalledAtUtc)
        };
        levels.Sort(compare);
        if(!Ascending) levels.Reverse();
    }
    private static int InstalledRank(TufLevel level) {
        int rank = TufDifficultyFilter.RankOf(level.Difficulty);
        return rank < 0 ? int.MaxValue : rank;
    }
    public void DeleteInstalled(TufLevel level) {
        if(disposed || level == null || index == null || IsBusy) return;
        TufInstallEntry entry = index.Data.Find(level.Id);
        string folder = level.InstallFolder ?? entry?.Folder;
        if(string.IsNullOrEmpty(folder)) return;
        bool removed = downloads.DeleteLevel(level.Id, folder, settings?.Data.KnownRoots);
        if(!removed) {
            level.Error = MainCore.Tr.Get("TUF_DELETE_FAILED", "Could not delete this level; see the log.");
            Notify();
            return;
        }
        index.Data.Remove(level.Id);
        index.RequestSave();
        if(ShowInstalled) LoadInstalled();
        else {
            level.InstallFolder = null;
            level.State = level.DownloadUri == null ? TufItemState.Unavailable : TufItemState.Download;
            level.Error = "";
            Notify();
        }
    }
}
