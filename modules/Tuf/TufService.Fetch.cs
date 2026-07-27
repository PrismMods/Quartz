using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.UI;
namespace Quartz.Features.Tuf;
public sealed partial class TufService : IRuntimeService {
    private async void Fetch(bool append) {
        listRequest?.Cancel();
        listRequest?.Dispose();
        listRequest = new CancellationTokenSource();
        CancellationToken token = listRequest.Token;
        int generation = ++listGeneration;
        string query = Query;
        TufSort sort = Sort;
        bool ascending = Ascending;
        TufDifficultyFilter filter = DifficultyFilter;
        if(append) {
            LoadingMore = true;
            appendFailed = false;
        }
        else {
            appendFailed = false;
            State = TufListState.Loading;
            Error = "";
        }
        OfflineError = false;
        Notify();
        try {
            TufPage page = await api.FetchAsync(query, sort, ascending, append ? nextOffset : 0, filter, token);
            MainThread.Enqueue(() => ApplyPage(page, append, token, generation, query, sort, ascending, filter));
        } catch(OperationCanceledException) when(token.IsCancellationRequested) { }
        catch(Exception e) {
            bool offline = e is OperationCanceledException || TufNetworkPolicy.IsOfflineError(e);
            string message = e is OperationCanceledException
                ? MainCore.Tr.Get("TUF_TIMEOUT", "The request to TUF timed out.")
                : e.Message;
            MainThread.Enqueue(() => {
                if(!RequestIsCurrent(token, generation, query, sort, ascending, filter)) return;
                LoadingMore = false;
                appendFailed = append;
                State = TufListState.Error;
                Error = message;
                OfflineError = offline;
                Notify();
            });
        }
    }
    private void ApplyPage(TufPage page, bool append, CancellationToken token, int generation,
        string query, TufSort sort, bool ascending, TufDifficultyFilter filter) {
        if(!RequestIsCurrent(token, generation, query, sort, ascending, filter)) return;
        if(!append) {
            levels.Clear();
            nextOffset = 0;
        }
        nextOffset += page.ConsumedCount;
        HashSet<int> existing = levels.Select(x => x.Id).ToHashSet();
        foreach(TufLevel level in page.Results) {
            if(existing.Add(level.Id)) {
                MarkIfInstalled(level);
                levels.Add(level);
            }
        }
        HasMore = page.HasMore && page.ConsumedCount > 0;
        LoadingMore = false;
        appendFailed = false;
        State = levels.Count == 0 ? TufListState.Empty : TufListState.Ready;
        Notify();
    }
    internal void MarkIfInstalled(TufLevel level) {
        if(disposed || index == null || level == null) return;
        TufInstallEntry entry = index.Data.Find(level.Id);
        if(entry != null) {
            if(downloads.TryGetCachedChart(level.Id, entry.Folder, out _)) {
                level.State = TufItemState.Load;
                level.InstallFolder = entry.Folder;
                level.InstalledAtUtc = entry.InstalledAtUtc;
                if(string.IsNullOrEmpty(entry.Song)) {
                    index.Data.Record(level, entry.Folder);
                    index.RequestSave();
                }
                return;
            }
            index.Data.Remove(level.Id);
            index.RequestSave();
        }
        if(downloads.TryGetCachedChart(level.Id, out _)) {
            level.State = TufItemState.Load;
            level.InstallFolder = downloads.LevelFolder(level.Id);
            RecordInstalledLevel(level);
        }
    }
    private bool RequestIsCurrent(CancellationToken token, int generation, string query,
        TufSort sort, bool ascending, TufDifficultyFilter filter) =>
        !token.IsCancellationRequested && !disposed && generation == listGeneration
        && query == Query && sort == Sort && ascending == Ascending && filter.Equals(DifficultyFilter);
    private void InvalidateListRequest() {
        listRequest?.Cancel();
        listGeneration++;
    }
    private void CancelDebounce() {
        debounce?.Cancel();
        debounce?.Dispose();
        debounce = null;
    }
}
