using System.Reflection;
using HarmonyLib;
using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.UI;
namespace Quartz.Features;
public static class FeatureService {
    public sealed class Handle {
        public FeatureAttribute Info;
        public Type Entry;
        public string Id => Info.Id;
        public string Name => Info.Name;
        public bool Enabled;
        public string Error;
        public QuartzFeature Instance;
        internal FeatureContext Context;
        internal bool Active;
        internal bool Ticks;
        public bool Loaded => Instance != null && Error == null;
    }
    // The KeyViewer flavour runs only these.
    private static readonly string[] KeyViewerIds = ["overlay", "keyviewer"];
    private static List<Handle> all;
    private static List<Handle> features;
    private static List<Handle> All => all ??= Scan();
    public static IReadOnlyList<Handle> Features => features ??= All.FindAll(h => !Info.KeyViewerOnly || Array.IndexOf(KeyViewerIds, h.Id) >= 0);
    private static FeatureState state;
    public static FeatureState State => state ??= FeatureState.Load();
    private static Action<bool, bool> modChangedHandler;
    private static bool initialized;
    public static readonly IRuntimeService Service = new ServiceAdapter();
    public static readonly IRuntimeTick Ticker = new TickAdapter();
    private sealed class ServiceAdapter : IRuntimeService {
        public void Initialize() => FeatureService.Initialize();
        public void Dispose() => FeatureService.Dispose();
    }
    private sealed class TickAdapter : IRuntimeTick {
        public void Tick() => FeatureService.Tick();
    }
    private static List<Handle> Scan() {
        List<Handle> found = [];
        foreach(Type type in AccessTools.GetTypesFromAssembly(MainCore.Asm)) {
            FeatureAttribute info = type.GetCustomAttribute<FeatureAttribute>(false);
            if(info == null || !typeof(QuartzFeature).IsAssignableFrom(type)) continue;
            info.Patches ??= [type.Namespace];
            found.Add(new Handle { Info = info, Entry = type });
        }
        found.Sort(static (a, b) => {
            int byOrder = a.Info.Order.CompareTo(b.Info.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });
        // Stable topological order.
        List<Handle> ordered = [];
        HashSet<string> placed = new(StringComparer.Ordinal);
        while(ordered.Count < found.Count) {
            bool progressed = false;
            foreach(Handle handle in found) {
                if(placed.Contains(handle.Id) || !handle.Info.Deps.All(placed.Contains)) continue;
                ordered.Add(handle);
                placed.Add(handle.Id);
                progressed = true;
            }
            if(progressed) continue;
            foreach(Handle handle in found) {
                if(!placed.Add(handle.Id)) continue;
                handle.Error = "part of a dependency cycle or needs a feature that doesn't exist";
                ordered.Add(handle);
            }
        }
        return ordered;
    }
    private static bool InNamespaces(Type type, string[] namespaces) {
        string ns = type.Namespace ?? "";
        foreach(string owned in namespaces)
            if(ns == owned || ns.StartsWith(owned + ".", StringComparison.Ordinal)) return true;
        return false;
    }
    internal static bool OwnsPatchClass(Type type) {
        foreach(Handle handle in All)
            if(InNamespaces(type, handle.Info.Patches)) return true;
        return false;
    }
    internal static IEnumerable<Type> PatchClasses(FeatureAttribute info) {
        foreach(Type type in AccessTools.GetTypesFromAssembly(MainCore.Asm))
            if(InNamespaces(type, info.Patches) && Core.Service.HarmonyService.IsPatchClass(type)) yield return type;
    }
    private static void Initialize() {
        if(initialized) return;
        initialized = true;
        state = FeatureState.Load();
        RegisterBuiltinTranslations();
        foreach(Handle handle in Features) handle.Enabled = state.IsEnabled(handle.Id);
        modChangedHandler = (_, isDispose) => ApplyActive(!isDispose);
        MainCore.OnModEnabledChanged += modChangedHandler;
        Sync();
    }
    // A disabled feature never runs OnLoad, so its name on the Features page would stay untranslated.
    private static void RegisterBuiltinTranslations() {
        foreach(string name in MainCore.Asm.GetManifestResourceNames()) {
            if(!name.StartsWith("Quartz.Features.", StringComparison.Ordinal) || !name.Contains(".Lang.")
                || !name.EndsWith(".json", StringComparison.Ordinal)) continue;
            try {
                using System.IO.Stream stream = MainCore.Asm.GetManifestResourceStream(name);
                if(stream == null) continue;
                using System.IO.StreamReader reader = new(stream);
                MainCore.Tr.Merge(reader.ReadToEnd(), "builtin " + name);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[Features] couldn't read translations '{name}': {e.Message}");
            }
        }
    }
    private static void Dispose() {
        if(!initialized) return;
        initialized = false;
        for(int i = Features.Count - 1; i >= 0; i--) Teardown(Features[i]);
        if(modChangedHandler != null) {
            MainCore.OnModEnabledChanged -= modChangedHandler;
            modChangedHandler = null;
        }
        state?.Save();
        state = null;
    }
    private static void Tick() {
        foreach(Handle handle in Features) {
            if(!handle.Active || !handle.Ticks) continue;
            try {
                handle.Instance.OnTick();
            } catch(Exception e) {
                handle.Error = $"OnTick threw: {e}";
                MainCore.Log.Err($"[Feature:{handle.Id}] OnTick threw — feature stopped: {e}");
                SafeDisable(handle);
            }
        }
    }
    public static Handle Find(string id) {
        foreach(Handle handle in Features)
            if(handle.Id == id) return handle;
        return null;
    }
    public static bool IsEnabled(string id) => Find(id) is { Enabled: true };
    // On also enables dependencies; off also disables dependents.
    public static void SetEnabled(string id, bool enabled) {
        Handle handle = Find(id);
        if(handle == null) return;
        HashSet<string> affected = new(StringComparer.Ordinal);
        Collect(handle, enabled, affected);
        foreach(string other in affected) {
            Handle target = Find(other);
            if(target == null) continue;
            target.Enabled = enabled;
            if(enabled) target.Error = null;
            State.Features[other] = enabled;
        }
        State.Save();
        MainThread.Enqueue(static () => {
            Sync();
            RebuildUI();
        });
    }
    private static void Collect(Handle handle, bool enabled, HashSet<string> into) {
        if(!into.Add(handle.Id)) return;
        if(enabled) {
            foreach(string dep in handle.Info.Deps)
                if(Find(dep) is Handle needed) Collect(needed, true, into);
            return;
        }
        foreach(Handle other in Features)
            if(Array.IndexOf(other.Info.Deps, handle.Id) >= 0) Collect(other, false, into);
    }
    private static void Sync() {
        for(int i = Features.Count - 1; i >= 0; i--) {
            Handle handle = Features[i];
            if(handle.Instance != null && !ShouldRun(handle)) Teardown(handle);
        }
        foreach(Handle handle in Features)
            if(handle.Instance == null && handle.Error == null && ShouldRun(handle)) Instantiate(handle);
        ApplyActive();
    }
    private static bool ShouldRun(Handle handle) {
        if(!handle.Enabled) return false;
        foreach(string dep in handle.Info.Deps)
            if(Find(dep) is not { Loaded: true }) return false;
        return true;
    }
    private static void RebuildUI() {
        if(UICore.Pages.Count == 0) return;
        if(Quartz.UI.Nav.NavRegistry.ByState(UICore.CurrentMenuState) == null)
            UICore.CurrentMenuState = Quartz.UI.Nav.NavRegistry.FirstVisibleState();
        UICore.Rebuild();
    }
    private static void Instantiate(Handle handle) {
        try {
            QuartzFeature instance = (QuartzFeature)Activator.CreateInstance(handle.Entry);
            handle.Context = new FeatureContext(handle.Info);
            instance.Context = handle.Context;
            handle.Instance = instance;
            handle.Ticks = handle.Entry.GetMethod(nameof(QuartzFeature.OnTick))?.DeclaringType != typeof(QuartzFeature);
            instance.OnLoad();
        } catch(Exception e) {
            handle.Error = $"OnLoad threw: {e}";
            MainCore.Log.Err($"[Feature:{handle.Id}] {handle.Error}");
            handle.Context?.Cleanup();
            handle.Context = null;
            handle.Instance = null;
            handle.Ticks = false;
        }
    }
    private static void ApplyActive(bool unpatchOnDisable = true) {
        foreach(Handle handle in Features) {
            bool should = MainCore.IsModEnabled && handle.Enabled && handle.Loaded;
            if(should == handle.Active) continue;
            if(should) {
                try {
                    FeatureContext context = handle.Context;
                    context?.ApplyPatches();
                    // Mark active before OnEnable so a throwing enable still unwinds its patches.
                    handle.Active = true;
                    handle.Instance.OnEnable();
                    context?.RunEnableSteps();
                } catch(Exception e) {
                    handle.Error = $"OnEnable threw: {e}";
                    MainCore.Log.Err($"[Feature:{handle.Id}] OnEnable threw: {e}");
                    SafeDisable(handle);
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
            MainCore.Log.Err($"[Feature:{handle.Id}] OnDisable threw: {e}");
        }
        if(unpatch) handle.Context?.RemovePatches();
    }
    private static void Teardown(Handle handle) {
        SafeDisable(handle);
        if(handle.Instance != null) {
            try {
                handle.Instance.OnUnload();
            } catch(Exception e) {
                MainCore.Log.Err($"[Feature:{handle.Id}] OnUnload threw: {e}");
            }
        }
        handle.Context?.Cleanup();
        handle.Instance = null;
        handle.Context = null;
        handle.Ticks = false;
    }
}
