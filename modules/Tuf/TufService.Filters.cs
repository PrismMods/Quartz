using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.UI;
namespace Quartz.Features.Tuf;
public sealed partial class TufService : IRuntimeService {
    public void SetQuery(string value) {
        string query = TufInput.NormalizeQuery(value);
        if(query == Query) return;
        Query = query;
        InvalidateListRequest();
        CancelDebounce();
        levels.Clear();
        HasMore = false;
        LoadingMore = false;
        nextOffset = 0;
        appendFailed = false;
        if(ShowInstalled) {
            RebuildInstalledList();
            return;
        }
        State = TufListState.Loading;
        Error = "";
        OfflineError = false;
        Notify();
        debounce = new CancellationTokenSource();
        DebouncedRefresh(debounce.Token);
    }
    private async void DebouncedRefresh(CancellationToken token) {
        try {
            await Task.Delay(300, token);
            if(!token.IsCancellationRequested) {
                if(debounce != null && debounce.Token == token) {
                    debounce.Dispose();
                    debounce = null;
                }
                Fetch(false);
            }
        } catch(OperationCanceledException e) { Diag.Ignore(e); }
    }
    public void SetSort(TufSort value) {
        if(Sort == value) return;
        Sort = value;
        SaveSettings();
        Refresh();
    }
    public void ToggleAscending() {
        Ascending = !Ascending;
        SaveSettings();
        Refresh();
    }
    public void ToggleInstalled() {
        ShowInstalled = !ShowInstalled;
        InvalidateListRequest();
        CancelDebounce();
        levels.Clear();
        HasMore = false;
        LoadingMore = false;
        nextOffset = 0;
        appendFailed = false;
        Refresh();
    }
    public void ShowInstalledLevels() {
        if(!ShowInstalled) ToggleInstalled();
        else Refresh();
    }
    public void SetDifficultyRange(int minIndex, int maxIndex) =>
        SetDifficultyFilter(DifficultyFilter.WithRange(minIndex, maxIndex));
    public void ToggleSpecialDifficulty(string name) =>
        SetDifficultyFilter(DifficultyFilter.Toggle(name));
    public void SetQuantumRange(int minIndex, int maxIndex) {
        int last = TufDifficultyFilter.QuantumNames.Count - 1;
        quantumMinIndex = Math.Clamp(minIndex, 0, last);
        quantumMaxIndex = Math.Clamp(maxIndex, 0, last);
        if(quantumMinIndex > quantumMaxIndex) (quantumMinIndex, quantumMaxIndex) = (quantumMaxIndex, quantumMinIndex);
        SetDifficultyFilter(DifficultyFilter.WithQuantumRange(quantumMinIndex, quantumMaxIndex));
    }
    public void ClearQuantum() => SetDifficultyFilter(DifficultyFilter.WithoutQuantum());
    public void ResetDifficultyFilter() => SetDifficultyFilter(TufDifficultyFilter.AllRanked);
    private void SetDifficultyFilter(TufDifficultyFilter filter) {
        if(DifficultyFilter.Equals(filter)) return;
        DifficultyFilter = filter;
        SaveSettings();
        levels.Clear();
        HasMore = false;
        nextOffset = 0;
        Refresh();
    }
    public bool ShowPreviews => settings?.Data.ShowPreviews ?? true;
    public void SetShowPreviews(bool value) {
        if(settings == null || settings.Data.ShowPreviews == value) return;
        settings.Data.ShowPreviews = value;
        settings.RequestSave();
        if(!value) TufPreviewCache.Clear();
        Notify();
    }
    public bool GridView => settings?.Data.GridView ?? false;
    public void SetGridView(bool value) {
        if(settings == null || settings.Data.GridView == value) return;
        settings.Data.GridView = value;
        settings.RequestSave();
        Notify();
    }
    public bool LinkTufHelperLite => settings?.Data.LinkTufHelperLite ?? false;
    public void SetLinkTufHelperLite(bool value) {
        if(settings == null || settings.Data.LinkTufHelperLite == value) return;
        settings.Data.LinkTufHelperLite = value;
        settings.Data.RememberRoot(ResolveInstallRoot().Path);
        settings.RequestSave();
        if(ShowInstalled) LoadInstalled();
        else Notify();
    }
}
