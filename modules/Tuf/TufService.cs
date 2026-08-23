using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.UI;
namespace Quartz.Features.Tuf;
public enum TufMoveState { Idle, Moving, Done, Failed }
public sealed partial class TufService : IRuntimeService {
    public static TufService Instance { get; private set; }
    public IReadOnlyList<TufLevel> Levels => levels;
    public TufListState State { get; private set; } = TufListState.Idle;
    public string Error { get; private set; } = "";
    public bool OfflineError { get; private set; }
    public string Query { get; private set; } = "";
    public TufSort Sort { get; private set; } = TufSort.Recent;
    public bool Ascending { get; private set; }
    public bool HasMore { get; private set; }
    public bool LoadingMore { get; private set; }
    public bool IsBusy => actions?.IsBusy ?? false;
    public bool IsLaunching => actions?.IsLaunching ?? false;
    public int QueueCount => actions?.QueueCount ?? 0;
    public int QueuePosition(int id) => actions?.QueuePosition(id) ?? 0;
    public bool ShowInstalled { get; private set; }
    public int InstalledCount => index?.Data.Count ?? 0;
    public int InfoRevision { get; private set; }
    internal TufDownloadService Downloads => downloads;
    internal TufLevelLauncher Launcher => launcher;
    public TufDifficultyFilter DifficultyFilter { get; private set; } = TufDifficultyFilter.AllRanked;
    public int MinDifficultyIndex => DifficultyFilter.MinIndex;
    public int MaxDifficultyIndex => DifficultyFilter.MaxIndex;
    public bool QuantumEnabled => DifficultyFilter.HasQuantum;
    public int QuantumMinIndex => quantumMinIndex;
    public int QuantumMaxIndex => quantumMaxIndex;
    public IReadOnlyList<string> SelectedDifficulties => DifficultyFilter.SelectedDifficulties;
    public event Action Changed = delegate { };
    public TufMoveState MoveState { get; private set; } = TufMoveState.Idle;
    public int MoveDone { get; private set; }
    public int MoveTotal { get; private set; }
    public string MoveError { get; private set; } = "";
    private readonly List<TufLevel> levels = [];
    private TufApiClient api;
    private TufDownloadService downloads;
    private TufLevelLauncher launcher;
    private TufLevelActionRunner actions;
    private CancellationTokenSource listRequest;
    private CancellationTokenSource debounce;
    private CancellationTokenSource moveRequest;
    private int listGeneration;
    private int nextOffset;
    private bool appendFailed;
    private bool disposed;
    private SettingsFile<TufSettings> settings;
    private SettingsFile<TufInstallIndex> index;
    private const int MaxInfoProbes = 25;
    private readonly HashSet<int> infoProbed = [];
    private readonly HashSet<int> chartProbed = [];
    private CancellationTokenSource infoRequest;
    private int quantumMinIndex;
    private int quantumMaxIndex = TufDifficultyFilter.QuantumNames.Count - 1;
    public void Initialize() {
        Instance = this;
        TufHelperLiteLink.Reset();
        settings = new SettingsFile<TufSettings>(Path.Combine(MainCore.Paths.TufPath, "Settings.json"));
        settings.Load();
        index = new SettingsFile<TufInstallIndex>(Path.Combine(MainCore.Paths.TufPath, "Installed.json"));
        index.Load();
        Sort = settings.Data.GetSort();
        Ascending = settings.Data.Ascending;
        DifficultyFilter = settings.Data.GetDifficultyFilter();
        quantumMinIndex = settings.Data.QuantumMinIndex;
        quantumMaxIndex = settings.Data.QuantumMaxIndex;
        api = new TufApiClient();
        downloads = new TufDownloadService(MainCore.Paths.TufLevelsPath, ResolveInstallRoot);
        launcher = MainCore.Root == null ? null : MainCore.Root.AddComponent<TufLevelLauncher>();
        if(launcher == null) {
            MainCore.Log.Err("[TUF] could not attach the level launcher; launching levels is unavailable this session.");
        } else {
            launcher.Initialize(MainCore.Paths.TufLevelsPath, TrustedRoots);
        }
        actions = new TufLevelActionRunner(levels, downloads, launcher, Notify, RecordInstalledLevel) {
            Finished = OnActionFinished
        };
        RefreshSnapshotCounts();
    }
    public void EnsureLoaded() {
        if(State == TufListState.Idle) Refresh();
    }
    public void Refresh() {
        CancelDebounce();
        if(ShowInstalled) LoadInstalled();
        else Fetch(false);
    }
    public void LoadMore() {
        if(HasMore && !LoadingMore
            && (State == TufListState.Ready || (State == TufListState.Error && appendFailed))) Fetch(true);
    }
    public void Act(TufLevel level) => actions?.Act(level);
    public void LaunchChart(TufLevel level, string chart) => actions?.LaunchChart(level, chart);
    private void Notify() {
        Action handlers = Changed;
        if(handlers == null) return;
        foreach(Delegate entry in handlers.GetInvocationList()) {
            Action handler = (Action)entry;
            try {
                handler();
            } catch(Exception e) {
                Changed -= handler;
                MainCore.Log.Err($"[TUF] dropped a listener that failed to refresh: {e.Message}");
            }
        }
    }
    public void Dispose() {
        disposed = true;
        TufRollbackDialog.Close();
        settings?.Save();
        index?.Save();
        if(settings != null) SettingsRegistry.Unregister(settings);
        if(index != null) SettingsRegistry.Unregister(index);
        debounce?.Cancel();
        listRequest?.Cancel();
        moveRequest?.Cancel();
        infoRequest?.Cancel();
        updateRequest?.Cancel();
        actions?.Dispose();
        downloads?.Cancel();
        launcher?.Cancel();
        if(launcher != null) UnityEngine.Object.Destroy(launcher);
        launcher = null;
        debounce?.Dispose();
        listRequest?.Dispose();
        moveRequest?.Dispose();
        infoRequest?.Dispose();
        updateRequest?.Dispose();
        downloads?.Dispose();
        api?.Dispose();
        TufPreviewCache.Clear();
        Changed = delegate { };
        levels.Clear();
        if(ReferenceEquals(Instance, this)) Instance = null;
    }
}
