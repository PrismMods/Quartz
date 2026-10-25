using System.Reflection;
namespace Quartz.Bootstrap;
public sealed class BootstrapResult {
    public RuntimeCandidate Loaded;
    public Assembly Assembly;
    public bool UpdateFailed;
}
// Repair the store, ask the current engine for an update, load the winner. A new download runs as a trial and becomes Current only if Load returns; a failed trial is held back until a newer release.
public static class BootstrapCore {
    public static BootstrapResult Run(
        string runtimeRoot,
        string dataRoot,
        Action<string> msg,
        Action<string> warn,
        Func<Assembly, Exception> tryStart) {
        RuntimeStore store = new(runtimeRoot, warn);
        RuntimeState state;
        try {
            state = store.LoadAndRepair();
        } catch(Exception storeError) {
            // Nothing usable on disk: restore this bootstrap's own release and retry once.
            warn($"the runtime store is unusable ({storeError.Message})");
            if(!RecoveryInstaller.TryRestore(runtimeRoot, msg, warn)) throw;
            state = store.LoadAndRepair();
        }
        RuntimeCandidate current = store.GetCandidate(state.Current);
        UpdateResolution resolution;
        try {
            resolution = EngineClient.Resolve(current, runtimeRoot, dataRoot, state.Failed);
            if(resolution.Message != null) msg("[AutoUpdate] " + resolution.Message);
        } catch(Exception e) {
            warn("[AutoUpdate] the update check failed (" + e.Message + ") — loading the current runtime");
            resolution = UpdateResolution.None();
        }
        RuntimeCandidate trial;
        try {
            trial = resolution.HasCandidate
                ? store.ValidateCandidate(resolution.Version, resolution.RuntimePath)
                : StagedTrial(store, state);
        } catch(Exception e) {
            warn("[AutoUpdate] the downloaded runtime is unusable (" + e.Message + ") — loading the current runtime");
            state.Staged = null;
            store.Save(state);
            return LoadCurrent(current, dataRoot, warn, tryStart);
        }
        if(trial == null) return LoadCurrent(current, dataRoot, warn, tryStart);
        msg($"[AutoUpdate] updating {current.Version} -> {trial.Version}");
        state.Trial = trial.Version;
        store.Save(state);
        DataSync.Apply(trial.RuntimePath, dataRoot, warn);
        Assembly assembly = PayloadLoader.Load(trial);
        Exception startError = tryStart(assembly);
        if(startError == null) {
            store.Promote(state, trial.Version);
            msg($"[AutoUpdate] now running {trial.Version}");
            return new BootstrapResult { Loaded = trial, Assembly = assembly };
        }
        // The failed payload may have left static state: load nothing else this launch.
        state.Trial = null;
        state.Staged = null;
        state.Failed = trial.Version;
        store.Save(state);
        store.DeleteRuntime(trial.Version, state);
        warn($"[AutoUpdate] the updated runtime {trial.Version} failed to start: {startError}");
        warn($"[AutoUpdate] it is held back — restart to run {current.Version}");
        return new BootstrapResult { UpdateFailed = true };
    }
    private static RuntimeCandidate StagedTrial(RuntimeStore store, RuntimeState state) {
        string staged = state.Staged;
        if(string.IsNullOrWhiteSpace(staged)
            || string.Equals(staged, state.Current, StringComparison.OrdinalIgnoreCase)
            || string.Equals(staged, state.Failed, StringComparison.OrdinalIgnoreCase)) return null;
        return store.GetCandidate(staged);
    }
    private static BootstrapResult LoadCurrent(
        RuntimeCandidate current,
        string dataRoot,
        Action<string> warn,
        Func<Assembly, Exception> tryStart) {
        DataSync.Apply(current.RuntimePath, dataRoot, warn);
        Assembly assembly = PayloadLoader.Load(current);
        Exception startError = tryStart(assembly);
        if(startError != null) {
            warn($"the {current.Version} runtime failed to start: {startError}");
            return new BootstrapResult();
        }
        return new BootstrapResult { Loaded = current, Assembly = assembly };
    }
}
