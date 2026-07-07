using HarmonyLib;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.UI.Generator;
using UnityEngine;

namespace Quartz.Addons;

// Per-addon service handle, created by AddonService and injected as
// QuartzAddon.Context before OnLoad. Everything registered through it is
// tracked so a reload/unload can undo the addon's footprint (Harmony patches,
// panel stats, settings pages) without the addon writing teardown code.
public sealed class AddonContext {
    public string Id { get; }

    private HarmonyLib.Harmony harmony;
    private readonly List<string> statIds = [];
    private readonly List<string> tagNames = [];
    private object settings; // the AddonSettings<T> instance, created lazily

    internal AddonContext(string id) => Id = id;

    // === logging ===

    public void Msg(string message) => MainCore.Log.Msg($"[Addon:{Id}] {message}");
    public void Wrn(string message) => MainCore.Log.Wrn($"[Addon:{Id}] {message}");
    public void Err(string message) => MainCore.Log.Err($"[Addon:{Id}] {message}");

    // === Harmony ===

    // The addon's own Harmony instance — patches applied through it are
    // removed automatically on unload/reload.
    public HarmonyLib.Harmony Harmony => harmony ??= new HarmonyLib.Harmony("quartz.addon." + Id);

    // Applies every [HarmonyPatch] class declared by the given type's assembly
    // (pass any addon type, e.g. GetType()). Prefer calling this from OnLoad.
    public void PatchAll(Type anyTypeInAddon) =>
        Harmony.PatchAll(anyTypeInAddon.Assembly);

    // === settings ===

    // The addon's persisted settings (Addon.<id>.json in the data root,
    // included in profile snapshots). One settings type per addon; repeated
    // calls with the same T return the same live instance.
    public T GetSettings<T>() where T : class, new() {
        if(settings is AddonSettings<T> existing) return existing.Data;
        if(settings != null) throw new InvalidOperationException($"addon '{Id}' already loaded settings of type {settings.GetType()}");

        AddonSettings<T> file = new(Path.Combine(MainCore.Paths.RootPath, $"Addon.{Id}.json"));
        file.Load();
        settings = file;
        return file.Data;
    }

    // Persists the settings object GetSettings returned.
    public void SaveSettings() {
        switch(settings) {
            case null: return;
            case IO.ISettingsHandle handle: handle.Save(); break;
        }
    }

    // === panel stats ===

    // Adds a stat to the Panels catalog (shows up in the panel stat picker
    // under the given category). valueProvider runs every frame while a panel
    // shows the stat — return the formatted value, or null to hide the line.
    // Ids share one namespace with built-in stats; prefix with the addon id
    // if in doubt.
    public void RegisterStat(string statId, string label, string category, Func<string> valueProvider) {
        // The delegate runs inside the panel updater's per-frame loop; a
        // throwing addon must degrade to a hidden line, not break every
        // panel. Logged once, not per frame.
        bool reported = false;
        PanelsOverlay.RegisterStat(new PanelsOverlay.StatDef {
            Id = statId,
            Label = label,
            Category = string.IsNullOrEmpty(category) ? "Addons" : category,
            Value = _ => {
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

    // === tags ===

    // Registers a named tag (Overlayer-style registerTag): a value-producer
    // referenced as {name} in any text that supports tag interpolation — the
    // Panels custom-"text" stat today. valueProvider runs each time the text
    // is rendered; return the value (may be multi-line), or "" for nothing.
    // Names are letters/digits/underscore and share one namespace with
    // built-in stat ids (so {fps} etc. also resolve).
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

    // === settings pages ===

    // Adds a page under the Addons sidebar category. build receives the
    // page's scrollable content column; fill it with GenerateUI rows
    // (Toggle/Slider/Button/...). Takes effect when the settings panel next
    // (re)builds — at startup and on every addon reload that's automatic.
    public void RegisterTab(string title, Action<Transform> build) =>
        AddonUI.Register(Id, title, GenerateUI.LocaleKeyFromText("ADDON_", title), build);

    // === teardown (service-owned) ===

    internal void Cleanup() {
        try {
            harmony?.UnpatchSelf();
        } catch(Exception e) {
            MainCore.Log.Err($"[Addon:{Id}] unpatch failed: {e}");
        }
        harmony = null;

        foreach(string statId in statIds) PanelsOverlay.UnregisterStat(statId);
        statIds.Clear();

        foreach(string tagName in tagNames) AddonTags.Unregister(tagName);
        tagNames.Clear();

        AddonUI.UnregisterAddon(Id);

        // Persist a final time, then drop from the profile registry so a
        // later SaveAll/ReloadAll can't touch this now-dead instance.
        if(settings is IO.ISettingsHandle handle) {
            handle.Save();
            IO.SettingsRegistry.Unregister(handle);
        }
        settings = null;
    }
}
