#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Core;
using Quartz.Features;
using Quartz.IO;
using Quartz.Prism;
using Quartz.UI.Home;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu's Home: its header line, the quick switches and their Edit… popup, the status rows against the classic
/// Home, and Edit layout there and on a classic page with an on-screen element. Then how pages lay out: a tab row, the
/// search results heading, the overlays under every Prism window, and a close after the switch went off.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string Switches = "home.switches", FailedText = "This card failed to build — see the log.";

    private static IEnumerator HomePage(Action<string, bool, string> check, Action<string> info) {
        bool wasOpen = UICore.IsOpen;
        string stored = QuartzHost.Host.Read(Switches);
        // Key Limiter's card is the status row checked, Combo's page the classic layout row: both on for the duration.
        var flipped = new List<(string id, bool had, bool value)>();
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            foreach(string id in new[] { "keylimiter", "combo" }) {
                if(FeatureService.IsEnabled(id)) continue;
                flipped.Add((id, FeatureService.State.Features.TryGetValue(id, out bool v), v));
                FeatureService.SetEnabled(id, true);
            }
            yield return Frames(3);
            FacetBridge.OpenNewMenu(CorePages.HomePageKey);
            yield return Frames(2);
            HomeHeader(check);
            yield return HomeSwitches(check);
            yield return HomeEditSwitches(check);
            yield return HomeStatus(check);
            yield return HomeLayout(check);
            yield return ClassicLayoutRow(check);
            yield return HomeLayoutNoTargets(check, info);
        } finally {
            HomeRegistry.RemoveOwner("selftest");
            QuartzHost.Host.Write(Switches, stored);
            QuartzShell.Changed();
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            for(int i = flipped.Count - 1; i >= 0; i--) RestoreFeature(flipped[i].id, false, flipped[i].had, flipped[i].value);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(3);
    }

    private static void HomeHeader(Action<string, bool, string> check) {
        List<Label> labels = Body.Q(name: "menu-header")?.Query<Label>().ToList() ?? [];
        string line = labels.Count > 1 ? labels[1].text : null;
        int on = FeatureService.Features.Count(h => h.Enabled), total = FeatureService.Features.Count;
        bool counts = QuartzHost.Host.Language == "en" ? line?.Contains($"{on} of {total}") == true
            : line?.Contains(on.ToString()) == true && line.Contains(total.ToString());
        check("Home's header reads the version, the features on and the profile",
            QuartzShell.CurrentPage == CorePages.HomePageKey && line != null && line.Contains(Info.DisplayVersion) && line.Contains(ProfileManager.Active) && counts,
            $"page={QuartzShell.CurrentPage} line={line} on={on}/{total} profile={ProfileManager.Active}");
    }

    private static List<VisualElement> Tiles() => Body.Q(name: "menu-quick")?.Children().ToList() ?? [];
    private static string TileLabel(string id) {
        FeatureService.Handle h = FeatureService.Find(id);
        return h == null ? null : QuartzHost.Host.Translate(QuartzShell.Label(h.Info.NameKey, h.Name));
    }
    private static string TilesShown() => string.Join("|", Tiles().Select(t => $"{t.name}:{t.Q<Label>()?.text}"));
    private static bool TilesAre(List<string> ids) =>
        Tiles().Select(t => t.name).SequenceEqual(ids.Select(id => "menu-switch-" + id))
        && Tiles().Select(t => t.Q<Label>()?.text).SequenceEqual(ids.Select(TileLabel));

    // The switch list: nothing stored means the defaults, unknown ids drop.
    private static IEnumerator HomeSwitches(Action<string, bool, string> check) {
        List<string> ids = [.. FeatureService.Features.Select(h => h.Id)];
        QuartzHost.Host.Write(Switches, null);
        QuartzShell.Changed();
        yield return Frames(3);
        List<string> defaults = ShellModel.ParseSwitches(null, ids);
        check("with no stored choice, Home's quick switches are the defaults, labelled, in order", defaults.Count > 0 && TilesAre(defaults),
            $"shown={TilesShown()} want={string.Join(",", defaults)}");
        QuartzHost.Host.Write(Switches, "ottoicon,nope");
        QuartzShell.Changed();
        yield return Frames(3);
        check("a stored \"ottoicon,nope\" shows one tile, Otto Icon", TilesAre(["ottoicon"]), $"shown={TilesShown()}");
        const string id = "ottoicon";
        bool was = FeatureService.IsEnabled(id), had = FeatureService.State.Features.TryGetValue(id, out bool value);
        try {
            VisualElement track = Body.Q(name: "menu-switch-" + id + "-input");
            if(track != null) Click(track);
            yield return Frames(3);
            bool flips = FeatureService.IsEnabled(id) != was;
            RestoreFeature(id, was, had, value);
            yield return Frames(3);
            check("its switch flips the feature", track != null && flips, $"track={track != null} was={was}");
            VisualElement tile = Body.Q(name: "menu-switch-" + id);
            if(tile != null) Click(tile);
            yield return Frames(2);
            check("its label opens the feature's page", tile != null && FeaturePages.FeatureOf(NavRegistry.ByKey(QuartzShell.CurrentPage)) == id,
                $"page={QuartzShell.CurrentPage}");
        } finally {
            RestoreFeature(id, was, had, value);
        }
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
    }

    // The popup's picks only write the list, so it stays open; Home follows when it closes.
    private static IEnumerator HomeEditSwitches(Action<string, bool, string> check) {
        string label = QuartzHost.Host.Translate("MENU_EDIT");
        Button edit = Body.Query<Button>().Where(b => b.text == label).First();
        if(edit != null) Submit(edit);
        yield return Frames(2);
        VisualElement pick = Popped("menu-pick-combo");
        int picks = pick?.parent?.childCount ?? 0;
        check("Edit… opens a popup with every feature", pick != null && picks == FeatureService.Features.Count,
            $"edit={edit != null} picks={picks}/{FeatureService.Features.Count}");
        if(pick == null) yield break;
        Click(pick);
        yield return Frames(3);
        string stored = QuartzHost.Host.Read(Switches);
        bool open = pick.panel != null;
        check("picking Combo stores it after Otto Icon and keeps the popup open", stored == "ottoicon,combo" && open, $"stored={stored} open={open}");
        bool took = FacetBridge.HandleEscape();
        float t0 = Time.realtimeSinceStartup;
        while(Body.Q(name: "menu-switch-combo") == null && Time.realtimeSinceStartup - t0 < 0.15f) yield return null;
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        check("closing the popup shows the Combo tile on Home within 150 ms",
            took && pick.panel == null && Body.Q(name: "menu-switch-combo") != null && TilesAre(["ottoicon", "combo"]),
            $"took={took} closed={pick.panel == null} ms={ms:0} shown={TilesShown()}");
    }

    private static TextElement StatusValue(string cardKey) => Body.Q<TextElement>("menu-status-" + cardKey + "-input");

    // The same lines in both menus: one status row here, one line each on the classic Home.
    private static IEnumerator HomeStatus(Action<string, bool, string> check) {
        HomeCard card = HomeRegistry.Visible().FirstOrDefault(c => c.Key == "keylimiter.home");
        string want = card?.Lines == null ? null : string.Join(" · ", card.Lines());
        string shown = StatusValue("keylimiter.home")?.text;
        check("the Key Limiter status row reads its card's lines", want != null && shown == want, $"shown={shown} want={want}");
        bool classic = ClassicHomeShows(want);
        check("the classic Home shows the same line", want != null && classic, $"line={want}");
        HomeRegistry.Add(new HomeCard { Key = "selftest.home", OwnerId = "selftest", Title = "Self-test", Order = 99, Lines = () => throw new InvalidOperationException("self-test") });
        QuartzShell.Changed();
        yield return Frames(3);
        string failed = StatusValue("selftest.home")?.text, text = QuartzHost.Text("HOME_CARD_FAILED", FailedText);
        check("a card whose lines throw shows that it failed", failed == text, $"shown={failed} want={text}");
        HomeRegistry.RemoveOwner("selftest");
        QuartzShell.Changed();
        yield return Frames(3);
    }

    // The classic Home built into a throwaway host: one of its lines reads `line`.
    private static bool ClassicHomeShows(string line) {
        var host = new GameObject("FacetSelfTestHome", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(UICore.Panel, false);
        try {
            Quartz.UI.Factory.Page.PageHome.Create(host);
            return host.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Any(t => t.text == line);
        } finally {
            UnityEngine.Object.Destroy(host.gameObject);
            UICore.Rebuild();   // the build pointed Home's static banner references at this host
        }
    }

    // Hidden by VisibleWhen on the element or on any element around it.
    private static bool Displayed(VisualElement e) {
        for(; e != null; e = e.parent) if(e.style.display.value == DisplayStyle.None) return false;
        return true;
    }

    // An element on Facet's popup surface, a document of its own.
    private static VisualElement Popped(string name) {
        foreach(UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)) {
            VisualElement e = d.rootVisualElement?.Q(name: name);
            if(e?.panel != null) return e;
        }
        return null;
    }

    // Edit layout hands the screen to the shared editor and the new menu comes back after that session, and after no other.
    private static IEnumerator HomeLayout(Action<string, bool, string> check) {
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(1);
        VisualElement row = Body.Q(name: "menu-edit-layout");
        bool ui = PrismBridge.UiAvailable;
        check("Home's Edit layout shows exactly when PrismLib.UI is there", row != null && Displayed(row) == ui, $"row={row != null} ui={ui}");
        if(row == null || !ui) yield break;
        Submit(Body.Q<Button>("menu-edit-layout-input"));
        yield return Frames(2);
        bool hid = !FacetBridge.NewMenuVisible();
        int frames = 0;
        for(; frames < 60 && !UICore.SharedLayoutOpen; frames++) yield return null;
        check("Edit layout hides the new menu and a shared layout session opens within 60 frames", hid && UICore.SharedLayoutOpen,
            $"hid={hid} frames={frames} {Showing()}");
        float editor = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .FirstOrDefault(d => d.name == "PrismLayoutEditor")?.panelSettings?.sortingOrder ?? -1f;
        List<Canvas> overlays = OverlayCanvases();
        check("Quartz's overlays sit under the layout editor, like the other mods'", editor > 0f && overlays.Count > 0 && overlays.All(c => c.sortingOrder < editor),
            $"editor={editor} {OverlayOrders(overlays)}");
        yield return Frames(1);
        bool took = FacetBridge.HandleEscape();
        int back = 0;
        for(; back < 10 && !FacetBridge.NewMenuVisible(); back++) yield return null;
        check("closing the editor brings the new menu back within 10 frames",
            took && FacetBridge.NewMenuVisible() && !UICore.SharedLayoutOpen && !UICore.IsOpen, $"took={took} frames={back} {Showing()}");
        // A session Edit layout did not start (another mod's) shuts the new menu, and its end leaves it shut.
        UICore.SetSharedLayout(true);
        bool shut = !FacetBridge.NewMenuVisible();
        UICore.SetSharedLayout(false);
        yield return Frames(3);
        check("another mod's layout session leaves the new menu shut after it ends", shut && !FacetBridge.NewMenuVisible() && !UICore.IsOpen, Showing());
        FacetBridge.OpenNewMenu();
        yield return Frames(1);
    }

    // The check clears the registry itself, so there are zero targets whatever the timing of the overlays' teardown. That
    // also covers Calibration's handle, which no feature switch removes.
    private static readonly System.Reflection.BindingFlags Static = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
    private static List<ReorganizeHandle> HandleList() =>
        (List<ReorganizeHandle>)typeof(ReorganizeHandle).GetField("layoutHandles", Static).GetValue(null);
    private static void BumpLayoutVersion() {
        System.Reflection.FieldInfo f = typeof(ReorganizeHandle).GetField("layoutVersion", Static);
        f.SetValue(null, (int)f.GetValue(null) + 1);
    }

    // With no Quartz element to move, no session ever starts: the editor alone keeps the new menu shut, until it closes.
    private static IEnumerator HomeLayoutNoTargets(Action<string, bool, string> check, Action<string> info) {
        if(!PrismBridge.UiAvailable) {
            info("PrismLib.UI is not loaded: the no-target Edit layout check is skipped");
            yield break;
        }
        var saved = new List<(string id, bool was, bool had, bool value)>();
        List<ReorganizeHandle> handles = HandleList(), kept = [.. handles];
        try {
            // Read all before switching any: "overlay" takes its dependents down with it. Switching a dependent on stores "overlay" too, so it is put back last, which clears that.
            string[] ids = ["overlay", "keyviewer", "combo", "progressbar", "songtitle", "judgement", "practice", "panels"];
            foreach(string id in ids) saved.Add((id, FeatureService.IsEnabled(id), FeatureService.State.Features.TryGetValue(id, out bool v), v));
            foreach(string id in ids) FeatureService.SetEnabled(id, false);
            handles.Clear();
            BumpLayoutVersion();
            QuartzShell.Go(CorePages.HomePageKey);
            yield return Frames(5);
            Submit(Body.Q<Button>("menu-edit-layout-input"));
            yield return Frames(130);
            bool hidden = !FacetBridge.NewMenuVisible(), shown = LayoutEditorProbe.Shown();
            check("with no Quartz layout target, the new menu stays shut while the editor is open", hidden && shown,
                $"hidden={hidden} editor={shown} {Showing()}");
            bool took = FacetBridge.HandleEscape();
            int back = 0;
            for(; back < 10 && !FacetBridge.NewMenuVisible(); back++) yield return null;
            check("closing that editor brings the new menu back within 10 frames", took && FacetBridge.NewMenuVisible(), $"took={took} frames={back} {Showing()}");
        } finally {
            handles.Clear();
            handles.AddRange(kept);
            BumpLayoutVersion();
            for(int i = saved.Count - 1; i >= 0; i--) RestoreFeature(saved[i].id, saved[i].was, saved[i].had, saved[i].value);
        }
        yield return Frames(3);
    }

    private static IEnumerator ClassicLayoutRow(Action<string, bool, string> check) {
        string combo = NavRegistry.KeyFor(FeaturePages.StateFor("combo"));
        QuartzShell.Go(combo);
        yield return Frames(1);
        VisualElement row = Body.Q(name: "menu-classic-layout");
        bool shown = row != null && Displayed(row) == PrismBridge.UiAvailable;
        QuartzShell.Go("profiles.main");
        yield return Frames(1);
        bool none = Body.Q(name: "menu-classic-layout") == null;
        check("a classic page with an on-screen element links to the layout editor, Profiles does not", shown && none,
            $"page={combo} row={row != null} profiles={!none}");
    }

    private static IEnumerator PageLayout(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            FacetBridge.OpenNewMenu("overlay.general");
            yield return Frames(2);
            TabRowFits(check);
            yield return ResultsHeading(check);
            yield return SwitchedOffClose(check);
            List<Canvas> overlays = OverlayCanvases();
            check("every Quartz overlay canvas sorts under Facet's window bands (below 29000)", overlays.Count > 0 && overlays.All(c => c.sortingOrder < 29000),
                OverlayOrders(overlays));
        } finally {
            QuartzShell.Nav?.Filter("");
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(3);
    }

    // A tab whose button lost its text measure draws its name one letter per line, over the page above it.
    private static void TabRowFits(Action<string, bool, string> check) {
        global::Facet.Tabs row = QuartzShell.TabRow;
        List<Button> tabs = row?.Element.Query<Button>().ToList().Where(Displayed).ToList() ?? [];
        var bad = new List<string>();
        foreach(Button b in tabs) {
            Vector2 m = b.MeasureTextSize(b.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            if(b.contentRect.width + 0.5f < m.x || b.contentRect.height > m.y * 1.5f) bad.Add($"{b.text} {b.contentRect.size} needs {m}");
        }
        bool above = row != null && row.Element.worldBound.yMax <= row.Content.worldBound.yMin + 0.5f;
        check("every tab on Overlay's tab row is as wide as its name and one line tall, and the row sits above its page",
            tabs.Count > 1 && bad.Count == 0 && above, $"page={QuartzShell.CurrentPage} tabs={tabs.Count} above={above} {string.Join(", ", bad)}");
    }

    private static IEnumerator ResultsHeading(Action<string, bool, string> check) {
        QuartzShell.Nav.Filter("key");
        yield return Frames(2);
        string want = global::Facet.Words.OwnF("Results for \u201c{0}\u201d", QuartzHost.Host.Language, "key");
        Label heading = ContentScroll.parent.Query<Label>().Where(l => l.text == want).First();
        VisualElement first = Body.childCount > 0 ? Body[0] : null;
        check("the search results heading sits above the first result", heading != null && first != null && heading.worldBound.yMax <= first.worldBound.yMin + 0.5f,
            $"heading={heading?.worldBound.yMax} first={first?.worldBound.yMin}");
        QuartzShell.Nav.Filter("");
        yield return Frames(1);
    }

    // "Open it", then the new menu switched off in classic Settings: the classic menu's close has nothing to go back to.
    private static IEnumerator SwitchedOffClose(Action<string, bool, string> check) {
        bool was = MainCore.Conf.ExperimentalNewMenu;
        try {
            QuartzShell.Go("profiles.main");
            yield return Frames(2);
            Button open = Body.Q<Button>("menu-classic-input");
            if(open != null) Submit(open);
            yield return Frames(2);
            bool handed = UICore.IsOpen && HandOff.ReturnTo == HandOff.Menu.New;
            UICore.SetNewMenuSwitch(false);
            yield return Frames(3);   // the classic pages rebuild a pump later
            UICore.Close(true);
            yield return Frames(2);
            check("a classic menu opened from the new one closes to nothing once the switch went off", handed && !FacetBridge.NewMenuVisible() && !UICore.IsOpen,
                $"handed={handed} {Showing()}");
        } finally {
            if(MainCore.Conf.ExperimentalNewMenu != was) UICore.SetNewMenuSwitch(was);
        }
        yield return Frames(3);
        FacetBridge.OpenNewMenu();
        yield return Frames(1);
    }

    // Quartz's on-screen overlays and the reorganizer's dim under them; not its menus, toast or dialogs, which stay on top.
    private static readonly string[] Overlays = ["QuartzKeyViewerCanvas", "QuartzPanelsCanvas", "QuartzComboCanvas", "QuartzSongTitleCanvas",
        "QuartzJudgementCanvas", "QuartzProgressBarCanvas", "QuartzPracticeCanvas", "QuartzReorganizeDim"];
    private static List<Canvas> OverlayCanvases() =>
        [.. UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => Overlays.Contains(c.gameObject.name))];
    private static string OverlayOrders(List<Canvas> overlays) => string.Join(", ", overlays.Select(c => $"{c.gameObject.name}={c.sortingOrder}"));
}
#endif
