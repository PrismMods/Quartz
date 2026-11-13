#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Prism;
using Quartz.UI.Factory;
using Quartz.UI.Nav;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// PrismLib's Smooth scrolling setting in Quartz: the classic menu's wheel with it off and on, and its switch on the new
/// menu's Settings page. A check that needs it on or off writes scroll.smooth through PrismLib's Config and ticks Quartz
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

    // A wheel step of Scroll Speed on the classic Settings page, open, from its top: got = the content's position in the
    // step's own frame, the step's target, and the position after the classic tween's 0.2 s and two frames.
    private static IEnumerator ClassicStep(int state, float[] got) {
        UIScrollController sc = UICore.Pages.TryGetValue(state, out RectTransform page) && page != null
            ? page.GetComponentInChildren<UIScrollController>(true) : null;
        got[0] = got[1] = got[2] = float.NaN;
        if(sc == null) yield break;
        sc.JumpTo(0f);
        yield return Frames(1);
        sc.ScrollTo(sc.Offset + MainCore.Conf.ScrollSpeed);   // the wheel's step: a target, then ApplyTween
        got[0] = sc.content.anchoredPosition.y;
        got[1] = sc.Offset;
        yield return WaitGlide();
        got[2] = sc.content.anchoredPosition.y;
        sc.JumpTo(0f);
    }

    // The classic menu follows the switch: off, a wheel step lands in its own frame; on, it still tweens there.
    private static IEnumerator ClassicWheel(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        int state = NavRegistry.StateFor(CorePages.SettingsPageKey);
        Action restore = PrismBridge.KeepPrismConfig();
        HandOff.ReturnTo = HandOff.Menu.None;
        QuartzShell.Hide();
        try {
            UICore.Open(true);
            MenuFactory.SetState(state);
            yield return Frames(3);
            foreach(bool on in new[] { false, true }) {
                SmoothSwitch(on);
                var got = new float[3];
                yield return ClassicStep(state, got);
                string d = $"in its frame={got[0]:0.#} target={got[1]:0.#} after 0.2 s={got[2]:0.#}";
                if(on) check("smooth scrolling on: a wheel step on a classic page still tweens there",
                    got[1] > 0.5f && got[0] < got[1] - 0.5f && Mathf.Abs(got[2] - got[1]) < 0.5f, d);
                else check("smooth scrolling off: a wheel step on a classic page lands in its own frame",
                    got[1] > 0.5f && Mathf.Abs(got[0] - got[1]) < 0.5f, d);
            }
        } finally {
            restore();
            PrismBridge.Tick();
            if(!wasOpen && UICore.IsOpen) UICore.Close(true);
        }
        yield return Frames(2);
    }

    // The Animation card holds the Smooth scrolling switch, then the animation duration row. The switch writes scroll.smooth
    // and both menus follow at once; Prism · Settings' own row writes the same setting, which the classic Settings page
    // opened next shows; search finds the switch on this page. Runs on the new menu's Settings page.
    private static IEnumerator SettingsSmooth(Action<string, bool, string> check) {
        VisualElement card = SettingsCard("HEADING_ANIMATION");
        VisualElement row = card?.Q(name: "SMOOTH_SCROLL"), anim = card?.Q(name: "MENU_ANIM");
        List<VisualElement> all = card?.Query<VisualElement>().ToList() ?? [];
        bool prism = PrismBridge.Available;
        check("the Animation card holds the smooth scrolling switch, then the animation duration row, and shows with PrismLib",
            row != null && anim != null && all.IndexOf(row) < all.IndexOf(anim) && card != null && Displayed(card) == prism,
            $"card={card != null} switch={all.IndexOf(row)} anim={all.IndexOf(anim)} prism={prism}");
        if(!prism) yield break;
        int state = NavRegistry.StateFor(CorePages.SettingsPageKey);
        bool before = PrismBridge.SmoothScroll;
        foreach(bool want in new[] { !before, before }) {
            VisualElement track = Body.Q(name: "SMOOTH_SCROLL-input");
            if(track != null) Click(track);
            yield return Frames(2);
            string stored = PrismBridge.SmoothStored;
            bool facet = global::Facet.Anim.SmoothScroll;
            QuartzShell.OpenClassic(state);
            yield return Frames(2);
            UIToggle live = LiveToggle("smooth_scroll", state);
            var got = new float[3];
            yield return ClassicStep(state, got);
            UICore.Close();
            yield return Frames(2);
            bool lands = Mathf.Abs(got[0] - got[1]) < 0.5f, settles = Mathf.Abs(got[2] - got[1]) < 0.5f;
            check($"Smooth scrolling {(want ? "on" : "off")}: a click stores it, Facet's windows {(want ? "glide" : "step")}, and the classic Settings page shows it and {(want ? "tweens" : "lands at once")}",
                track != null && stored == (want ? "1" : "0") && facet == want && live?.Value == want && lands != want && settles && got[1] > 0.5f,
                $"stored={stored ?? "missing"} facet={facet} classic={live?.Value.ToString() ?? "none"} in its frame={got[0]:0.#} target={got[1]:0.#} after={got[2]:0.#}");
        }
        bool now = PrismBridge.SmoothScroll, set = PrismBridge.SetSmoothEntry(!now);
        yield return Frames(2);   // Quartz's tick sees the change and marks the classic page stale
        QuartzShell.OpenClassic(state);
        yield return Frames(2);
        UIToggle shown = LiveToggle("smooth_scroll", state);
        check("Prism · Settings' Smooth scrolling row writes the same setting, which the classic Settings page opened next shows",
            set && PrismBridge.SmoothScroll == !now && shown?.Value == !now, $"set={set} now={PrismBridge.SmoothScroll} classic={shown?.Value.ToString() ?? "none"}");
        UICore.Close();
        yield return Frames(2);
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
