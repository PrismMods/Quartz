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
/// Every page is drawn in the menu or stays undrawn for a reason the spec gives.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator Coverage(Action<string, bool, string> check, Action<string> info) {
        var was = new Dictionary<string, bool>();
        var turnedOn = new List<string>();
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
            check("every page is drawn natively or stays undrawn for a recorded reason", pages is { Count: > 0 } && bare.Count == 0,
                $"bare={string.Join(",", bare)} turned on={string.Join(",", turnedOn)} pages={string.Join("; ", routes)}");
            check("no page is partly moved", typeof(QuartzShell).GetField("PartlyMoved", BindingFlags.Static | BindingFlags.NonPublic) == null, "");
        } finally {
            foreach(FeatureService.Handle h in FeatureService.Features)   // a dependency turned on with a feature goes back too
                if(was.TryGetValue(h.Id, out bool on) && h.Enabled != on) FeatureService.SetEnabledForRun(h.Id, on);
        }
        yield return Frames(2);
    }
}
#endif
