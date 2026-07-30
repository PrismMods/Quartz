using Quartz.Core;
using Quartz.IO;
using Quartz.IO.Interface;
using Quartz.Overlay;
using Quartz.Resource;
using Quartz.UI.Nav;
using UnityEngine;
namespace Quartz.Modules;
public sealed class ModuleContext {
    public string Id { get; }
    public ModuleManifest Manifest { get; }
    private HarmonyLib.Harmony harmony;
    private readonly List<string> statIds = [];
    private readonly List<string> tagNames = [];
    private readonly List<string> rescaleIds = [];
    private readonly List<string> spriteKeys = [];
    private readonly List<string> bandIds = [];
    private readonly List<ISettingsHandle> settings = [];
    private readonly List<(string Name, Action Step)> enableSteps = [];
    private readonly List<(string Name, Action Step)> disableSteps = [];
    private int pageCount;
    private bool injectedKeySource;
    private bool importHandlers;
    private readonly List<string> homeCards = [];
    internal ModuleContext(ModuleManifest manifest) {
        Manifest = manifest;
        Id = manifest.Id;
    }
    public void Msg(string message) => MainCore.Log.Msg($"[Module:{Id}] {message}");
    public void Wrn(string message) => MainCore.Log.Wrn($"[Module:{Id}] {message}");
    public void Err(string message) => MainCore.Log.Err($"[Module:{Id}] {message}");
    public HarmonyLib.Harmony Harmony => harmony ??= new HarmonyLib.Harmony("quartz.module." + Id);
    private System.Reflection.Assembly patchAssembly;
    private readonly ModulePatchGate patchGate = new();
    public void PatchAll(Type anyTypeInModule) {
        if(anyTypeInModule == null) throw new ArgumentNullException(nameof(anyTypeInModule));
        patchAssembly = anyTypeInModule.Assembly;
        if(patchGate.SourceArrived()) ApplyPatchClasses();
    }
    internal void ApplyPatches() {
        if(patchGate.Want()) ApplyPatchClasses();
    }
    private void ApplyPatchClasses() {
        IEnumerable<Type> types;
        try {
            types = Quartz.Plugins.PluginEntryScan.Types(patchAssembly);
        } catch(Exception e) {
            Err($"couldn't enumerate patch classes: {e.Message}");
            return;
        }
        foreach(Type type in types) {
            try {
                Harmony.CreateClassProcessor(type).Patch();
            } catch(Exception e) {
                Err($"skipped patch class {type.FullName}: {e.Message}");
            }
        }
    }
    internal void RemovePatches() {
        if(!patchGate.Release()) return;
        try {
            harmony?.UnpatchAll(harmony.Id);
        } catch(Exception e) {
            Err($"unpatch failed: {e}");
        }
        harmony = null;
    }
    public SettingsFile<T> Settings<T>(string fileName) where T : class, ISettingsFile, new() {
        SettingsFile<T> file = SettingsFile<T>.Loaded(fileName);
        settings.Add(file);
        return file;
    }
    public NavPage AddPage(NavPage page) {
        if(page == null) throw new ArgumentNullException(nameof(page));
        page.OwnerId = Id;
        if(string.IsNullOrWhiteSpace(page.Key)) page.Key = Id + ".page" + pageCount;
        pageCount++;
        NavPage existing = NavRegistry.ByKey(page.Key);
        if(existing != null) {
            Wrn($"page '{page.Key}' is already registered by '{existing.OwnerId ?? "core"}' — keeping that one");
            return existing;
        }
        return NavRegistry.AddPage(page);
    }
    public void RegisterStat(string statId, string label, string category, Func<string> valueProvider) {
        if(valueProvider == null) throw new ArgumentNullException(nameof(valueProvider));
        bool reported = false;
        StatRegistry.Register(new StatSource {
            Id = statId,
            Label = label,
            Category = string.IsNullOrEmpty(category) ? Manifest.Name : category,
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
        Quartz.Addons.AddonTags.Register(name, () => {
            try {
                return valueProvider();
            } catch(Exception e) {
                Err($"tag '{name}' threw: {e.Message}");
                return "";
            }
        });
        tagNames.Add(name);
    }
    public void RegisterRescalable(string id, Action<float, float> rescale) {
        OverlayRescale.Register(id, rescale);
        rescaleIds.Add(id);
    }
    public void ReserveOverlayBand(string id, Func<float> bottomEdge) {
        OverlayLayout.Reserve(id, bottomEdge);
        bandIds.Add(id);
    }
    public ResourceManager Resources(Type anyTypeInModule, string prefix) =>
        new(anyTypeInModule.Assembly, prefix);
    public bool RegisterTranslations(string json) => MainCore.Tr.Merge(json, "module " + Id);
    private bool RegisterTranslations(string json, string resourceName) =>
        MainCore.Tr.Merge(json, "module " + Id + " " + resourceName);
    public bool RegisterTranslations(Type anyTypeInModule, string resourceName) {
        try {
            using Stream stream = anyTypeInModule.Assembly.GetManifestResourceStream(resourceName);
            if(stream == null) {
                Wrn($"translation resource '{resourceName}' not found");
                return false;
            }
            using StreamReader reader = new(stream);
            return RegisterTranslations(reader.ReadToEnd(), resourceName);
        } catch(Exception e) {
            Err($"couldn't read translations '{resourceName}': {e.Message}");
            return false;
        }
    }
    public void RegisterInjectedKeySource(Func<UnityEngine.KeyCode, bool> isInjected, Func<bool> guardActive) {
        Quartz.Game.InjectedKeys.Register(Id, isInjected, guardActive);
        injectedKeySource = true;
    }
    public Quartz.UI.Home.HomeCard AddHomeCard(Quartz.UI.Home.HomeCard card) {
        if(card == null) throw new ArgumentNullException(nameof(card));
        card.OwnerId = Id;
        if(string.IsNullOrWhiteSpace(card.Key)) card.Key = Id + ".home" + homeCards.Count;
        homeCards.Add(card.Key);
        return Quartz.UI.Home.HomeRegistry.Add(card);
    }
    public void RegisterImportHandler(Quartz.Interop.IImportHandler handler) {
        Quartz.Interop.ImportRegistry.Register(Id, handler);
        importHandlers = true;
    }
    public void RegisterSprite(string key, byte[] png) {
        SpriteRegistry.Register(key, png);
        spriteKeys.Add(key);
    }
    public void OnModEnable(string name, Action step) => enableSteps.Add((name, step));
    public void OnModDisable(string name, Action step) => disableSteps.Add((name, step));
    internal void RunEnableSteps() => Run(enableSteps);
    internal void RunDisableSteps() => Run(disableSteps);
    private void Run(List<(string Name, Action Step)> steps) {
        foreach((string name, Action step) in steps) {
            try {
                step();
            } catch(Exception e) {
                Err($"step '{name}' threw: {e}");
            }
        }
    }
    internal void Cleanup() {
        RemovePatches();
        patchGate.Forget();
        patchAssembly = null;
        foreach(string statId in statIds) StatRegistry.Unregister(statId);
        statIds.Clear();
        foreach(string tagName in tagNames) Quartz.Addons.AddonTags.Unregister(tagName);
        tagNames.Clear();
        foreach(string id in rescaleIds) OverlayRescale.Unregister(id);
        rescaleIds.Clear();
        foreach(string id in bandIds) OverlayLayout.Release(id);
        bandIds.Clear();
        foreach(string key in spriteKeys) SpriteRegistry.Unregister(key);
        spriteKeys.Clear();
        if(injectedKeySource) {
            Quartz.Game.InjectedKeys.Unregister(Id);
            injectedKeySource = false;
        }
        if(importHandlers) {
            Quartz.Interop.ImportRegistry.Unregister(Id);
            importHandlers = false;
        }
        MainCore.Tr.Forget("module " + Id);
        if(homeCards.Count > 0) {
            Quartz.UI.Home.HomeRegistry.RemoveOwner(Id);
            homeCards.Clear();
        }
        NavRegistry.RemoveOwner(Id);
        foreach(ISettingsHandle handle in settings) {
            try {
                handle.Save();
                SettingsRegistry.Unregister(handle);
            } catch(Exception e) {
                Err($"saving settings failed: {e.Message}");
            }
        }
        settings.Clear();
        enableSteps.Clear();
        disableSteps.Clear();
    }
}
