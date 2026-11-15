#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Addons;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using Quartz.UI.Search;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// A page that stays in the classic menu, for the hand-off and search checks: it stands in for a custom addon page (an
/// addon's own tab, which the new menu shows as a card that opens it). Only the checks that use it register it.
/// </summary>
internal static class FacetSelfTestFixture {
    internal const string PageKey = "addon.selftest.classic", RowKey = "SELFTEST_CLASSIC_ROW";
    internal const string Owner = "selftest", Title = "Self-test", RowLabel = "Self-test row";

    /// <summary>Adds the page and rebuilds the classic menu, as an addon's registration does, then waits for its search row.</summary>
    internal static IEnumerator Register() {
        if(NavRegistry.ByKey(PageKey) == null)
            NavRegistry.AddPage(new NavPage {
                Key = PageKey,
                CategoryKey = CorePages.AddonsCategoryKey,
                Order = 999,
                Title = Title,
                LocaleKey = "ADDON_SELF_TEST",   // no Lang entry, like a real addon page's key
                OwnerId = Owner,
                Build = content => GenerateUI.Toggle(GenerateUI.Row(content), false, false, _ => { }, RowLabel, "selftest_classic_row"),
            });
        UICore.Rebuild();
        for(int i = 0; i < 60 && !Ready(); i++) yield return null;
    }

    /// <summary>Takes the page out again and rebuilds the classic menu without it.</summary>
    internal static IEnumerator Unregister() {
        Forget();
        for(int i = 0; i < 60 && NavRegistry.StateFor(PageKey) >= 0; i++) yield return null;
    }

    /// <summary>
    /// The page out of the registry and the classic menu rebuilt without it, also from a section's finally when its checks
    /// threw: a later section must not find the stale page there. Nothing when it is already gone.
    /// </summary>
    internal static void Forget() {
        if(NavRegistry.ByKey(PageKey) == null) return;
        NavRegistry.RemoveOwner(Owner);
        UICore.Rebuild();
    }

    /// <summary>A settings tab, as an addon's RegisterSettingsTab("Settings") adds it: one member of every kind. Its save counts
    /// calls and writes no file.</summary>
    internal static readonly string TabKey = "addon." + Owner + "." + GenerateUI.LocaleKeyFromText("", "Settings");
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
            LocaleKey = GenerateUI.LocaleKeyFromText("ADDON_", "Settings"),
            OwnerId = Owner,
            Build = content => AddonSettingsUI.Build(content, ctx, data, defaults, () => Saves++),
        });
        UICore.Rebuild();
        Tab = new AddonSettingsTabs.Tab(TabKey, Owner, data, defaults, () => Saves++, ctx.Wrn);
        AddonSettingsTabs.Register(Tab);
    }

    internal static void UnregisterSettingsTab() {
        NavRegistry.RemoveOwner(Owner);
        AddonSettingsTabs.RemoveAddon(Owner);
        UICore.Rebuild();
    }

    private static bool Ready() =>
        NavRegistry.StateFor(PageKey) is int state && state >= 0 && SettingsSearch.Rows().Any(r => r.Key == RowKey && r.State == state);
}
#endif
