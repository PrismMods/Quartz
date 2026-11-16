#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Prism;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// PrismLib's Smooth scrolling setting in Quartz: its switch on the menu's Settings page. A check that needs it on or off writes scroll.smooth through PrismLib's Config and ticks Quartz
/// once, never Facet's flag (every Prism mod copies the setting over it each frame), and puts the settings file back.
/// </summary>
internal static partial class FacetSelfTestChecks {
    // The setting for the steps after this, copied into Facet now.
    private static void SmoothSwitch(bool on) {
        PrismBridge.StoreSmooth(on ? "1" : "0");
        PrismBridge.Tick();
    }

    // Past a 200 ms glide begun at most now, and two frames for it to land.
    private static IEnumerator WaitGlide() {
        float until = Time.realtimeSinceStartup + 0.2f;
        while(Time.realtimeSinceStartup < until) yield return null;
        yield return Frames(2);
    }

    // The Animation card holds the Smooth scrolling switch, then the animation duration row. The switch writes scroll.smooth
    // and Facet follows at once; Prism · Settings' own row writes the same setting; search finds the switch on this page. Runs on the new menu's Settings page.
    private static IEnumerator SettingsSmooth(Action<string, bool, string> check) {
        VisualElement card = SettingsCard("HEADING_ANIMATION");
        VisualElement row = card?.Q(name: "SMOOTH_SCROLL"), anim = card?.Q(name: "MENU_ANIM");
        List<VisualElement> all = card?.Query<VisualElement>().ToList() ?? [];
        bool prism = PrismBridge.Available;
        check("the Animation card holds the smooth scrolling switch, then the animation duration row, and shows with PrismLib",
            row != null && anim != null && all.IndexOf(row) < all.IndexOf(anim) && card != null && Displayed(card) == prism,
            $"card={card != null} switch={all.IndexOf(row)} anim={all.IndexOf(anim)} prism={prism}");
        if(!prism) yield break;
        bool before = PrismBridge.SmoothScroll;
        foreach(bool want in new[] { !before, before }) {
            VisualElement track = Body.Q(name: "SMOOTH_SCROLL-input");
            if(track != null) Click(track);
            yield return Frames(2);
            string stored = PrismBridge.SmoothStored;
            bool facet = global::Facet.Anim.SmoothScroll;
            check($"Smooth scrolling {(want ? "on" : "off")}: a click stores it and Facet's windows {(want ? "glide" : "step")}",
                track != null && stored == (want ? "1" : "0") && facet == want, $"stored={stored ?? "missing"} facet={facet}");
        }
        bool now = PrismBridge.SmoothScroll, set = PrismBridge.SetSmoothEntry(!now);
        yield return Frames(2);
        check("Prism · Settings' Smooth scrolling row writes the same setting", set && PrismBridge.SmoothScroll == !now,
            $"set={set} now={PrismBridge.SmoothScroll}");
        PrismBridge.SetSmoothEntry(now);
        yield return Frames(2);
        List<SearchIndex.Hit> hits = Find("smooth");
        check("search finds the smooth scrolling switch on the Settings page", hits.Any(h => h.Key == "SMOOTH_SCROLL" && h.PageRef != null), Hits(hits));
        QuartzShell.Nav.Filter("");
        QuartzShell.Go(CorePages.SettingsPageKey);
        yield return Frames(2);
    }
}
#endif
