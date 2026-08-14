using HarmonyLib;
using Quartz.Core;
using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.Addons;
public enum QuartzLoader {
    MelonLoader,
    UnityModManager,
}
public sealed class AddonContext {
    public static QuartzLoader Loader =>
#if QUARTZ_UMM
        QuartzLoader.UnityModManager;
#else
        QuartzLoader.MelonLoader;
#endif
    public static bool IsMelonLoader => Loader == QuartzLoader.MelonLoader;
    public static bool IsUnityModManager => Loader == QuartzLoader.UnityModManager;
    public string Id { get; }
    public string DataPath {
        get {
            string path = Path.Combine(MainCore.Paths.AddonsPath, Id);
            Directory.CreateDirectory(path);
            return path;
        }
    }
    private HarmonyLib.Harmony harmony;
    private readonly List<string> statIds = [];
    private readonly List<string> tagNames = [];
    private readonly List<(string Label, Action Run)> actions = [];
    internal IReadOnlyList<(string Label, Action Run)> Actions => actions;
    private object settings;
    private object settingsData;
    private object settingsDefaults;
    private bool mergedTranslations;
    internal AddonContext(string id) => Id = id;
    public static bool IsAddonLoaded(string id) => AddonService.FindLoaded(id) != null;
    public static object GetAddonApi(string id) => AddonService.ApiOf(id);
    public static object CallAddon(string id, params object[] args)
        => AddonService.FindLoaded(id)?.OnCall(args ?? []);
    public void Msg(string message) => MainCore.Log.Msg($"[Addon:{Id}] {message}");
    public void Wrn(string message) => MainCore.Log.Wrn($"[Addon:{Id}] {message}");
    public void Err(string message) => MainCore.Log.Err($"[Addon:{Id}] {message}");
    public HarmonyLib.Harmony Harmony => harmony ??= new HarmonyLib.Harmony("quartz.addon." + Id);
    public void PatchAll(Type anyTypeInAddon) =>
        Harmony.PatchAll(anyTypeInAddon.Assembly);
    public T GetSettings<T>() where T : class, new() {
        if(settings is AddonSettings<T> existing) return existing.Data;
        if(settings != null) throw new InvalidOperationException($"addon '{Id}' already loaded settings of type {settings.GetType()}");
        AddonSettings<T> file = new(Path.Combine(MainCore.Paths.RootPath, $"Addon.{Id}.json"));
        file.Load();
        settings = file;
        settingsData = file.Data;
        settingsDefaults = new T();
        return file.Data;
    }
    public void SaveSettings() {
        switch(settings) {
            case null: return;
            case IO.ISettingsHandle handle: handle.Save(); break;
        }
    }
    public void RegisterStat(string statId, string label, string category, Func<string> valueProvider) {
        bool reported = false;
        Quartz.Overlay.StatRegistry.Register(new Quartz.Overlay.StatSource {
            Id = statId,
            Label = label,
            Category = string.IsNullOrEmpty(category) ? "Addons" : category,
            Value = () => {
                try {
                    return valueProvider();
                } catch(Exception e) {
                    if(!reported) {
                        reported = true;
                        Err($"stat '{statId}' threw (line hidden): {e}");
                    }
                    return null;
                }
            },
        });
        statIds.Add(statId);
    }
    public void RegisterTag(string name, Func<string> valueProvider) {
        if(valueProvider == null) throw new ArgumentNullException(nameof(valueProvider));
        AddonTags.Register(name, () => {
            try {
                return valueProvider();
            } catch(Exception e) {
                Err($"tag '{name}' threw: {e.Message}");
                return "";
            }
        });
        tagNames.Add(name);
    }
    public void RegisterAction(string label, Action action) {
        if(action == null) throw new ArgumentNullException(nameof(action));
        actions.Add((label, () => {
            try {
                action();
            } catch(Exception e) {
                Err($"action '{label}' threw: {e}");
            }
        }));
    }
    public bool RegisterTranslations(string json) {
        mergedTranslations = true;
        return MainCore.Tr.Merge(json, "addon " + Id);
    }
    public bool RegisterTranslations(Type anyTypeInAddon, string resourceName) {
        try {
            using Stream stream = anyTypeInAddon.Assembly.GetManifestResourceStream(resourceName);
            if(stream == null) {
                Wrn($"translation resource '{resourceName}' not found");
                return false;
            }
            using StreamReader reader = new(stream);
            mergedTranslations = true;
            return MainCore.Tr.Merge(reader.ReadToEnd(), "addon " + Id + " " + resourceName);
        } catch(Exception e) {
            Err($"couldn't read translations '{resourceName}': {e.Message}");
            return false;
        }
    }
    public void RegisterSettingsTab(string title = null) {
        if(settingsData == null)
            throw new InvalidOperationException($"addon '{Id}' must call GetSettings<T>() before RegisterSettingsTab()");
        object data = settingsData;
        object defaults = settingsDefaults;
        RegisterTab(title ?? Id, content => AddonSettingsUI.Build(content, this, data, defaults, SaveSettings));
    }
    public void RegisterTab(string title, Action<Transform> build) =>
        Quartz.UI.Nav.NavRegistry.AddPage(new Quartz.UI.Nav.NavPage {
            Key = "addon." + Id + "." + Quartz.UI.Generator.GenerateUI.LocaleKeyFromText("", title),
            CategoryKey = Quartz.UI.Nav.CorePages.AddonsCategoryKey,
            Order = 100 + tabCount++,
            Title = title,
            LocaleKey = GenerateUI.LocaleKeyFromText("ADDON_", title),
            OwnerId = Id,
            OwnScroll = false,
            Build = content => build(content),
        });
    private int tabCount;
    internal void Cleanup() {
        try {
            Quartz.Compat.HarmonyCompat.UnpatchOwn(harmony);
        } catch(Exception e) {
            MainCore.Log.Err($"[Addon:{Id}] unpatch failed: {e}");
        }
        harmony = null;
        foreach(string statId in statIds) Quartz.Overlay.StatRegistry.Unregister(statId);
        statIds.Clear();
        foreach(string tagName in tagNames) AddonTags.Unregister(tagName);
        tagNames.Clear();
        actions.Clear();
        if(mergedTranslations) {
            MainCore.Tr.Forget("addon " + Id);
            mergedTranslations = false;
        }
        Quartz.UI.Nav.NavRegistry.RemoveOwner(Id);
        if(settings is IO.ISettingsHandle handle) {
            handle.Save();
            IO.SettingsRegistry.Unregister(handle);
        }
        settings = null;
        settingsData = null;
        settingsDefaults = null;
    }
}
