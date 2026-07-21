using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using Quartz.IO.Interface;
namespace Quartz.IO;
public interface ISettingsHandle {
    string Path { get; }
    bool Load();
    void LoadOrDefaults();
    bool Save();
    void CancelPendingSave();
}
public static class SaveGate {
    public static Func<bool> DeferWrites;
    public static bool ShouldDefer {
        get {
            try {
                return DeferWrites?.Invoke() ?? false;
            } catch {
                return false;
            }
        }
    }
}
public static class SettingsRegistry {
    private static readonly List<ISettingsHandle> handles = [];
    private static readonly object sync = new();
    public static void Register(ISettingsHandle handle) {
        lock(sync) {
            handles.RemoveAll(h => h.Path == handle.Path);
            handles.Add(handle);
        }
    }
    public static void Unregister(ISettingsHandle handle) {
        lock(sync) handles.Remove(handle);
    }
    public static ISettingsHandle[] Snapshot() {
        lock(sync) return [.. handles];
    }
    public static bool SaveAll() {
        bool success = true;
        foreach(ISettingsHandle handle in Snapshot()) success &= handle.Save();
        return success;
    }
    public static void CancelPendingSaves() {
        foreach(ISettingsHandle handle in Snapshot()) handle.CancelPendingSave();
    }
    public static void ReloadAll() {
        foreach(ISettingsHandle handle in Snapshot()) {
            handle.CancelPendingSave();
            handle.LoadOrDefaults();
        }
    }
}
public sealed class SettingsFile<T> : ISettingsHandle where T : class, ISettingsFile, new() {
    public T Data { get; } = new();
    public readonly string Path;
    string ISettingsHandle.Path => Path;
    private readonly object saveLock = new();
    private readonly object requestLock = new();
    private CancellationTokenSource saveCts;
    public SettingsFile(string path) {
        Path = path;
        SettingsRegistry.Register(this);
    }
    public static SettingsFile<T> Loaded(string fileName) {
        SettingsFile<T> file = new(System.IO.Path.Combine(MainCore.Paths.RootPath, fileName));
        file.Load();
        return file;
    }
    public bool Load() {
        try {
            if(!File.Exists(Path)) return false;
            string json = File.ReadAllText(Path);
            JToken token = JToken.Parse(json);
            Data.Deserialize(token);
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(SettingsFile<>)}] Failed to load settings '{Path}': {e}");
            return false;
        }
    }
    public void LoadOrDefaults() {
        if(Load()) return;
        try {
            Data.Deserialize(new T().Serialize());
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(SettingsFile<>)}] Failed to reset settings '{Path}': {e}");
        }
    }
    public bool Save() {
        CancelPendingSave();
        return SaveCore();
    }
    private long saveSeq;
    private long lastWrittenSeq;
    private bool SaveCore() {
        if(!TrySerialize(out string json, out long seq)) return false;
        return WriteJson(json, seq);
    }
    private bool TrySerialize(out string json, out long seq) {
        try {
            json = Data.Serialize().ToString();
            seq = Interlocked.Increment(ref saveSeq);
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(SettingsFile<>)}] Failed to serialize settings '{Path}': {e}");
            json = null;
            seq = 0;
            return false;
        }
    }
    private bool WriteJson(string json, long seq) {
        lock(saveLock) {
            if(seq < lastWrittenSeq) return true;
            try {
                string dir = System.IO.Path.GetDirectoryName(Path);
                if(!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                AtomicFile.WriteAllText(Path, json);
                lastWrittenSeq = seq;
                return true;
            } catch(Exception e) {
                MainCore.Log.Err($"[{nameof(SettingsFile<>)}] Failed to save settings '{Path}': {e}");
                return false;
            }
        }
    }
    public void RequestSave(int delay = 500) {
        CancellationTokenSource request = new();
        CancellationTokenSource previous;
        lock(requestLock) {
            previous = saveCts;
            saveCts = request;
        }
        previous?.Cancel();
        _ = SaveAfterDelay(delay, request);
    }
    private async Task SaveAfterDelay(int delay, CancellationTokenSource request) {
        CancellationToken token = request.Token;
        try {
            await Task.Delay(delay, token);
            if(token.IsCancellationRequested) {
                request.Dispose();
                return;
            }
            MainThread.Enqueue(() => {
                bool isCurrent;
                lock(requestLock) {
                    isCurrent = ReferenceEquals(saveCts, request);
                }
                if(!isCurrent || token.IsCancellationRequested) {
                    request.Dispose();
                    return;
                }
                if(SaveGate.ShouldDefer) {
                    _ = SaveAfterDelay(delay, request);
                    return;
                }
                lock(requestLock) {
                    if(ReferenceEquals(saveCts, request)) saveCts = null;
                }
                if(TrySerialize(out string json, out long seq))
                    _ = Task.Run(() => WriteJson(json, seq));
                request.Dispose();
            });
        } catch(OperationCanceledException) {
            lock(requestLock) {
                if(ReferenceEquals(saveCts, request)) saveCts = null;
            }
            request.Dispose();
        } catch(Exception e) {
            lock(requestLock) {
                if(ReferenceEquals(saveCts, request)) saveCts = null;
            }
            request.Dispose();
            MainCore.Log.Err($"[{nameof(SettingsFile<>)}] Failed to request save '{Path}': {e}");
        }
    }
    public void CancelPendingSave() {
        CancellationTokenSource pending;
        lock(requestLock) {
            pending = saveCts;
            saveCts = null;
        }
        pending?.Cancel();
    }
    public void Dispose() {
        if(Data is IDisposable disposable) disposable.Dispose();
    }
}
