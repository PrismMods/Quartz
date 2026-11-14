#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Prism;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu: its first open, the window and rail, its pages, the hand-off both ways, a feature switched under an
/// open page, the page it reopens on, a language switch, the wheel and the cost of a closed tick.
/// </summary>
internal static partial class FacetSelfTestChecks {
    // The Nav's rail, read from the Nav: while compact it is drawn in front of the page, last in the tree.
    private static ScrollView RailScroll =>
        (ScrollView)typeof(Facet.Nav).GetField("_railScroll", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(QuartzShell.Nav);
    private static List<Label> RailLabels() =>
        RailScroll.contentContainer.Children().Where(e => e.name is not ("rule" or "rail-button")).Select(e => e.Q<Label>()).ToList();
    private static string SelectedTab() {
        Tabs row = QuartzShell.TabRow;
        if(row == null) return null;
        List<Button> tabs = row.Element.Query<Button>().ToList();
        return row.Selected < 0 || row.Selected >= tabs.Count ? null : tabs[row.Selected].text;
    }
    // Facet's failed-page card, in the host's language, after its "{0}" owner.
    private static bool AnyFailed() {
        string failed = Words.Own("{0}: this page failed to build — see the log.", QuartzHost.Host.Language)[3..];
        return Body.Query<Label>().ToList().Any(l => l.text != null && l.text.Contains(failed));
    }
    private static string Showing() =>
        $"new={FacetBridge.NewMenuVisible()} classic={UICore.IsOpen} hub={UICore.IsReorganizing} page={QuartzShell.CurrentPage} returnTo={HandOff.ReturnTo}";

    private static IEnumerator Shell(Action<string, bool, string> check, Action<string> info) {
        MainCore.Conf.FacetWindows.Remove("window." + QuartzShell.WindowId);   // Run's finally puts the map back
        float until = Time.realtimeSinceStartup + 10f;
        while(!WarmUp.Done && Time.realtimeSinceStartup < until) yield return null;
        check("facet warm-up done before the new menu's first open", WarmUp.Done, null);
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            yield return ShellFirstOpen(check, info);
            if(QuartzShell.Window == null) yield break;
            yield return ShellWindow(check, info);
            yield return ShellPages(check, info);
            yield return ShellHandOff(check);
            yield return ShellFeatureFlip(check);
            yield return ShellLastPage(check);
            yield return ShellLanguage(check);
            yield return ShellWheel(check);
            yield return ShellWheelSpeed(check);
            yield return ShellClosedCost(check);
        } finally {
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(wasOpen && !UICore.IsOpen) UICore.Open(true);
            else if(!wasOpen && UICore.IsOpen) UICore.Close(true);
        }
    }

    // Prebuilt, then shown: its first mesh generation after the show is "open".
    private static IEnumerator ShellFirstOpen(Action<string, bool, string> check, Action<string> info) {
        bool first = QuartzShell.Surface == null;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        FacetBridge.PrebuildNewMenu();
        info($"new menu cold build {clock.Elapsed.TotalMilliseconds:0.0} ms (first build: {first})");
        check("the new menu builds", QuartzShell.Window != null, null);
        if(QuartzShell.Window == null) yield break;
        // Measured on Home whatever page the player left open (a moved page's build is logged with its own checks); Run's
        // finally puts back the stored page.
        if(QuartzShell.CurrentPage != CorePages.HomePageKey) QuartzShell.Go(CorePages.HomePageKey);
        string stored = QuartzHost.Host.Read("window." + QuartzShell.WindowId);
        check("a new menu built ahead and not shown stores no window rect", !first || stored == null, $"first={first} stored={stored}");
        yield return Frames(3);
        ScrollView scroll = ContentScroll;
        var was = scroll.generateVisualContent;
        double ms = -1;
        scroll.generateVisualContent = _ => {
            if(ms >= 0) return;
            ms = clock.Elapsed.TotalMilliseconds;
            MainCore.Log.Msg($"[perf] new menu first open {ms:0.0} ms (warm-up done: {WarmUp.Done})");
        };
        clock.Restart();
        FacetBridge.OpenNewMenu();
        yield return Frames(2);
        scroll.generateVisualContent = was;
        check("[perf] new menu first open is at most 50 ms", ms >= 0 && ms <= 50, $"ms={ms:0.0}");
        check("the new menu shows as a menu, with the classic one shut",
            FacetBridge.NewMenuVisible() && UICore.MenuShowing && !UICore.IsOpen, Showing());
    }

    private static IEnumerator ShellWindow(Action<string, bool, string> check, Action<string> info) {
        (float X, float Y, float W, float H) want = default;
        bool At() {
            Rect panel = QuartzShell.Surface.Root.layout, r = QuartzShell.Window.Rect;
            want = ShellModel.InitialRect(panel.width, panel.height);
            return Mathf.Abs(r.x - want.X) <= 2f && Mathf.Abs(r.y - want.Y) <= 2f && Mathf.Abs(r.width - want.W) <= 2f && Mathf.Abs(r.height - want.H) <= 2f;
        }
        // The rect lands on the first tick that knows the panel's size.
        for(int i = 0; i < 10 && QuartzShell.FreshGeometry && !At(); i++) yield return null;
        bool at = At();
        string detail = $"rect={QuartzShell.Window.Rect} want={want} panel={QuartzShell.Surface.Root.layout.size}";
        if(QuartzShell.FreshGeometry) check("the new menu opens at 80% of the panel, centred", at, detail);
        else info("new menu had saved geometry when built; initial rect not asserted: " + detail);
        VisualElement left = QuartzShell.Window.TitleBarLeft, logo = left.childCount > 0 ? left[0] : null;
        check("the title bar leads with the Quartz logo", logo?.name == "menu-logo" && logo.style.backgroundImage.value.sprite != null,
            $"first={logo?.name}");
        var expected = new List<string>();
        for(int g = 0; g < 3; g++)
            foreach(NavCategory c in NavRegistry.AllCategories)
                if(ShellModel.RailGroup(c.Key) == g && c.IsVisible && NavRegistry.PagesIn(c.Key).Count > 0)
                    expected.Add(QuartzHost.Host.Translate(QuartzShell.Label(c.LocaleKey, c.Title)));
        string[] shown = RailLabels().Select(l => l?.text).ToArray();
        int rules = RailScroll.contentContainer.Children().Count(e => e.name == "rule");
        check("the rail lists the visible categories in their three groups", shown.SequenceEqual(expected),
            $"shown={string.Join("|", shown)} want={string.Join("|", expected)}");
        check("the rail has two rules and no Credits", rules == 2 && !shown.Contains(QuartzHost.Host.Translate("CREDITS")), $"rules={rules}");
    }

    private static IEnumerator ShellPages(Action<string, bool, string> check, Action<string> info) {
        QuartzShell.Go("visuals.effectremover");
        yield return Frames(1);
        string tab = SelectedTab(), fx = MainCore.Tr.Get("SECTION_EFFECT_REMOVER", "Effect Remover");
        check("Go opens Effect Remover, drawn in Facet, on its tab",
            QuartzShell.CurrentPage == "visuals.effectremover" && tab == fx && Body.Q(name: "FXRM_MODE") != null,
            $"page={QuartzShell.CurrentPage} tab={tab} want={fx} mode={Body.Q(name: "FXRM_MODE") != null}");
        int moved = 0, classic = 0, stub = 0;
        var failed = new List<string>();
        foreach(NavPage page in NavRegistry.AllVisible()) {
            if(ShellModel.RailGroup(page.CategoryKey) < 0) continue;   // Credits stays off the rail
            QuartzShell.Go(page.Key);
            if(QuartzShell.CurrentPage != page.Key || AnyFailed()) failed.Add($"{page.Key}->{QuartzShell.CurrentPage}");
            if(QuartzShell.Moved.ContainsKey(page.Key)) moved++;
            else if(FeaturePages.IsStub(page)) stub++;
            else classic++;
        }
        info($"new menu pages: moved={moved} classic={classic} stub={stub}");
        check("every page on the rail opens in the new menu without a failed card", failed.Count == 0, string.Join(", ", failed));
        QuartzShell.Go("profiles.main");
        yield return Frames(1);
        string title = Body.Q(name: "menu-header")?.Q<Label>()?.text, profiles = MainCore.Tr.Get("PROFILES", "Profiles");
        check("a classic page shows its title and the card that opens it",
            Body.Q(name: "menu-classic") != null && title == profiles, $"title={title} want={profiles}");
        yield return ShellHiddenCategory(check);
    }

    // Two run-scoped categories, one with a shown page and one whose only page is hidden: the second stays off the rail
    // and its page is no Go target. Both are removed again, and the rail rebuilt without them.
    private static IEnumerator ShellHiddenCategory(Action<string, bool, string> check) {
        const string owner = "selftest.rail";
        bool OnRail(string title) => RailLabels().Any(l => l?.text == QuartzHost.Host.Translate(QuartzShell.Label(null, title)));
        void Add(string key, string title, bool shown) {
            NavRegistry.AddCategory(new NavCategory { Key = key, Title = title, OwnerId = owner });
            NavRegistry.AddPage(new NavPage { Key = key + ".page", CategoryKey = key, Title = title, OwnerId = owner, Visible = () => shown, Build = _ => { } });
        }
        bool shownOn = false, hiddenOff = false, noGo = false, gone = false;
        string page = null;
        try {
            Add("selftest.shown", "Self-test shown", true);
            Add("selftest.hidden", "Self-test hidden", false);
            FacetBridge.OpenNewMenu();   // a category added since the last open joins the rail
            yield return Frames(1);
            shownOn = OnRail("Self-test shown");
            hiddenOff = !OnRail("Self-test hidden");
            string before = QuartzShell.CurrentPage;
            QuartzShell.Go("selftest.hidden.page");
            page = QuartzShell.CurrentPage;
            noGo = page == before;
        } finally {
            NavRegistry.RemoveOwner(owner);
            FacetBridge.OpenNewMenu();
        }
        yield return Frames(1);
        gone = !OnRail("Self-test shown");
        check("a category whose pages are all hidden stays off the rail, and its page is no Go target",
            shownOn && hiddenOff && noGo && gone, $"shown={shownOn} hiddenOff={hiddenOff} page={page} removed={gone}");
    }

    // "Open" on a classic card, then each way a user closes the classic menu, which brings the new one back.
    private static IEnumerator ShellHandOff(Action<string, bool, string> check) {
        int state = NavRegistry.StateFor("profiles.main");
        foreach(string way in new[] { "close", "escape", "hotkey" }) {
            QuartzShell.Go("profiles.main");
            Button open = Body.Q<Button>("menu-classic-input");
            if(open == null) { check($"Open ({way})", false, "no menu-classic-input"); yield break; }
            Submit(open);
            yield return Frames(2);
            check($"Open hands the page to the classic menu ({way})",
                !FacetBridge.NewMenuVisible() && UICore.IsOpen && !UICore.IsReorganizing && UICore.CurrentMenuState == state,
                $"{Showing()} state={UICore.CurrentMenuState}/{state}");
            if(way == "close") UICore.Close();
            else if(way == "escape") UICore.EscapeDown();
            else UICore.Toggle();
            yield return Frames(way == "escape" ? 3 : 2);   // the classic Escape runs a pump later
            check($"closing the classic menu ({way}) brings the new one back on its page",
                FacetBridge.NewMenuVisible() && !UICore.IsOpen && QuartzShell.CurrentPage == "profiles.main" && HandOff.ReturnTo == HandOff.Menu.None,
                Showing());
        }
        // Another mod's shared layout session over a classic menu opened from here: both shut, and its end reopens neither.
        Submit(Body.Q<Button>("menu-classic-input"));
        yield return Frames(2);
        bool fromShell = UICore.IsOpen && !FacetBridge.NewMenuVisible();
        UICore.SetSharedLayout(true);
        bool shut = !UICore.IsOpen && !FacetBridge.NewMenuVisible();
        UICore.SetSharedLayout(false);
        yield return Frames(2);
        check("a shared layout session shuts both menus, and its end reopens neither",
            fromShell && shut && !UICore.IsOpen && !FacetBridge.NewMenuVisible(), $"from={fromShell} shut={shut} {Showing()}");
        FacetBridge.OpenNewMenu();
        yield return Frames(1);
        // A feature that closes the classic menu to get it out of the way (a level launch): the new menu stays shut, now and after a later toggle.
        Submit(Body.Q<Button>("menu-classic-input"));
        yield return Frames(2);
        bool again = UICore.IsOpen && !FacetBridge.NewMenuVisible();
        UICore.Dismiss();
        yield return Frames(3);
        bool dismissed = !UICore.IsOpen && !FacetBridge.NewMenuVisible();
        UICore.Toggle(true);
        yield return Frames(2);
        UICore.Toggle(true);
        yield return Frames(3);
        check("dismissing the classic menu opened from the new one leaves both shut, even after a toggle",
            again && dismissed && !UICore.IsOpen && !FacetBridge.NewMenuVisible(), $"from={again} dismissed={dismissed} {Showing()}");
        FacetBridge.OpenNewMenu();
        yield return Frames(1);
    }

    // A feature switched off and on under its open page keeps its tab.
    private static IEnumerator ShellFeatureFlip(Action<string, bool, string> check) {
        const string id = "ottoicon";
        bool was = FeatureService.IsEnabled(id), had = FeatureService.State.Features.TryGetValue(id, out bool stored);
        try {
            if(!was) {
                FeatureService.SetEnabled(id, true);
                yield return Frames(3);
            }
            string key = NavRegistry.KeyFor(FeaturePages.StateFor(id));
            QuartzShell.Go(key);
            yield return Frames(1);
            FeatureService.SetEnabled(id, false);
            yield return Frames(3);
            NavPage now = NavRegistry.ByKey(QuartzShell.CurrentPage);
            VisualElement track = Body.Q(name: "menu-header-input");
            string tab = SelectedTab(), label = now == null ? null : QuartzHost.Host.Translate(QuartzShell.Label(now.LocaleKey, now.Title));
            bool off = track != null && Shows(track, false), failed = AnyFailed();
            check("switching a feature off keeps its tab, now its off page with the switch off",
                FeaturePages.IsStub(now) && FeaturePages.FeatureOf(now) == id && tab == label && off && !failed,
                $"from={key} page={QuartzShell.CurrentPage} tab={tab} want={label} off={off} failed={failed}");
            FeatureService.SetEnabled(id, true);
            yield return Frames(3);
            check("switching it back on brings its moved page back on the same tab",
                QuartzShell.CurrentPage == key && Body.Q(name: "SECTION_OTTO_ICON") != null && Body.Q(name: "menu-classic") == null && !AnyFailed(),
                $"page={QuartzShell.CurrentPage} want={key}");
        } finally {
            RestoreFeature(id, was, had, stored);
        }
        yield return Frames(3);
    }

    // The page it showed, and Home when the stored page is gone.
    private static IEnumerator ShellLastPage(Action<string, bool, string> check) {
        QuartzShell.Go("visuals.effectremover");
        string page = QuartzShell.CurrentPage;
        QuartzShell.Hide();
        yield return Frames(1);
        FacetBridge.OpenNewMenu();
        yield return Frames(1);
        string stored = QuartzHost.Host.Read(QuartzShell.PageStore);
        check("the new menu reopens on the page it showed, and stores it",
            page == "visuals.effectremover" && QuartzShell.CurrentPage == page && stored == page, $"page={QuartzShell.CurrentPage} stored={stored}");
        QuartzShell.Hide();
        QuartzHost.Host.Write(QuartzShell.PageStore, "nope.page");
        QuartzHost.Shutdown();   // QuartzShell.Shutdown alone would leave its surface undisposed
        FacetBridge.OpenNewMenu();
        yield return Frames(2);
        check("a stored page that is gone opens Home", FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == CorePages.HomePageKey, Showing());
    }

    // The rail and the open page relabel in the same frame; Chinese draws.
    private static IEnumerator ShellLanguage(Action<string, bool, string> check) {
        QuartzShell.Go("profiles.main");
        NavCategory overlay = NavRegistry.Category("overlay");
        string before = QuartzHost.Host.Translate(QuartzShell.Label(overlay.LocaleKey, overlay.Title));
        Label rail = RailLabels().FirstOrDefault(l => l?.text == before), head = Body.Q(name: "menu-header")?.Q<Label>();
        if(rail == null || head == null) { check("language: the Overlay rail entry and a page title", false, $"rail={rail != null} head={head != null}"); yield break; }
        string was = MainCore.Tr.Language;
        var results = new List<(string name, bool ok, string detail)>();
        try {
            foreach(string lang in new[] { "ko-KR", "zh-CN" }) {
                MainCore.Tr.Language = lang;
                string want = MainCore.Tr.Get("OVERLAY", "Overlay"), title = MainCore.Tr.Get("PROFILES", "Profiles");
                int frames = 0;
                for(; frames < 30 && rail.text != want; frames++) yield return null;
                bool same = head.text == title;   // read in the frame the rail relabelled
                bool draws = lang != "zh-CN" || Fonts.Has(Fonts.Regular(QuartzHost.Host), rail.text);
                results.Add(($"language {lang}: the rail and the page relabel in one frame{(lang == "zh-CN" ? ", and draw" : "")}",
                    rail.text == want && same && draws, $"rail={rail.text} want={want} head={head.text} title={title} frames={frames} draws={draws}"));
            }
        } finally {
            MainCore.Tr.Language = was;
        }
        for(int i = 0; i < 30 && rail.text != before; i++) yield return null;
        foreach(var r in results) check(r.name, r.ok, r.detail);
    }

    // Two mods ticking the wheel in one frame scroll one notch: at once with smooth scrolling off, and with it on once the
    // glide has settled (nothing moves in the step's own frame). The glide is 200 ms whatever the player's animation duration.
    // The page must overflow by a notch, whichever features the player has on: Settings, in a window at its minimum height.
    private static IEnumerator ShellWheel(Action<string, bool, string> check) {
        Rect rect = QuartzShell.Window.Rect;
        Action restore = PrismBridge.KeepPrismConfig();
        QuartzShell.Window.Rect = new Rect(rect.x, rect.y, rect.width, global::Facet.Window.MinHeight);
        try {
            QuartzShell.Go(CorePages.SettingsPageKey);
            yield return Frames(3);
            ScrollView sv = ContentScroll;
            foreach(bool on in new[] { false, true }) {
                SmoothSwitch(on);
                sv.scrollOffset = Vector2.zero;
                yield return Frames(1);
                float before = sv.scrollOffset.y, speed = global::Facet.Anim.Speed;
                global::Facet.Anim.Speed = 1f;
                FacetBridge.TickWheel(ScreenCentre(sv), -1f);
                FacetBridge.TickWheel(ScreenCentre(sv), -1f);
                global::Facet.Anim.Speed = speed;
                float now = sv.scrollOffset.y - before;
                if(on) yield return WaitGlide();
                float moved = sv.scrollOffset.y - before;
                // One notch in the page's own px: Scroll Speed in classic canvas px, brought to this window's scale.
                float notch = MainCore.Conf.ScrollSpeed * ClassicPx / (PanelPx(sv) * QuartzShell.Window.Scale);
                if(on) check("smooth scrolling on: two wheel ticks in one frame glide the new menu one notch, nothing in that frame",
                    Mathf.Abs(now) < 0.5f && Mathf.Abs(moved - notch) < 0.5f, $"in its frame={now:0.#} settled={moved:0.#} notch={notch:0.#}");
                else check("two wheel ticks in one frame scroll the new menu one notch", Mathf.Abs(moved - notch) < 0.5f, $"moved={moved:0.#} notch={notch:0.#}");
            }
        } finally {
            QuartzShell.Window.Rect = rect;
            restore();
            PrismBridge.Tick();
        }
    }

    // Screen px per panel px, and per classic canvas px; both canvases scale with the screen from 1920x1080.
    private static float PanelPx(VisualElement e) => Screen.width / e.panel.visualTree.layout.width;
    private static float ClassicPx => Screen.width / UICore.CanvasSize.x;
    private static Vector2 ScreenCentre(VisualElement e) {
        Vector2 centre = e.worldBound.center;
        float k = PanelPx(e);
        return new Vector2(centre.x * k, Screen.height - (centre.y * k));   // panel y runs down
    }

    // A wheel unit moves the page as far on screen as the classic menu at the same Scroll Speed, a trackpad's tenth a
    // tenth of that, and a changed Scroll Speed shows on the next scroll. Smooth scrolling off, the step lands in its own
    // frame; on, it glides there: a quarter into the 200 ms glide the page is strictly between start and target (a frame
    // that ran past the glide's end re-runs the step once), and once settled it has moved the same distance.
    private static IEnumerator ShellWheelSpeed(Action<string, bool, string> check) {
        float speed = MainCore.Conf.ScrollSpeed;
        Rect rect = QuartzShell.Window.Rect;
        Action restore = PrismBridge.KeepPrismConfig();
        QuartzShell.Window.Rect = new Rect(rect.x, rect.y, rect.width, global::Facet.Window.MinHeight);
        try {
            QuartzShell.Go(CorePages.SettingsPageKey);
            yield return Frames(3);
            ScrollView sv = ContentScroll;
            (string name, float s, float delta)[] steps = [
                ("a wheel notch moves the new menu as far on screen as the classic menu (Scroll Speed 80)", 80f, 1f),
                ("...a trackpad's 0.1 a tenth of that", 80f, 0.1f),
                ("...and Scroll Speed 160 twice as far, on the next scroll", 160f, 1f),
            ];
            foreach(bool on in new[] { false, true }) {
                SmoothSwitch(on);
                foreach(var (name, s, delta) in steps) {
                    MainCore.Conf.ScrollSpeed = s;
                    float now = 0f, mid = float.NaN, moved = 0f;
                    for(int run = 0; run < 2 && float.IsNaN(mid); run++) {
                        sv.scrollOffset = Vector2.zero;
                        yield return Frames(1);
                        float top = sv.contentContainer.worldBound.y, start = Time.realtimeSinceStartup, anim = global::Facet.Anim.Speed;
                        global::Facet.Anim.Speed = 1f;
                        FacetBridge.TickWheel(ScreenCentre(sv), -delta);
                        global::Facet.Anim.Speed = anim;
                        now = sv.scrollOffset.y;
                        if(on) {
                            while(Time.realtimeSinceStartup - start < 0.05f) yield return null;
                            if(Time.realtimeSinceStartup - start < 0.2f) mid = sv.scrollOffset.y;
                            yield return WaitGlide();
                        } else {
                            mid = now;
                            yield return Frames(1);
                        }
                        moved = (top - sv.contentContainer.worldBound.y) * PanelPx(sv);
                    }
                    float want = s * delta * ClassicPx, end = sv.scrollOffset.y, step = want / (PanelPx(sv) * QuartzShell.Window.Scale);
                    string d = $"moved={moved:0.0} want={want:0.0} screen px; page px: in its frame={now:0.##} a quarter in={mid:0.##} settled={end:0.##}";
                    if(on) check("smooth scrolling on: " + name + ", gliding there",
                        Mathf.Abs(now) < 0.01f && mid > 0.01f && mid < end - 0.01f && Mathf.Abs(moved - want) <= 1f, d);
                    else check(name + ", in its own frame (smooth scrolling off)", Mathf.Abs(now - step) < 0.5f && Mathf.Abs(moved - want) <= 1f, d);
                }
            }
        } finally {
            MainCore.Conf.ScrollSpeed = speed;
            QuartzShell.Window.Rect = rect;
            restore();
            PrismBridge.Tick();
        }
    }

    private static IEnumerator ShellClosedCost(Action<string, bool, string> check) {
        QuartzShell.Hide();
        yield return Frames(1);
        const int n = 600;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for(int i = 0; i < n; i++) FacetBridge.TickNewMenu();
        double us = clock.Elapsed.TotalMilliseconds * 1000.0 / n;
        MainCore.Log.Msg($"[perf] closed tick mean {us:0.000} µs");
        check("[perf] closed tick mean is at most 2 µs", us <= 2.0, $"mean={us:0.000} µs");
    }
}
#endif
