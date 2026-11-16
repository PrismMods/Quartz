#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Addons;
using Quartz.Localization;
using Quartz.UI.Nav;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// A page drawn for the old menu: it stands in for a custom addon page (an addon's own uGUI tab, which the menu shows as a
/// note that it cannot be drawn). Only the checks that use it register it.
/// </summary>
internal static class FacetSelfTestFixture {
    internal const string PageKey = "addon.selftest.classic";
    internal const string Owner = "selftest", Title = "Self-test";

    /// <summary>Adds the page, as an addon's registration does, then waits for it to be registered.</summary>
    internal static IEnumerator Register() {
        if(NavRegistry.ByKey(PageKey) == null)
            NavRegistry.AddPage(new NavPage {
                Key = PageKey,
                CategoryKey = CorePages.AddonsCategoryKey,
                Order = 999,
                Title = Title,
                LocaleKey = "ADDON_SELF_TEST",   // no Lang entry, like a real addon page's key
                OwnerId = Owner,
                Build = _ => { },   // a uGUI builder, which the menu cannot draw
            });
        for(int i = 0; i < 60 && !Ready(); i++) yield return null;
    }

    /// <summary>Takes the page out again.</summary>
    internal static IEnumerator Unregister() {
        Forget();
        for(int i = 0; i < 60 && NavRegistry.StateFor(PageKey) >= 0; i++) yield return null;
    }

    /// <summary>
    /// The page out of the registry, also from a section's finally when its checks
    /// threw: a later section must not find the stale page there. Nothing when it is already gone.
    /// </summary>
    internal static void Forget() {
        if(NavRegistry.ByKey(PageKey) == null) return;
        NavRegistry.RemoveOwner(Owner);
    }

    /// <summary>A settings tab, as an addon's RegisterSettingsTab("Settings") adds it: one member of every kind. Its save counts
    /// calls and writes no file.</summary>
    internal static readonly string TabKey = "addon." + Owner + "." + LocaleKeys.FromText("", "Settings");
    internal static int Saves;
    internal static AddonContext Context;
    internal static TabSettings Data;
    internal static AddonSettingsTabs.Tab Tab;

    internal enum TabMode { FastMode, SlowerMode2 }

    internal sealed class TabSettings {
        public bool Enabled = true;
        public TabMode Mode = TabMode.FastMode;
        [Choices("Alpha", "Beta")] public string Pick = "Alpha";
        [Slider(0f, 10f)] public int Count = 3;
        [Slider(0f, 1f, 0.1f)] public float Volume = 0.5f;
        public double Ratio = 1.5;
        public int? Limit = 5;
        [Section("Look"), Name("Shown title"), Desc("A described row.")] public string Title = "Alpha";
        public Color Tint = Color.red;
        public Dictionary<string, string> Map = new() { ["a"] = "1" };
        public List<KeyValuePair<string, string>> Pairs = [new("k", "v")];
        [ReloadRequired] public bool Heavy = false;
        [Ignore] public bool Hidden = false;
    }

    internal static void RegisterSettingsTab() {
        if(NavRegistry.ByKey(TabKey) != null) return;
        Context = new AddonContext(Owner);
        Data = new TabSettings();
        var defaults = new TabSettings();
        AddonContext ctx = Context;
        TabSettings data = Data;
        NavRegistry.AddPage(new NavPage {
            Key = TabKey,
            CategoryKey = CorePages.AddonsCategoryKey,
            Order = 100,
            Title = "Settings",
            LocaleKey = LocaleKeys.FromText("ADDON_", "Settings"),
            OwnerId = Owner,
        });
        Tab = new AddonSettingsTabs.Tab(TabKey, Owner, data, defaults, () => Saves++, ctx.Wrn);
        AddonSettingsTabs.Register(Tab);
    }

    internal static void UnregisterSettingsTab() {
        NavRegistry.RemoveOwner(Owner);
        AddonSettingsTabs.RemoveAddon(Owner);
    }

    private static bool Ready() => NavRegistry.StateFor(PageKey) >= 0;
}
#endif
