using Quartz.Async;
using Quartz.Core;
using Quartz.UI;
namespace Quartz.Features.Tuf;
internal sealed class TufLevelActionRunner {
    private readonly IReadOnlyList<TufLevel> owner;
    private readonly TufDownloadService downloads;
    private readonly TufLevelLauncher launcher;
    private readonly Action notify;
    private readonly Action<TufLevel> installed;
    private CancellationTokenSource actionRequest;
    private int activeLevelId;
    private bool disposed;
    public bool IsBusy => activeLevelId != 0;
    public TufLevelActionRunner(IReadOnlyList<TufLevel> owner, TufDownloadService downloads,
        TufLevelLauncher launcher, Action notify, Action<TufLevel> installed = null) {
        this.owner = owner;
        this.downloads = downloads;
        this.launcher = launcher;
        this.notify = notify;
        this.installed = installed;
    }
    public void Act(TufLevel level) {
        if(level == null || IsBusy
            || level.State is TufItemState.Downloading or TufItemState.Extracting or TufItemState.Loading) return;
        if(level.State == TufItemState.ChooseChart) {
            ExitChoose(level);
            return;
        }
        activeLevelId = level.Id;
        if(downloads.TryGetCachedChart(level.Id, level.InstallFolder, out string cached)) {
            IReadOnlyList<string> charts = downloads.ListCachedCharts(level.Id, level.InstallFolder);
            if(charts.Count > 1) EnterChoose(level, charts);
            else Launch(level, cached);
            return;
        }
        if(level.DownloadUri == null) {
            activeLevelId = 0;
            switch(TufMainLevel.Resolve(level, out string codeOrUrl)) {
                case TufMainLevel.TufMainAction.Play: LaunchMainLevel(level, codeOrUrl); break;
                case TufMainLevel.TufMainAction.BuyDlc: TufMainLevel.OpenStore(codeOrUrl); break;
            }
            return;
        }
        actionRequest?.Cancel();
        actionRequest?.Dispose();
        actionRequest = new CancellationTokenSource();
        Update(level, TufItemState.Downloading, 0f, "");
        Download(level, actionRequest.Token);
    }
    private void LaunchMainLevel(TufLevel level, string code) {
        MainCore.Log.Msg($"[TUF] opening base-game level {code} for #{level.Id}");
        if(TufMainLevel.Launch(code)) {
            UICore.Close(true);
            return;
        }
        level.Error = MainCore.Tr.Get("TUF_MAIN_LAUNCH_FAILED", "Could not open the base-game level.");
        notify();
    }
    public void LaunchChart(TufLevel level, string chart) {
        if(level == null || IsBusy || level.State != TufItemState.ChooseChart) return;
        if(level.Charts == null || !level.Charts.Contains(chart, StringComparer.Ordinal)) return;
        activeLevelId = level.Id;
        ExitChoose(level, notify: false);
        Launch(level, chart);
    }
    private void EnterChoose(TufLevel level, IReadOnlyList<string> charts) {
        activeLevelId = 0;
        foreach(TufLevel other in owner)
            if(!ReferenceEquals(other, level) && other.State == TufItemState.ChooseChart) ExitChoose(other, notify: false);
        level.State = TufItemState.ChooseChart;
        level.Progress = 0f;
        level.Error = "";
        level.Charts = charts;
        level.ChartsRoot = level.InstallFolder ?? downloads.LevelFolder(level.Id);
        notify();
    }
    private void ExitChoose(TufLevel level, bool notify = true) {
        level.Charts = null;
        level.ChartsRoot = null;
        if(level.State == TufItemState.ChooseChart) level.State = TufItemState.Load;
        if(notify) this.notify();
    }
    private async void Download(TufLevel level, CancellationToken token) {
        int lastPercent = -2;
        try {
            await downloads.DownloadAsync(level, (state, progress) => {
                int percent = progress < 0 ? -1 : (int)(progress * 100f);
                if(state == TufItemState.Downloading && percent >= 0 && lastPercent >= 0
                    && percent / 5 == lastPercent / 5) return;
                lastPercent = percent;
                MainThread.Enqueue(() => Update(level, state, progress, ""));
            }, token);
            MainThread.Enqueue(() => {
                if(disposed || token.IsCancellationRequested) return;
                installed?.Invoke(level);
                FinishAction(level, TufItemState.Load, "");
            });
        } catch(OperationCanceledException) {
            MainThread.Enqueue(() => FinishAction(level, TufItemState.Download, ""));
        }
        catch(Exception e) {
            MainThread.Enqueue(() => {
                MainCore.Log.Wrn($"[TUF] level {level.Id} could not be downloaded or extracted: {e}");
                FinishAction(level, TufItemState.Retry, e.Message);
            });
        }
    }
    private void Launch(TufLevel level, string chart) {
        if(disposed) return;
        Update(level, TufItemState.Loading, 1f, "");
        launcher.Launch(chart, (success, error) => MainThread.Enqueue(() => {
            if(disposed) return;
            if(!success) {
                MainCore.Log.Wrn("[TUF] automatic play failed: " + error);
                UICore.Open(true);
            }
            FinishAction(level, success ? TufItemState.Load : TufItemState.Retry, error);
        }));
    }
    private void FinishAction(TufLevel level, TufItemState state, string error) {
        activeLevelId = 0;
        if(disposed) return;
        if(owner.Contains(level)) {
            level.State = state;
            level.Progress = 0f;
            level.Error = error ?? "";
        }
        notify();
    }
    private void Update(TufLevel level, TufItemState state, float progress, string error) {
        if(disposed || !owner.Contains(level)) return;
        level.State = state;
        level.Progress = progress;
        level.Error = error ?? "";
        notify();
    }
    public void Cancel() => actionRequest?.Cancel();
    public void Dispose() {
        disposed = true;
        actionRequest?.Cancel();
        actionRequest?.Dispose();
        actionRequest = null;
        activeLevelId = 0;
    }
}
