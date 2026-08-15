using System.Reflection;
namespace Quartz.Bootstrap;
public sealed class BootstrapResult {
    public RuntimeCandidate Loaded;
    public Assembly Assembly;
    public bool UpdateFailed;
}
// The launch sequence: repair the runtime store, ask the current runtime's
// update engine for a newer version, and load whichever runtime wins. A fresh
// download is loaded as a TRIAL — it only becomes Current after the payload's
// entry point returns cleanly, and a trial that fails (or dies mid-launch) is
// recorded as Failed and held back until a newer release supersedes it, so a
// broken update costs one launch, not every launch.
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
            // Nothing usable on disk (an installer that dropped Runtime/, a
            // hand-gutted folder). Restore this bootstrap's own release and
            // try once more; a second failure is the real error.
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
        if(!resolution.HasCandidate) return LoadCurrent(current, dataRoot, warn, tryStart);
        RuntimeCandidate trial;
        try {
            trial = store.ValidateCandidate(resolution.Version, resolution.RuntimePath);
        } catch(Exception e) {
            warn("[AutoUpdate] the downloaded runtime is unusable (" + e.Message + ") — loading the current runtime");
            return LoadCurrent(current, dataRoot, warn, tryStart);
        }
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
        // The failed payload may have half-initialized static state in this
        // process, so nothing else is loaded this launch; the old runtime is
        // intact and comes back on the next one.
        state.Trial = null;
        state.Failed = trial.Version;
        store.Save(state);
        store.DeleteRuntime(trial.Version, state);
        warn($"[AutoUpdate] the updated runtime {trial.Version} failed to start: {startError}");
        warn($"[AutoUpdate] it is held back — restart to run {current.Version}");
        return new BootstrapResult { UpdateFailed = true };
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
