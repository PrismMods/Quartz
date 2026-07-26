using HarmonyLib;
using Quartz.Core;
using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.Addons;
public sealed class AddonContext {
    public string Id { get; }
    private HarmonyLib.Harmony harmony;
    private readonly List<string> statIds = [];
    private readonly List<string> tagNames = [];
    private object settings;
    internal AddonContext(string id) => Id = id;
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
            harmony?.UnpatchAll(harmony.Id);
        } catch(Exception e) {
            MainCore.Log.Err($"[Addon:{Id}] unpatch failed: {e}");
        }
        harmony = null;
        foreach(string statId in statIds) Quartz.Overlay.StatRegistry.Unregister(statId);
        statIds.Clear();
        foreach(string tagName in tagNames) AddonTags.Unregister(tagName);
        tagNames.Clear();
        Quartz.UI.Nav.NavRegistry.RemoveOwner(Id);
        if(settings is IO.ISettingsHandle handle) {
            handle.Save();
            IO.SettingsRegistry.Unregister(handle);
        }
        settings = null;
    }
}
