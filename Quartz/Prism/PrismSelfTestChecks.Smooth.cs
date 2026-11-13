#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using UnityEngine.UIElements;
namespace Quartz.Prism;
/// <summary>
/// PrismLib's Smooth scrolling setting: Quartz's own tick copies it into Facet, with PrismLib.UI or without it, and
/// Prism · Settings shows its switch in PrismLib's Animation card.
/// </summary>
internal static partial class PrismSelfTestChecks {
    // scroll.smooth "0" then "1", each with Facet's flag at the other value as a sentinel, one Quartz tick, and the flag read
    // in that same frame: every Prism mod copies the setting each frame, so a later read would pass without Quartz. The
    // settings file and the flag are put back.
    private static string SmoothProbe() {
        Action restore = PrismBridge.KeepPrismConfig();
        bool was = FacetSmooth();
        var seen = new List<string>();
        try {
            foreach(bool on in new[] { false, true }) {
                PrismBridge.StoreSmooth(on ? "1" : "0");
                FacetSmoothSet(!on);
                PrismBridge.Tick();
                seen.Add($"{(on ? 1 : 0)}:{FacetSmooth()}");
            }
        } finally {
            restore();
            FacetSmoothSet(was);
        }
        return string.Join(" ", seen);
    }
    private const string SmoothSeen = "0:False 1:True";
    // Quartz's link down while a PrismLib is loaded (Register threw, or another mod's copy is below Quartz's floor): the stored
    // scroll.smooth still decides, both in Facet's flag, which every wired mod copies too, and in the getter the classic menu
    // reads; missing reads on. With no PrismLib at all both are on, whatever is stored, and so they are once the read failed
    // (a PrismLib older than Config, the only one a read fails on, fails every mod's read, so Quartz's write is the only one).
    // Facet's flag starts at the other value as a sentinel each time; the settings file, the link and the flag are put back.
    private static string SmoothLinkDown() {
        Action restore = PrismBridge.KeepPrismConfig();
        bool was = FacetSmooth();
        var seen = new List<string>();
        try {
            foreach((string name, bool loaded, bool foreign, bool failed) in new[] {
                ("threw", true, false, false), ("foreign", false, true, false), ("none", false, false, false), ("failed", false, true, true) }) {
                foreach(string stored in name is "none" or "failed" ? new[] { "0" } : new[] { null, "1", "0" }) {
                    PrismBridge.StoreSmooth(stored);
                    FacetSmoothSet(stored == "0" && (loaded || foreign) && !failed);
                    bool getter;
                    Action back = PrismBridge.LinkDown(loaded, foreign, failed);
                    try {
                        PrismBridge.Tick();
                        getter = PrismBridge.SmoothScroll;
                    } finally { back(); }
                    seen.Add($"{name}/{stored ?? "missing"}:{FacetSmooth()},{getter}");
                }
            }
        } finally {
            restore();
            FacetSmoothSet(was);
        }
        return string.Join(" ", seen);
    }
    private const string SmoothDownSeen =
        "threw/missing:True,True threw/1:True,True threw/0:False,False foreign/missing:True,True foreign/1:True,True foreign/0:False,False none/0:True,True failed/0:True,True";

    internal static IEnumerator Smooth(Action<string, bool, string> check) {
        string seen = SmoothProbe();
        check("prism: scroll.smooth reaches Facet in Quartz's own tick", seen == SmoothSeen, seen);
        string down = SmoothLinkDown();
        check("prism: with a PrismLib loaded but Quartz's link to it down, the stored smooth scrolling reaches Facet in Quartz's tick and the classic menu (missing reads on); with none, or once the read failed, both are on",
            down == SmoothDownSeen, down);
        bool was = PrismWindows.SettingsVisible;
        try {
            PrismBridge.OpenPrismSettings();
            yield return Frames(2);
            bool picked = PickSettingsPage("PrismLib");
            yield return Frames(2);
            VisualElement body = SettingsBody();
            VisualElement anim = body?.Q(name: "Animation duration (ms)"), smooth = body?.Q(name: "Smooth scrolling");
            VisualElement input = smooth?.Q(name: "Smooth scrolling-input");
            // The two rows' nearest shared parent is a card's body when they share a card; its header's caps are the title.
            VisualElement shared = anim;
            while(shared != null && smooth != null && !shared.Contains(smooth)) shared = shared.parent;
            string title = shared == null || shared == body ? null : shared.parent?.Q<Label>()?.text, want = Words.T("Animation");
            List<VisualElement> all = body?.Query<VisualElement>().ToList() ?? [];
            check("prism: Prism · Settings shows Smooth scrolling as a switch in PrismLib's Animation card, after the animation duration",
                picked && anim != null && smooth != null && input?.childCount == 1 && all.IndexOf(anim) < all.IndexOf(smooth)
                && string.Equals(title, want, StringComparison.OrdinalIgnoreCase),
                $"page={picked} anim={all.IndexOf(anim)} smooth={all.IndexOf(smooth)} switch={input?.childCount.ToString() ?? "none"} card={title ?? "none"} want={want}");
        } finally {
            if(PrismWindows.SettingsVisible != was) PrismBridge.TogglePrismSettings();
        }
        yield return Frames(2);
    }

    // With PrismLib.UI off (after UiOff), Quartz's tick still copies the setting: the copy sits outside that half.
    internal static void SmoothUiOff(Action<string, bool, string> check) {
        string seen = SmoothProbe();
        check("prism: ...and with PrismLib.UI off, scroll.smooth still reaches Facet in Quartz's own tick",
            !PrismBridge.UiAvailable && seen == SmoothSeen, $"ui={PrismBridge.UiAvailable} {seen}");
    }

    private static object Panel() => typeof(PrismWindows).GetField("settings", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    private static object PanelField(object panel, string name) =>
        panel?.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool PickSettingsPage(string page) {
        if(PanelField(Panel(), "_tabs") is not Tabs tabs || PanelField(Panel(), "_pages") is not List<string> pages) return false;
        int at = pages.IndexOf(page);
        if(at < 0) return false;
        tabs.Select(at);
        return tabs.SelectedName == page;
    }
    private static VisualElement SettingsBody() => (PanelField(Panel(), "_body") as ScrollView)?.contentContainer;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FacetSmooth() => PrismLib.UI.Toolkit.Anim.SmoothScroll;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FacetSmoothSet(bool on) => PrismLib.UI.Toolkit.Anim.SmoothScroll = on;
}
#endif
