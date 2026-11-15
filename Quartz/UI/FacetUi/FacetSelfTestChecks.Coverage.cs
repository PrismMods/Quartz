#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Quartz.Addons;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.Tuf;
using Quartz.UI.Nav;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Every classic page is drawn in the new menu or stays classic for a reason the spec gives, and the classic menu still opens
/// each page the new menu took over.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static readonly string[] ClassicStillOpens = ["profiles.main", "import.main", CorePages.AddonsPageKey, "overlay.panels", "tuf.levels", "tuf.packs"];

    private static IEnumerator Coverage(Action<string, bool, string> check, Action<string> info) {
        var keys = new List<string>(ClassicStillOpens);
        if(Info.IsDev) keys.Insert(0, "developer.main");
        else info("coverage: developer.main is not opened in the classic menu: not a dev build");
        var was = new Dictionary<string, bool>();
        var turnedOn = new List<string>();
        var wrong = new List<string>();
        int updates = SelfTestDoor.UpdateChecks;
        bool checking = false;
        try {
            // Every feature on for the run, in memory: a feature that is off registers only its stub, not its pages.
            foreach(FeatureService.Handle h in FeatureService.Features) was[h.Id] = h.Enabled;
            foreach(FeatureService.Handle h in FeatureService.Features)
                if(!FeatureService.IsEnabled(h.Id)) {
                    FeatureService.SetEnabledForRun(h.Id, true);
                    turnedOn.Add(h.Id);
                }
            if(turnedOn.Count > 0) yield return Frames(3);

            // Hidden pages too: the registry's visible lists follow each page's predicate (the flavour, dev builds).
            var pages = typeof(NavRegistry).GetField("pages", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as List<NavPage>;
            var routes = new List<string>();
            var bare = new List<string>();
            foreach(NavPage page in pages ?? []) {
                bool tab = AddonSettingsTabs.TryGet(page.Key, out _);
                bool addon = page.OwnerId != null && FeaturePages.FeatureOf(page) == null;   // a feature's pages are owned too
                string reason = PageRules.StaysClassic(page.Key, addon, tab);
                string route = QuartzShell.Moved.ContainsKey(page.Key) ? "Moved" : tab ? "settings tab" : FeaturePages.IsStub(page) ? "stub" : null;
                if(QuartzShell.IsClassic(page) && reason == null) bare.Add(page.Key);
                routes.Add($"{page.Key}={route ?? reason ?? "none"}");
            }
            check("every classic page is drawn natively or stays classic for a recorded reason", pages is { Count: > 0 } && bare.Count == 0,
                $"bare={string.Join(",", bare)} turned on={string.Join(",", turnedOn)} pages={string.Join("; ", routes)}");
            check("no page is partly moved", typeof(QuartzShell).GetField("PartlyMoved", BindingFlags.Static | BindingFlags.NonPublic) == null, "");

            foreach(string key in keys) {
                int state = NavRegistry.StateFor(key);
                FacetBridge.OpenNewMenu(CorePages.SettingsPageKey);
                yield return Frames(2);
                QuartzShell.OpenClassic(state);
                yield return Frames(3);
                bool drawn = state >= 0 && UICore.IsOpen && UICore.CurrentMenuState == state
                    && UICore.Pages.TryGetValue(state, out var root) && root != null && root.gameObject.activeSelf;
                UICore.Close();
                yield return Frames(3);
                if(!drawn || !FacetBridge.NewMenuVisible() || UICore.IsOpen) wrong.Add($"{key}(state={state} drawn={drawn} {Showing()})");
                checking |= TufService.Instance?.CheckingUpdates == true;
            }
        } finally {
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            TufService.Instance?.ForgetForTest();
            TufPackService.Instance?.ForgetForTest();
            foreach(FeatureService.Handle h in FeatureService.Features)   // a dependency turned on with a feature goes back too
                if(was.TryGetValue(h.Id, out bool on) && h.Enabled != on) FeatureService.SetEnabledForRun(h.Id, on);
        }
        yield return Frames(2);
        check("every in-scope classic page still opens in the classic menu", wrong.Count == 0 && SelfTestDoor.Network == 0
            && SelfTestDoor.UpdateChecks == updates && !checking,
            $"pages={string.Join(",", keys)} wrong={string.Join("; ", wrong)} network={SelfTestDoor.Network} "
            + $"update checks={SelfTestDoor.UpdateChecks - updates} checking={checking}");
    }
}
#endif
