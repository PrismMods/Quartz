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
    private readonly List<(TufLevel Level, bool Force, TufItemState Prior)> pending = [];
    private CancellationTokenSource actionRequest;
    public Action<TufLevel, bool> Finished;
    private TufLevel downloading;
    private int launchingId;
    private bool disposed;
    public bool IsBusy => downloading != null || launchingId != 0;
    public bool IsLaunching => launchingId != 0;
    public int QueueCount => pending.Count;
    public TufLevelActionRunner(IReadOnlyList<TufLevel> owner, TufDownloadService downloads,
        TufLevelLauncher launcher, Action notify, Action<TufLevel> installed = null) {
        this.owner = owner;
        this.downloads = downloads;
        this.launcher = launcher;
        this.notify = notify;
        this.installed = installed;
    }
    public int QueuePosition(int id) {
        for(int i = 0; i < pending.Count; i++) if(pending[i].Level.Id == id) return i + 1;
        return 0;
    }
    public void SyncState(TufLevel level) {
        if(level == null) return;
        if(downloading != null && downloading.Id == level.Id) {
            level.State = downloading.State;
            level.Progress = downloading.Progress;
            level.Error = downloading.Error;
        } else if(QueuePosition(level.Id) > 0) level.State = TufItemState.Queued;
    }
    public void Act(TufLevel level) {
        if(level == null || IsLaunching
            || level.State is TufItemState.Downloading or TufItemState.Extracting or TufItemState.Loading) return;
        if(level.State == TufItemState.Queued) {
            Dequeue(level);
            return;
        }
        if(level.State == TufItemState.ChooseChart) {
            ExitChoose(level);
            return;
        }
        if(downloads.TryGetCachedChart(level.Id, level.InstallFolder, out string cached)) {
            IReadOnlyList<string> charts = downloads.ListCachedCharts(level.Id, level.InstallFolder);
            if(charts.Count > 1) EnterChoose(level, charts);
            else Launch(level, cached);
            return;
        }
        if(level.DownloadUri == null) {
            switch(TufMainLevel.Resolve(level, out string codeOrUrl)) {
                case TufMainLevel.TufMainAction.Play: LaunchMainLevel(level, codeOrUrl); break;
                case TufMainLevel.TufMainAction.BuyDlc: TufMainLevel.OpenStore(codeOrUrl); break;
            }
            return;
        }
        Request(level, false);
    }
    public void UpdateLevel(TufLevel level) {
        if(level == null || IsLaunching || level.DownloadUri == null
            || level.State is TufItemState.Downloading or TufItemState.Extracting or TufItemState.Loading or TufItemState.Queued) return;
        if(level.State == TufItemState.ChooseChart) ExitChoose(level, notify: false);
        Request(level, true);
    }
    private void Request(TufLevel level, bool force) {
        if(downloading != null) {
            if(downloading.Id == level.Id || QueuePosition(level.Id) > 0) return;
            pending.Add((level, force, level.State));
            Update(level, TufItemState.Queued, 0f, "");
            return;
        }
        StartDownload(level, force);
    }
    private void Dequeue(TufLevel level) {
        int at = pending.FindIndex(p => p.Level.Id == level.Id);
        if(at < 0) return;
        TufItemState prior = pending[at].Prior;
        pending.RemoveAt(at);
        Apply(level, prior == TufItemState.Queued ? TufItemState.Download : prior, 0f, "");
        Finished?.Invoke(level, false);
        notify();
    }
    private void StartNext() {
        if(disposed || downloading != null || pending.Count == 0) return;
        (TufLevel stored, bool force, _) = pending[0];
        pending.RemoveAt(0);
        TufLevel current = stored;
        foreach(TufLevel other in owner) if(other.Id == stored.Id) { current = other; break; }
        current.InstallFolder ??= stored.InstallFolder;
        StartDownload(current, force);
    }
    private void StartDownload(TufLevel level, bool force) {
        actionRequest?.Cancel();
        actionRequest?.Dispose();
        actionRequest = new CancellationTokenSource();
        downloading = level;
        Update(level, TufItemState.Downloading, 0f, "");
        Download(level, actionRequest.Token, force);
    }
    private void LaunchMainLevel(TufLevel level, string code) {
        MainCore.Log.Msg($"[TUF] opening base-game level {code} for #{level.Id}");
        Quartz.Features.AprilFools.QuizGate.GateMain(code, level.Difficulty, () => {
            if(disposed) return;
            if(TufMainLevel.Launch(code)) {
                UICore.Close(true);
                return;
            }
            level.Error = MainCore.Tr.Get("TUF_MAIN_LAUNCH_FAILED", "Could not open the base-game level.");
            notify();
        });
    }
    public void LaunchChart(TufLevel level, string chart) {
        if(level == null || IsLaunching || level.State != TufItemState.ChooseChart) return;
        if(level.Charts == null || !level.Charts.Contains(chart, StringComparer.Ordinal)) return;
        ExitChoose(level, notify: false);
        Launch(level, chart);
    }
    private void EnterChoose(TufLevel level, IReadOnlyList<string> charts) {
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
    private async void Download(TufLevel level, CancellationToken token, bool force) {
        int lastPercent = -2;
        try {
            await downloads.DownloadAsync(level, (state, progress) => {
                int percent = progress < 0 ? -1 : (int)(progress * 100f);
                if(state == TufItemState.Downloading && percent >= 0 && lastPercent >= 0
                    && percent / 5 == lastPercent / 5) return;
                lastPercent = percent;
                MainThread.Enqueue(() => Update(level, state, progress, ""));
            }, token, force);
            MainThread.Enqueue(() => {
                if(disposed || token.IsCancellationRequested) return;
                installed?.Invoke(level);
                FinishAction(level, TufItemState.Load, "", true);
            });
        } catch(OperationCanceledException e) {
            Diag.Ignore(e);
            MainThread.Enqueue(() => FinishAction(level, TufItemState.Download, "", true));
        }
        catch(Exception e) {
            MainThread.Enqueue(() => {
                MainCore.Log.Wrn($"[TUF] level {level.Id} could not be downloaded or extracted: {e}");
                FinishAction(level, TufItemState.Retry, e.Message, true);
            });
        }
    }
    private void Launch(TufLevel level, string chart) {
        if(disposed) return;
        launchingId = level.Id;
        Update(level, TufItemState.Loading, 1f, "");
        Quartz.Features.AprilFools.QuizGate.GateChart(chart, level.Difficulty, () => {
            if(disposed) return;
            launcher.Launch(chart, (success, error) => MainThread.Enqueue(() => {
                if(disposed) return;
                bool aborted = !success && string.IsNullOrEmpty(error);
                if(!success) {
                    if(!aborted) MainCore.Log.Wrn("[TUF] automatic play failed: " + error);
                    UICore.Open(true);
                }
                FinishAction(level, success || aborted ? TufItemState.Load : TufItemState.Retry, error, false);
            }));
        });
    }
    private void FinishAction(TufLevel level, TufItemState state, string error, bool download) {
        if(download) downloading = null;
        else launchingId = 0;
        if(disposed) return;
        Apply(level, state, 0f, error);
        Finished?.Invoke(level, state != TufItemState.Retry);
        if(download) StartNext();
        notify();
    }
    private void Update(TufLevel level, TufItemState state, float progress, string error) {
        if(disposed) return;
        Apply(level, state, progress, error);
        notify();
    }
    private void Apply(TufLevel level, TufItemState state, float progress, string error) {
        level.State = state;
        level.Progress = progress;
        level.Error = error ?? "";
        foreach(TufLevel other in owner) {
            if(ReferenceEquals(other, level) || other.Id != level.Id) continue;
            other.State = state;
            other.Progress = progress;
            other.Error = level.Error;
            other.InstallFolder ??= level.InstallFolder;
        }
    }
    public void Cancel() => actionRequest?.Cancel();
    public void Dispose() {
        disposed = true;
        actionRequest?.Cancel();
        actionRequest?.Dispose();
        actionRequest = null;
        pending.Clear();
        downloading = null;
        launchingId = 0;
    }
}
