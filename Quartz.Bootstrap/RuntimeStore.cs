using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Bootstrap;
// Owns Runtime/state.json and Runtime/versions/. The store is deliberately
// self-healing: a missing or corrupt state file is reseeded from whatever valid
// runtimes are on disk, an interrupted trial is demoted to Failed, and only the
// Current/Previous pair of versions survives cleanup.
public sealed class RuntimeStore {
    private readonly string runtimeRoot;
    private readonly string versionsRoot;
    private readonly string statePath;
    private readonly Action<string> warn;
    public RuntimeStore(string runtimeRoot, Action<string> warn) {
        this.runtimeRoot = Path.GetFullPath(runtimeRoot);
        this.warn = warn;
        versionsRoot = Path.Combine(this.runtimeRoot, "versions");
        statePath = Path.Combine(this.runtimeRoot, "state.json");
    }
    public string VersionsRoot => versionsRoot;
    public RuntimeState LoadAndRepair() {
        RuntimeState state = ReadState() ?? Reseed();
        if(!string.IsNullOrWhiteSpace(state.Trial)) {
            // A recorded trial means the last launch died before promoting it.
            // Blame the trial runtime and hold it back until a newer release
            // lands, so one bad update can't dead-loop every launch.
            warn($"the previous update attempt ({state.Trial}) did not complete — holding it back");
            state.Failed = state.Trial;
            state.Trial = null;
            Save(state);
        }
        if(!TryValidate(state.Current)) {
            if(TryValidate(state.Previous)) {
                warn($"the current runtime ({state.Current}) is broken — falling back to {state.Previous}");
                state.Current = state.Previous;
                state.Previous = null;
                Save(state);
            } else {
                state = Reseed();
            }
        }
        CleanupVersions(state);
        return state;
    }
    private RuntimeState ReadState() {
        try {
            if(!File.Exists(statePath)) return null;
            JObject root = JObject.Parse(File.ReadAllText(statePath));
            RuntimeState state = new() {
                SchemaVersion = root.Value<int?>("SchemaVersion") ?? 0,
                Current = root.Value<string>("Current"),
                Previous = root.Value<string>("Previous"),
                Trial = root.Value<string>("Trial"),
                Failed = root.Value<string>("Failed"),
            };
            if(state.SchemaVersion != 1 || string.IsNullOrWhiteSpace(state.Current)) return null;
            return state;
        } catch(Exception e) {
            warn("state.json is unreadable (" + e.Message + ") — rescanning installed runtimes");
            return null;
        }
    }
    private RuntimeState Reseed() {
        string best = null;
        SemVer bestVersion = default;
        if(Directory.Exists(versionsRoot)) {
            foreach(string directory in Directory.GetDirectories(versionsRoot)) {
                string name = Path.GetFileName(directory);
                if(!SemVer.TryParse(name, out SemVer version)) continue;
                if(!IsValidRuntime(directory)) continue;
                if(best == null || version.CompareTo(bestVersion) > 0) {
                    best = name;
                    bestVersion = version;
                }
            }
        }
        if(best == null)
            throw new InvalidDataException(
                $"no usable runtime under {versionsRoot} — reinstall {BootstrapInfo.ModName} from {BootstrapInfo.GithubLink}");
        RuntimeState state = new() { Current = best };
        Save(state);
        return state;
    }
    public RuntimeCandidate GetCandidate(string version) {
        if(string.IsNullOrWhiteSpace(version)) throw new InvalidDataException("the runtime version is missing");
        string path = Path.Combine(versionsRoot, version);
        return ValidateCandidate(version, path);
    }
    public RuntimeCandidate ValidateCandidate(string version, string runtimePath) {
        string expected = Path.GetFullPath(Path.Combine(versionsRoot, version));
        string actual = Path.GetFullPath(runtimePath ?? string.Empty);
        if(!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("the update engine returned an unexpected runtime path");
        if(!IsValidRuntime(actual))
            throw new InvalidDataException("the runtime is incomplete: " + actual);
        string stamped = JObject.Parse(File.ReadAllText(Path.Combine(actual, "runtime.json"))).Value<string>("Version") ?? "";
        if(!SemVer.TryParse(stamped, out SemVer stampedVersion)
            || !SemVer.TryParse(version, out SemVer expectedVersion)
            || stampedVersion.ToString() != expectedVersion.ToString())
            throw new InvalidDataException($"the runtime version marker '{stamped}' does not match '{version}'");
        return new RuntimeCandidate(version, actual);
    }
    private bool TryValidate(string version) {
        try {
            if(string.IsNullOrWhiteSpace(version)) return false;
            GetCandidate(version);
            return true;
        } catch(Exception e) {
            _ = e.Message;
            return false;
        }
    }
    private static bool IsValidRuntime(string directory) {
        return File.Exists(Path.Combine(directory, BootstrapInfo.PayloadFileName))
            && File.Exists(Path.Combine(directory, BootstrapInfo.EngineFileName))
            && File.Exists(Path.Combine(directory, "runtime.json"));
    }
    public void Promote(RuntimeState state, string version) {
        if(!string.Equals(state.Current, version, StringComparison.OrdinalIgnoreCase)) state.Previous = state.Current;
        state.Current = version;
        state.Trial = null;
        state.Failed = null;
        Save(state);
        CleanupVersions(state);
    }
    public void Save(RuntimeState state) {
        try {
            Directory.CreateDirectory(runtimeRoot);
            string temporary = statePath + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(state, Formatting.Indented) + Environment.NewLine, Encoding.UTF8);
            if(File.Exists(statePath)) File.Delete(statePath);
            File.Move(temporary, statePath);
        } catch(Exception e) {
            warn("could not save runtime state: " + e.Message);
        }
    }
    public void DeleteRuntime(string version, RuntimeState state) {
        if(string.IsNullOrWhiteSpace(version)) return;
        if(string.Equals(version, state.Current, StringComparison.OrdinalIgnoreCase)) return;
        if(string.Equals(version, state.Previous, StringComparison.OrdinalIgnoreCase)) return;
        TryDeleteDirectory(Path.Combine(versionsRoot, version));
    }
    private void CleanupVersions(RuntimeState state) {
        if(!Directory.Exists(versionsRoot)) return;
        foreach(string directory in Directory.GetDirectories(versionsRoot)) {
            string name = Path.GetFileName(directory);
            if(string.Equals(name, state.Current, StringComparison.OrdinalIgnoreCase)) continue;
            if(string.Equals(name, state.Previous, StringComparison.OrdinalIgnoreCase)) continue;
            if(string.Equals(name, state.Trial, StringComparison.OrdinalIgnoreCase)) continue;
            TryDeleteDirectory(directory);
        }
    }
    private void TryDeleteDirectory(string path) {
        try {
            if(Directory.Exists(path)) Directory.Delete(path, true);
        } catch(Exception e) {
            warn("could not remove " + path + ": " + e.Message);
        }
    }
}
