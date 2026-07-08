using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.IO;
using Quartz.IO.Interface;
using Quartz.UI;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace Quartz.Addons;

// Persisted addon state: which addons the user toggled off. Everything else
// (metadata, errors) is derived fresh from the files each load.
public sealed class AddonsSettings : ISettingsFile {
    public Dictionary<string, bool> Enabled = new(StringComparer.OrdinalIgnoreCase);

    public JToken Serialize() {
        JObject enabled = new();
        foreach(var kvp in Enabled) enabled[kvp.Key] = kvp.Value;
        return new JObject { [nameof(Enabled)] = enabled };
    }

    public void Deserialize(JToken token) {
        Enabled.Clear();
        if(token?[nameof(Enabled)] is not JObject enabled) return;
        foreach(var prop in enabled.Properties())
            Enabled[prop.Name] = prop.Value.Type == JTokenType.Boolean && (bool)prop.Value;
    }
}

// Discovers and drives precompiled user addons from UserData/Quartz/Addons.
//
// Discovery units:
//   Addons/Baz.qaddon    one addon, precompiled against the QuartzAddon SDK
//                        (a plain .dll works too; id "Baz"). Loaded from a byte
//                        copy so the file stays unlocked — Reload picks up a
//                        rebuilt .qaddon, and Remove can delete it, on every OS.
// Names starting with '.' or '_' are skipped (a quick way to park a file), as
// are .example files.
//
// A .qaddon is just a compiled assembly with a branded extension: modders
// build against sdk/QuartzAddon.dll (the mod's public reference assembly) so
// their editor and compiler check the addon before it ever loads, then output
// the result as .qaddon. Both .qaddon and .dll load the same way. Loose .cs
// source is NOT compiled at runtime — addons ship precompiled.
public static class AddonService {
    public sealed class Handle {
        public string Id;
        // The file-derived id. Keys the persisted enabled map — the
        // instance-declared Id isn't known while an addon is parked
        // (disabled units are never loaded), so the toggle must key on
        // something derivable from the file alone.
        public string UnitId;
        public string Name;
        public string Version;
        public string Author;
        public string SourcePath;
        public bool Enabled;
        // Compile/load/runtime failure text shown on the Addons page.
        public string Error;
        public QuartzAddon Instance;
        internal AddonContext Context;
        // OnEnable delivered and OnDisable not yet — i.e. currently running.
        internal bool Active;
        public bool Loaded => Instance != null && Error == null;
    }

    private static readonly List<Handle> handles = [];
    public static IReadOnlyList<Handle> Addons => handles;

    private static SettingsFile<AddonsSettings> confMgr;

    private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> sceneHandler;
    private static Action<bool, bool> modChangedHandler;
    private static ResolveEventHandler modIdentityResolver;
    private static bool initialized;

    // Adapters so QuartzRuntime can slot the static service into its
    // services/ticks lists (same shape as Optimizer.Ticker).
    public static readonly IRuntimeService Service = new ServiceAdapter();
    public static readonly IRuntimeTick Ticker = new TickAdapter();

    private sealed class ServiceAdapter : IRuntimeService {
        public void Initialize() => AddonService.Initialize();
        public void Dispose() => AddonService.Dispose();
    }

    private sealed class TickAdapter : IRuntimeTick {
        public void Tick() => AddonService.Tick();
    }

    private static void Initialize() {
        if(initialized) return;
        initialized = true;

        HookModIdentityResolver();

        confMgr = SettingsFile<AddonsSettings>.Loaded("Addons.json");

        sceneHandler = (scene, _) => AddonEvents.RaiseSceneLoaded(scene);
        SceneManager.sceneLoaded += sceneHandler;

        modChangedHandler = (enabled, _) => {
            AddonEvents.RaiseModEnabledChanged(enabled);
            ApplyActive();
        };
        MainCore.OnModEnabledChanged += modChangedHandler;

        LoadAll();
    }

    private static void Dispose() {
        if(!initialized) return;
        initialized = false;

        UnloadAll();
        AddonUI.Clear();
        AddonEvents.Clear();
        AddonTags.Clear();

        if(sceneHandler != null) {
            SceneManager.sceneLoaded -= sceneHandler;
            sceneHandler = null;
        }
        if(modChangedHandler != null) {
            MainCore.OnModEnabledChanged -= modChangedHandler;
            modChangedHandler = null;
        }

        UnhookModIdentityResolver();
        confMgr?.Save();
        confMgr = null;
    }

    // Precompiled addons (.qaddon/.dll) are built against sdk/QuartzAddon.dll,
    // whose assembly identity is "Quartz". The UMM build ships the mod under a
    // DIFFERENT identity ("QuartzUmm", so the ML and UMM copies can coexist —
    // see Quartz.csproj), so a precompiled addon's "Quartz" reference would
    // fail to bind there. Unify both aliases onto whichever mod assembly is
    // actually loaded, so a single .qaddon runs on either loader. Fires only on
    // bind failure: under the ML build "Quartz" resolves normally and the
    // runtime never consults this.
    private static void HookModIdentityResolver() {
        if(modIdentityResolver != null) return;
        Assembly mod = typeof(QuartzAddon).Assembly;
        modIdentityResolver = (_, args) => {
            string name = new AssemblyName(args.Name).Name;
            return name is "Quartz" or "QuartzUmm" ? mod : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += modIdentityResolver;
    }

    private static void UnhookModIdentityResolver() {
        if(modIdentityResolver == null) return;
        AppDomain.CurrentDomain.AssemblyResolve -= modIdentityResolver;
        modIdentityResolver = null;
    }

    private static void Tick() {
        for(int i = 0; i < handles.Count; i++) {
            Handle handle = handles[i];
            if(!handle.Active) continue;
            try {
                handle.Instance.OnTick();
            } catch(Exception e) {
                // Park the addon instead of erroring every frame.
                handle.Error = $"OnTick threw: {e}";
                MainCore.Log.Err($"[Addon:{handle.Id}] OnTick threw — addon stopped: {e}");
                SafeDisable(handle);
            }
        }
    }

    // Flips each addon's running state to match (mod on) && (addon enabled)
    // && (no error). Delivers OnEnable/OnDisable exactly on transitions.
    private static void ApplyActive() {
        foreach(Handle handle in handles) {
            bool should = MainCore.IsModEnabled && handle.Enabled && handle.Loaded;
            if(should == handle.Active) continue;
            if(should) {
                try {
                    handle.Instance.OnEnable();
                    handle.Active = true;
                } catch(Exception e) {
                    handle.Error = $"OnEnable threw: {e}";
                    MainCore.Log.Err($"[Addon:{handle.Id}] OnEnable threw: {e}");
                }
            } else {
                SafeDisable(handle);
            }
        }
    }

    private static void SafeDisable(Handle handle) {
        if(!handle.Active) return;
        handle.Active = false;
        try {
            handle.Instance.OnDisable();
        } catch(Exception e) {
            MainCore.Log.Err($"[Addon:{handle.Id}] OnDisable threw: {e}");
        }
    }

    // Disables + unloads a single addon in place (OnDisable, OnUnload, context
    // cleanup, instance/context null-out) without touching the handles list —
    // shared by RemoveAddon (one handle) and UnloadAll (every handle).
    private static void TeardownHandle(Handle handle) {
        SafeDisable(handle);
        if(handle.Instance != null) {
            try {
                handle.Instance.OnUnload();
            } catch(Exception e) {
                MainCore.Log.Err($"[Addon:{handle.Id}] OnUnload threw: {e}");
            }
        }
        handle.Context?.Cleanup();
        handle.Instance = null;
        handle.Context = null;
    }

    // The user toggled an addon on the Addons page. Enabling may need OnLoad
    // to run (registrations happen there), so both directions go through a
    // full reload; deferred because the click arrives from a UI widget the
    // rebuild is about to destroy.
    public static void SetAddonEnabled(Handle handle, bool enabled) {
        if(handle.Enabled == enabled) return;
        handle.Enabled = enabled;
        confMgr.Data.Enabled[handle.UnitId] = enabled;
        confMgr.RequestSave();
        MainThread.Enqueue(Reload);
    }

    // Tear down every addon and re-discover from disk. Rebuilds the settings
    // panel so addon tabs/stats reflect the new state.
    public static void Reload() {
        if(!initialized) return;

        UnloadAll();
        AddonEvents.Clear();
        LoadAll();
        ApplyActive();

        // Rebuild the settings UI when it exists. If the current page was an
        // addon page from the previous generation, retarget the Addons page
        // first — CreatePages indexes UICore.Pages[CurrentMenuState].
        if(UICore.Pages.Count > 0) {
            if(AddonUI.IsAddonState(UICore.CurrentMenuState)
               && !AddonUI.Pages.Any(p => p.State == UICore.CurrentMenuState)) {
                UICore.CurrentMenuState = (int)OriginalMenuState.Addons;
            }
            UICore.Rebuild();
        }
    }

    public static void OpenAddonsFolder() {
        string path = MainCore.Paths.AddonsPath;
        try {
            Directory.CreateDirectory(path);
            UnityEngine.Application.OpenURL("file://" + path.Replace('\\', '/'));
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] couldn't open '{path}': {e.Message}");
        }
    }

    // File extensions accepted by ImportAddon / the "Add Addon" picker.
    public static readonly string[] ImportExtensions = ["qaddon", "dll"];

    // Copies a precompiled addon (.qaddon / .dll) into the Addons folder and
    // reloads. Non-destructive: a name clash is suffixed rather than
    // overwritten. Returns the destination file name, or null on failure
    // (unsupported extension, missing source, IO error).
    public static string ImportAddon(string sourcePath) {
        if(string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return null;

        string ext = Path.GetExtension(sourcePath).TrimStart('.');
        if(!ImportExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase))) {
            MainCore.Log.Err($"[Addons] can't add '{sourcePath}': only {string.Join("/", ImportExtensions.Select(e => "." + e))} files");
            return null;
        }

        try {
            string root = MainCore.Paths.AddonsPath;
            Directory.CreateDirectory(root);

            string dest = UniqueDestination(Path.Combine(root, Path.GetFileName(sourcePath)));
            File.Copy(sourcePath, dest);
            MainCore.Log.Msg($"[Addons] added '{Path.GetFileName(dest)}'");
            MainThread.Enqueue(Reload);
            return Path.GetFileName(dest);
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] couldn't add '{sourcePath}': {e}");
            return null;
        }
    }

    // "Foo.cs" -> "Foo.cs", or "Foo (2).cs" if taken, "Foo (3).cs", …
    private static string UniqueDestination(string path) {
        if(!File.Exists(path) && !Directory.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path);
        string stem = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for(int i = 2; ; i++) {
            string candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if(!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    // Deletes an addon's file (or folder) from disk, along with its settings
    // file and persisted enable state, then reloads. Precompiled addons are
    // shadow-loaded from bytes (see LoadPrecompiled), so their file isn't locked
    // and this works while they're loaded on every OS. Returns false only if the
    // source genuinely couldn't be deleted (IO error) — nothing else is touched
    // then. The handle is dead after a true return.
    public static bool RemoveAddon(Handle handle) {
        if(handle == null) return false;

        string path = handle.SourcePath;
        try {
            if(Directory.Exists(path)) Directory.Delete(path, true);
            else if(File.Exists(path)) File.Delete(path);
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] couldn't remove '{path}': {e.Message}");
            return false;
        }

        // Tear this addon down now — Cleanup saves+unregisters its settings —
        // then drop it from the list so the reload below skips the dead handle,
        // and only THEN delete the settings file (after Cleanup, so its
        // teardown save can't recreate the file we just removed).
        TeardownHandle(handle);
        handles.Remove(handle);

        try {
            string settings = Path.Combine(MainCore.Paths.RootPath, $"Addon.{handle.Id}.json");
            if(File.Exists(settings)) File.Delete(settings);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Addons] couldn't remove settings for '{handle.Id}': {e.Message}");
        }

        confMgr.Data.Enabled.Remove(handle.UnitId);
        confMgr.RequestSave();
        MainCore.Log.Msg($"[Addons] removed '{handle.Name}'");
        MainThread.Enqueue(Reload);
        return true;
    }

    private static void UnloadAll() {
        // Reverse order, mirroring RuntimeServices.Dispose.
        for(int i = handles.Count - 1; i >= 0; i--) {
            TeardownHandle(handles[i]);
        }
        handles.Clear();
    }

    // ===== discovery + load =====

    private static void LoadAll() {
        string root = MainCore.Paths.AddonsPath;
        List<(string id, string path)> units = [];

        try {
            Directory.CreateDirectory(root);

            foreach(string file in Directory.GetFiles(root)) {
                string name = Path.GetFileName(file);
                if(Skip(name)) continue;
                if(name.EndsWith(".qaddon", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    units.Add((Path.GetFileNameWithoutExtension(file), file));
            }
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] scan failed: {e}");
            return;
        }

        if(units.Count == 0) return;

        foreach(var unit in units) {
            Handle handle = new() {
                Id = unit.id,
                UnitId = unit.id,
                Name = unit.id,
                Version = "",
                Author = "",
                SourcePath = unit.path,
                Enabled = !confMgr.Data.Enabled.TryGetValue(unit.id, out bool en) || en,
            };
            handles.Add(handle);

            if(!handle.Enabled) continue; // parked: listed on the page, not loaded

            Assembly assembly = null;
            try {
                assembly = LoadPrecompiled(unit.path);
            } catch(Exception e) {
                handle.Error = $"failed to load {Path.GetExtension(unit.path)}: {e.Message}";
            }

            if(assembly == null) {
                if(handle.Error != null) MainCore.Log.Err($"[Addon:{handle.Id}] {handle.Error}");
                continue;
            }

            InstantiateAddon(handle, assembly);
        }
    }

    // Loads a precompiled addon from its bytes instead of Assembly.LoadFrom,
    // which pins the file open for the whole process on Windows (assemblies
    // can't be unloaded) — RemoveAddon could then never delete an enabled
    // .qaddon there. Reading the bytes leaves the file unlocked, so removal
    // works on every OS and a Reload picks up a rebuilt .qaddon without a
    // restart. A sibling <name>.pdb, if present, is loaded too so addon
    // stack traces keep file/line frames.
    private static Assembly LoadPrecompiled(string path) {
        byte[] image = File.ReadAllBytes(path);
        string pdb = Path.ChangeExtension(path, ".pdb");
        if(File.Exists(pdb)) {
            try {
                return Assembly.Load(image, File.ReadAllBytes(pdb));
            } catch {
                // Mismatched/unreadable symbols must never block the addon.
            }
        }
        return Assembly.Load(image);
    }

    private static void InstantiateAddon(Handle handle, Assembly assembly) {
        Type[] types;
        try {
            types = assembly.GetTypes();
        } catch(ReflectionTypeLoadException e) {
            types = e.Types.Where(t => t != null).ToArray();
        }

        List<Type> addonTypes = [.. types.Where(t => !t.IsAbstract && typeof(QuartzAddon).IsAssignableFrom(t))];
        if(addonTypes.Count == 0) {
            handle.Error = "no QuartzAddon subclass found — define `public class MyAddon : Quartz.Addons.QuartzAddon`";
            return;
        }
        if(addonTypes.Count > 1)
            MainCore.Log.Wrn($"[Addon:{handle.Id}] {addonTypes.Count} QuartzAddon classes found; using {addonTypes[0].FullName}");

        try {
            QuartzAddon instance = (QuartzAddon)Activator.CreateInstance(addonTypes[0]);

            // Prefer the addon's declared identity, but keep ids unique and
            // filename-safe — the id keys the settings file name and Harmony.
            string id = string.IsNullOrWhiteSpace(instance.Id) ? handle.Id : SanitizeId(instance.Id.Trim());
            if(id.Length == 0) id = handle.Id;
            if(handles.Any(h => h != handle && string.Equals(h.Id, id, StringComparison.OrdinalIgnoreCase))) {
                MainCore.Log.Wrn($"[Addons] duplicate addon id '{id}' ({handle.SourcePath}); suffixing");
                id = $"{id}_{handles.Count}";
            }
            handle.Id = id;
            handle.Name = string.IsNullOrWhiteSpace(instance.Name) ? id : instance.Name;
            handle.Version = instance.Version ?? "";
            handle.Author = instance.Author ?? "";

            handle.Context = new AddonContext(id);
            instance.Context = handle.Context;
            handle.Instance = instance;

            instance.OnLoad();
            MainCore.Log.Msg($"[Addons] loaded '{handle.Name}' v{handle.Version}{(handle.Author.Length > 0 ? $" by {handle.Author}" : "")}");
        } catch(Exception e) {
            handle.Error = $"OnLoad threw: {e}";
            MainCore.Log.Err($"[Addon:{handle.Id}] {handle.Error}");
            handle.Context?.Cleanup();
            handle.Context = null;
            handle.Instance = null;
        }
    }

    private static string SanitizeId(string id) {
        Span<char> buf = stackalloc char[id.Length];
        int n = 0;
        foreach(char c in id)
            if(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ' ') buf[n++] = c;
        return new string(buf[..n]).Trim();
    }

    private static bool Skip(string name) =>
        name.StartsWith('.') || name.StartsWith('_')
        || name.EndsWith(".example", StringComparison.OrdinalIgnoreCase);
}
