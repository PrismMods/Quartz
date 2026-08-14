using System.Reflection;
using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.UI;
namespace Quartz.Modules;
public static class ModuleService {
    public const string ModuleExtension = ModuleRemovalPaths.BinaryExtension;
    public const string ManifestExtension = ModuleRemovalPaths.ManifestExtension;
    public sealed class Handle {
        public ModuleManifest Manifest;
        public string Id => Manifest?.Id ?? "";
        public string Name => Manifest?.Name ?? Id;
        public string Version => Manifest?.Version ?? "";
        public string SourcePath;
        public bool Enabled;
        public string Error;
        public QuartzModule Instance;
        internal ModuleContext Context;
        internal Assembly Image;
        internal bool Active;
        public bool Loaded => Instance != null && Error == null;
    }
    private static readonly List<Handle> handles = [];
    public static IReadOnlyList<Handle> Modules => handles;
    private static ModuleState state;
    public static ModuleState State => state ??= ModuleState.Load();
    private static Action<bool, bool> modChangedHandler;
    private static bool initialized;
    public static readonly IRuntimeService Service = new ServiceAdapter();
    public static readonly IRuntimeTick Ticker = new TickAdapter();
    private sealed class ServiceAdapter : IRuntimeService {
        public void Initialize() => ModuleService.Initialize();
        public void Dispose() => ModuleService.Dispose();
    }
    private sealed class TickAdapter : IRuntimeTick {
        public void Tick() => ModuleService.Tick();
    }
    private static void Initialize() {
        if(initialized) return;
        initialized = true;
        state = ModuleState.Load();
        ModuleMigration.RunOnce(state);
        ModuleBundle.RefreshInstalled();
        ModuleMigration.ApplySplits(state);
        modChangedHandler = (_, isDispose) => ApplyActive(!isDispose);
        MainCore.OnModEnabledChanged += modChangedHandler;
        LoadAll();
        ApplyActive();
        ModuleCatalogService.EnsureLoaded();
    }
    private static void Dispose() {
        if(!initialized) return;
        initialized = false;
        UnloadAll();
        if(modChangedHandler != null) {
            MainCore.OnModEnabledChanged -= modChangedHandler;
            modChangedHandler = null;
        }
        state?.Save();
        state = null;
    }
    private static void Tick() {
        for(int i = 0; i < handles.Count; i++) {
            Handle handle = handles[i];
            if(!handle.Active) continue;
            try {
                handle.Instance.OnTick();
            } catch(Exception e) {
                handle.Error = $"OnTick threw: {e}";
                MainCore.Log.Err($"[Module:{handle.Id}] OnTick threw — module stopped: {e}");
                SafeDisable(handle);
            }
        }
    }
    public static Handle Find(string id) {
        foreach(Handle handle in handles)
            if(handle.Id == id) return handle;
        return null;
    }
    public static bool IsCoreOwned(string id) => CoreOwnedIds.Contains(id);
    private static readonly HashSet<string> CoreOwnedIds = new(StringComparer.Ordinal);
    public static void DeclareCoreOwned(string id) {
        if(ModuleManifest.IsValidId(id)) CoreOwnedIds.Add(id);
    }
    public static void SetEnabled(string id, bool enabled) {
        Handle handle = Find(id);
        ModuleState.Entry entry = State.For(id);
        if(entry.Enabled == enabled && handle != null && handle.Enabled == enabled) return;
        entry.Enabled = enabled;
        State.Save();
        if(handle != null) handle.Enabled = enabled;
        MainThread.Enqueue(() => {
            BeginScanBatch();
            try {
                if(enabled) {
                    LoadOne(id);
                    LoadDependents(id);
                } else {
                    UnloadOne(id);
                }
            } finally {
                EndScanBatch();
            }
            ApplyActive();
            RebuildUI();
        });
    }
    public static void LoadInstalled(IReadOnlyList<string> ids) {
        if(ids == null) return;
        BeginScanBatch();
        try {
            foreach(string id in ids) {
                handles.RemoveAll(h => h.Id == id && !h.Loaded);
                LoadOne(id);
            }
        } finally {
            EndScanBatch();
        }
        ApplyActive();
        RebuildUI();
    }
    public static void ReloadAll() {
        if(!initialized) return;
        MainThread.Enqueue(() => {
            UnloadAll();
            state = ModuleState.Load();
            LoadAll();
            ApplyActive();
            RebuildUI();
        });
    }
    private static void RebuildUI() {
        if(UICore.Pages.Count == 0) return;
        if(Quartz.UI.Nav.NavRegistry.ByState(UICore.CurrentMenuState) == null)
            UICore.CurrentMenuState = Quartz.UI.Nav.NavRegistry.FirstVisibleState();
        UICore.Rebuild();
    }
    private static int scanHold;
    private static List<ModuleManifest> scanFound;
    private static Dictionary<string, string> scanRejected;
    private static Dictionary<string, string> scanPaths;
    private static void BeginScanBatch() => scanHold++;
    private static void EndScanBatch() {
        if(scanHold > 0) scanHold--;
        if(scanHold > 0) return;
        scanFound = null;
        scanRejected = null;
        scanPaths = null;
    }
    private static List<ModuleManifest> ScanManifests(out Dictionary<string, string> rejected, out Dictionary<string, string> paths) {
        if(scanHold > 0 && scanFound != null) {
            rejected = scanRejected;
            paths = scanPaths;
            return scanFound;
        }
        List<ModuleManifest> scanned = ScanManifestsUncached(out rejected, out paths);
        if(scanHold > 0) {
            scanFound = scanned;
            scanRejected = rejected;
            scanPaths = paths;
        }
        return scanned;
    }
    private static List<ModuleManifest> ScanManifestsUncached(out Dictionary<string, string> rejected, out Dictionary<string, string> paths) {
        rejected = new Dictionary<string, string>(StringComparer.Ordinal);
        paths = new Dictionary<string, string>(StringComparer.Ordinal);
        List<ModuleManifest> found = [];
        string root = MainCore.Paths.ModulePath;
        string[] files;
        try {
            Directory.CreateDirectory(root);
            files = Directory.GetFiles(root, "*" + ModuleExtension);
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] scan failed: {e}");
            return found;
        }
        foreach(string file in files) {
            string stem = Path.GetFileNameWithoutExtension(file);
            paths[stem] = file;
            string manifestPath = Path.Combine(root, stem + ManifestExtension);
            if(!File.Exists(manifestPath)) {
                rejected[stem] = "missing " + stem + ManifestExtension;
                continue;
            }
            string json;
            try {
                var info = new FileInfo(manifestPath);
                if(info.Length > ModuleManifest.MaxBytes) {
                    rejected[stem] = "manifest is too large";
                    continue;
                }
                json = File.ReadAllText(manifestPath);
            } catch(Exception e) {
                rejected[stem] = "manifest unreadable: " + e.Message;
                continue;
            }
            ModuleManifest manifest = ModuleManifest.Parse(json, out string error);
            if(manifest == null) {
                rejected[stem] = error;
                continue;
            }
            if(manifest.Id != stem) {
                rejected[stem] = $"manifest id '{manifest.Id}' does not match the file name";
                continue;
            }
            if(IsCoreOwned(manifest.Id)) {
                rejected[stem] = "superseded by core";
                continue;
            }
            if(manifest.CoreAbi != Info.ModuleAbi) {
                rejected[stem] = $"built for module ABI {manifest.CoreAbi}, this Quartz uses {Info.ModuleAbi}";
                continue;
            }
            if(!MeetsMinCore(manifest)) {
                rejected[stem] = $"needs Quartz {manifest.MinCoreVersion} or newer";
                continue;
            }
            if(found.Any(m => string.Equals(m.Id, manifest.Id, StringComparison.Ordinal))) {
                rejected[stem] = "duplicate module id";
                continue;
            }
            found.Add(manifest);
        }
        return found;
    }
    private static bool MeetsMinCore(ModuleManifest manifest) {
        if(string.IsNullOrEmpty(manifest.MinCoreVersion)) return true;
        if(!SemVer.TryParse(manifest.MinCoreVersion, out SemVer min)) return true;
        SemVer current = Info.Current;
        return current.CompareTo(min) >= 0;
    }
    private static void LoadAll() {
        List<ModuleManifest> manifests = ScanManifests(out var rejected, out var paths);
        List<ModuleManifest> enabled = [];
        foreach(ModuleManifest manifest in manifests) {
            if(State.For(manifest.Id).Enabled) enabled.Add(manifest);
            else handles.Add(new Handle { Manifest = manifest, SourcePath = paths[manifest.Id], Enabled = false });
        }
        ModuleOrder.Result order = ModuleOrder.Sort(enabled);
        foreach(var kvp in order.Rejected) rejected[kvp.Key] = kvp.Value;
        foreach(ModuleManifest manifest in order.Ordered) Instantiate(manifest, paths[manifest.Id]);
        foreach(var kvp in rejected) {
            if(Find(kvp.Key) != null) continue;
            ModuleManifest manifest = enabled.FirstOrDefault(m => m.Id == kvp.Key)
                ?? manifests.FirstOrDefault(m => m.Id == kvp.Key);
            handles.Add(new Handle {
                Manifest = manifest ?? new ModuleManifest { Id = kvp.Key, Name = kvp.Key },
                SourcePath = paths.GetValueOrDefault(kvp.Key),
                Enabled = true,
                Error = kvp.Value,
            });
            MainCore.Log.Err($"[Module:{kvp.Key}] {kvp.Value}");
        }
    }
    private static void LoadOne(string id) {
        if(Find(id) is { Loaded: true }) return;
        handles.RemoveAll(h => h.Id == id);
        List<ModuleManifest> manifests = ScanManifests(out var rejected, out var paths);
        ModuleManifest manifest = manifests.FirstOrDefault(m => m.Id == id);
        if(manifest == null) {
            string why = rejected.GetValueOrDefault(id, "not installed");
            handles.Add(new Handle {
                Manifest = new ModuleManifest { Id = id, Name = id },
                SourcePath = paths.GetValueOrDefault(id),
                Enabled = true,
                Error = why,
            });
            MainCore.Log.Err($"[Module:{id}] {why}");
            return;
        }
        foreach(string dep in manifest.Deps) {
            if(Find(dep) is { Loaded: true }) continue;
            handles.Add(new Handle {
                Manifest = manifest,
                SourcePath = paths[id],
                Enabled = true,
                Error = $"requires '{dep}', which is not loaded",
            });
            return;
        }
        Instantiate(manifest, paths[id]);
        ApplyActive();
    }
    private static void LoadDependents(string id) {
        foreach(ModuleManifest manifest in ScanManifests(out _, out _)) {
            if(Array.IndexOf(manifest.Deps, id) < 0) continue;
            if(!State.For(manifest.Id).Enabled) continue;
            if(Find(manifest.Id) is { Loaded: true }) continue;
            LoadOne(manifest.Id);
            LoadDependents(manifest.Id);
        }
    }
    private static void UnloadOne(string id) {
        Handle handle = Find(id);
        if(handle == null) return;
        foreach(Handle other in handles.ToArray()) {
            if(other == handle || other.Manifest == null) continue;
            if(Array.IndexOf(other.Manifest.Deps, id) >= 0) UnloadOne(other.Id);
        }
        Teardown(handle);
        handle.Enabled = false;
    }
    private static readonly Dictionary<string, Assembly> images = new(StringComparer.Ordinal);
    private static Assembly LoadImageOnce(string id, string path) {
        if(images.TryGetValue(id, out Assembly cached)) return cached;
        Assembly loaded = Quartz.Plugins.PluginImage.Load(path);
        images[id] = loaded;
        Quartz.Plugins.PluginIdentityResolver.Publish(loaded);
        return loaded;
    }
    private static void ForgetImage(string id) {
        if(id == null || !images.TryGetValue(id, out Assembly cached)) return;
        images.Remove(id);
        Quartz.Plugins.PluginIdentityResolver.Withdraw(cached);
    }
    private static void Instantiate(ModuleManifest manifest, string path) {
        Handle handle = new() { Manifest = manifest, SourcePath = path, Enabled = true };
        handles.Add(handle);
        Assembly assembly;
        try {
            assembly = LoadImageOnce(manifest.Id, path);
            handle.Image = assembly;
        } catch(Exception e) {
            handle.Error = "failed to load: " + e.Message;
            MainCore.Log.Err($"[Module:{handle.Id}] {handle.Error}");
            return;
        }
        if(!AttributeMatches(assembly, manifest, out string mismatch)) {
            handle.Error = mismatch;
            MainCore.Log.Err($"[Module:{handle.Id}] {mismatch}");
            return;
        }
        Type entry = Quartz.Plugins.PluginEntryScan.FindEntry<QuartzModule>(assembly, manifest.Entry, out string scanError);
        if(entry == null) {
            handle.Error = scanError;
            MainCore.Log.Err($"[Module:{handle.Id}] {scanError}");
            return;
        }
        try {
            QuartzModule instance = (QuartzModule)Activator.CreateInstance(entry);
            handle.Context = new ModuleContext(manifest);
            instance.Context = handle.Context;
            handle.Instance = instance;
            instance.OnLoad();
            MainCore.Log.Msg($"[Modules] loaded '{handle.Name}' v{handle.Version}");
        } catch(Exception e) {
            handle.Error = $"OnLoad threw: {e}";
            MainCore.Log.Err($"[Module:{handle.Id}] {handle.Error}");
            handle.Context?.Cleanup();
            handle.Context = null;
            handle.Instance = null;
        }
    }
    private static bool AttributeMatches(Assembly assembly, ModuleManifest manifest, out string error) {
        error = null;
        QuartzModuleInfoAttribute stamp;
        try {
            stamp = assembly.GetCustomAttribute<QuartzModuleInfoAttribute>();
        } catch(Exception e) {
            Diag.Ignore(e);
            return true;
        }
        if(stamp == null) return true;
        if(stamp.Id != manifest.Id) {
            error = $"binary is stamped '{stamp.Id}' but the manifest says '{manifest.Id}'";
            return false;
        }
        if(stamp.CoreAbi != manifest.CoreAbi) {
            error = $"binary targets module ABI {stamp.CoreAbi} but the manifest claims {manifest.CoreAbi}";
            return false;
        }
        if(stamp.Version != manifest.Version) {
            error = $"binary is version {stamp.Version} but the manifest claims {manifest.Version}";
            return false;
        }
        return true;
    }
    private static void ApplyActive(bool unpatchOnDisable = true) {
        foreach(Handle handle in handles) {
            bool should = MainCore.IsModEnabled && handle.Enabled && handle.Loaded;
            if(should == handle.Active) continue;
            if(should) {
                try {
                    ModuleContext context = handle.Context;
                    context?.ApplyPatches();
                    handle.Instance.OnEnable();
                    context?.RunEnableSteps();
                    handle.Active = true;
                } catch(Exception e) {
                    handle.Error = $"OnEnable threw: {e}";
                    MainCore.Log.Err($"[Module:{handle.Id}] OnEnable threw: {e}");
                }
            } else {
                SafeDisable(handle, unpatchOnDisable);
            }
        }
    }
    private static void SafeDisable(Handle handle, bool unpatch = true) {
        if(!handle.Active) return;
        handle.Active = false;
        try {
            handle.Context?.RunDisableSteps();
            handle.Instance.OnDisable();
        } catch(Exception e) {
            MainCore.Log.Err($"[Module:{handle.Id}] OnDisable threw: {e}");
        }
        if(unpatch) handle.Context?.RemovePatches();
    }
    private static void Teardown(Handle handle) {
        SafeDisable(handle);
        if(handle.Instance != null) {
            try {
                handle.Instance.OnUnload();
            } catch(Exception e) {
                MainCore.Log.Err($"[Module:{handle.Id}] OnUnload threw: {e}");
            }
        }
        handle.Context?.Cleanup();
        handle.Image = null;
        handle.Instance = null;
        handle.Context = null;
    }
    private static void UnloadAll() {
        for(int i = handles.Count - 1; i >= 0; i--) Teardown(handles[i]);
        handles.Clear();
    }
    public static bool Remove(string id) {
        Handle handle = Find(id);
        if(handle == null) return false;
        if(!ModuleRemovalPaths.TryResolve(MainCore.Paths.ModulePath, handle.SourcePath, handle.Id,
            out string binary, out string manifest)) {
            MainCore.Log.Err($"[Modules] refused unsafe removal path for '{id}'");
            return false;
        }
        MainThread.Enqueue(() => {
            UnloadOne(id);
            try {
                if(binary != null && File.Exists(binary)) File.Delete(binary);
                if(manifest != null && File.Exists(manifest)) File.Delete(manifest);
            } catch(Exception e) {
                MainCore.Log.Err($"[Modules] couldn't remove '{id}': {e.Message}");
                RebuildUI();
                return;
            }
            handles.RemoveAll(h => h.Id == id);
            ForgetImage(id);
            State.Modules.Remove(id);
            State.Save();
            MainCore.Log.Msg($"[Modules] removed '{id}' — restart the game before installing it again");
            RebuildUI();
        });
        return true;
    }
    public static void OpenModuleFolder() {
        string path = MainCore.Paths.ModulePath;
        try {
            Directory.CreateDirectory(path);
            UnityEngine.Application.OpenURL("file://" + path.Replace('\\', '/'));
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] couldn't open '{path}': {e.Message}");
        }
    }
}
