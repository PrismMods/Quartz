#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Quartz.Core;
using Quartz.Features;
using Quartz.Prism;
using Quartz.UI.Factory;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The experimental switch: the hub the hotkey opens and where its Settings leads, the Settings row and Effect Remover's button, then the two menus
/// opened from each other or rebuilt behind, and a moved page's feature switched from its header. Every step checks that
/// at most one menu shows.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string FxPage = "visuals.effectremover", Fx = "effectremover";

    private static void Step(Action<string, bool, string> check, string name, bool ok, string detail = null) =>
        check(name, ok && !(UICore.IsOpen && FacetBridge.NewMenuVisible()), (detail == null ? "" : detail + " ") + Showing());

    private static IEnumerator SwitchHandOff(Action<string, bool, string> check) {
        bool was = MainCore.Conf.ExperimentalNewMenu, startup = MainCore.Conf.ShowOnStartup, wasOpen = UICore.IsOpen;
        bool fxWas = FeatureService.IsEnabled(Fx), fxHad = FeatureService.State.Features.TryGetValue(Fx, out bool fxStored);
        if(wasOpen) UICore.Close(true);
        QuartzShell.Hide();
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            if(!fxWas) {
                FeatureService.SetEnabled(Fx, true);
                yield return Frames(3);
            }
            yield return SwitchOff(check);
            yield return SwitchOn(check);
            yield return OpenerOver(check);
            yield return FacetSelfTestFixture.Register();   // the classic page both hand to
            yield return PingPong(check);
            yield return RebuildBehind(check);
            yield return FacetSelfTestFixture.Unregister();
            yield return HeaderSwitch(check);
            yield return ProfileLoadSwitchesOff(check);
        } finally {
            FacetSelfTestFixture.Forget();
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            MainCore.Conf.ShowOnStartup = startup;
            if(MainCore.Conf.ExperimentalNewMenu != was) UICore.SetNewMenuSwitch(was);
            RestoreFeature(Fx, fxWas, fxHad, fxStored);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(3);   // the restores' deferred rebuilds
    }

    // SetEnabled records the value; a rig that never chose one keeps its fresh-install default, and Features.json its bytes.
    private static void RestoreFeature(string id, bool was, bool had, bool stored) {
        if(FeatureService.IsEnabled(id) != was) FeatureService.SetEnabled(id, was);
        if(had) FeatureService.State.Features[id] = stored;
        else FeatureService.State.Features.Remove(id);
        FeatureService.State.Save();
    }

    // The switch flipped, as any of its rows does, while the classic menu shows Effect Remover: the live page follows.
    private static IEnumerator LivePageButton(Action<string, bool, string> check, bool on) {
        int state = NavRegistry.StateFor(FxPage);
        UICore.SetNewMenuSwitch(!on);
        yield return Frames(3);
        UICore.Open(true);
        MenuFactory.SetState(state);
        yield return Frames(2);
        bool before = UICore.Pages.TryGetValue(state, out RectTransform page) && page != null && FacetSelfTest.HasPreviewButton(page);
        UICore.SetNewMenuSwitch(on);
        yield return Frames(3);   // the classic pages rebuild a pump later
        page = UICore.Pages.TryGetValue(state, out RectTransform p) ? p : null;
        bool built = page != null && page.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Length > 0;
        bool shows = page != null && FacetSelfTest.HasPreviewButton(page);
        Step(check, $"switching the new menu {(on ? "on adds" : "off removes")} the button on the open classic Effect Remover page",
            UICore.IsOpen && UICore.CurrentMenuState == state && built && before == !on && shows == on, $"before={before} built={built} shows={shows}");
        UICore.Close(true);
        yield return Frames(2);
    }

    private static IEnumerator SwitchOff(Action<string, bool, string> check) {
        yield return LivePageButton(check, false);
        UICore.Toggle();
        yield return Frames(2);
        Step(check, "switch off: the hotkey opens the classic menu in the hub, the new one shut",
            UICore.IsOpen && UICore.IsReorganizing && !FacetBridge.NewMenuVisible());
        bool clicked = ClickHubSettings();
        yield return Frames(2);
        Step(check, "switch off: the hub's Settings opens the classic panel over the hub, the new menu shut",
            clicked && UICore.IsOpen && UICore.IsReorganizing && UICore.PanelInteractive && !FacetBridge.NewMenuVisible(), $"clicked={clicked}");
        UICore.Toggle();
        yield return Frames(2);
        Step(check, "switch off: the hotkey again shuts both", !UICore.IsOpen && !FacetBridge.NewMenuVisible());
        Step(check, "switch off: Effect Remover's page has no new-menu button", !FacetSelfTest.PreviewButtonOnPage());
        Step(check, "switch off: the classic Settings page has the new-menu switch", FacetSelfTest.SwitchRowOnSettingsPage());
        yield return Frames(2);
    }

    private static IEnumerator SwitchOn(Action<string, bool, string> check) {
        yield return LivePageButton(check, true);   // leaves the switch on for the steps after it
        yield return HubFlow(check);
        yield return HubTrips(check);
        yield return HoldOverNewMenu(check);
        Step(check, "switch on: Effect Remover's page has the new-menu button", FacetSelfTest.PreviewButtonOnPage());
    }

    // The hotkey opens the hub, as with the switch off. Its Settings opens the new menu on the page last left, out of
    // reorganize mode, and that menu's close and Escape come back to the hub; the hotkey over it shuts everything.
    private static IEnumerator HubFlow(Action<string, bool, string> check) {
        UICore.Toggle();
        yield return Frames(2);
        Step(check, "switch on: the hotkey opens the hub, not the new menu",
            UICore.IsOpen && UICore.IsReorganizing && !UICore.PanelInteractive && !FacetBridge.NewMenuVisible());
        string last = QuartzShell.CurrentPage;
        foreach(string way in new[] { "close", "escape" }) {
            bool clicked = ClickHubSettings();
            yield return Frames(2);
            Step(check, $"switch on: the hub's Settings opens the new menu on the page last left, out of reorganize mode ({way})",
                clicked && FacetBridge.NewMenuVisible() && UICore.MenuShowing && !UICore.IsOpen && !UICore.IsArranging && QuartzShell.CurrentPage == last,
                $"clicked={clicked} last={last}");
            CloseNewMenu(way);
            yield return Frames(3);
            Step(check, $"switch on: the new menu's {way} goes back to the hub", AtHub());
        }
        bool up = ClickHubSettings();
        yield return Frames(2);
        up &= FacetBridge.NewMenuVisible();
        UICore.Toggle();
        yield return Frames(3);
        Step(check, "switch on: the hotkey over the new menu opened from the hub shuts everything",
            up && !FacetBridge.NewMenuVisible() && !UICore.IsOpen && !UICore.IsReorganizing && HandOff.ReturnTo == HandOff.Menu.None, $"up={up}");
    }

    private static bool AtHub() => !FacetBridge.NewMenuVisible() && UICore.IsOpen && UICore.IsReorganizing && !UICore.PanelInteractive;

    private static bool AllShut() => !FacetBridge.NewMenuVisible() && !UICore.IsOpen && !UICore.IsReorganizing && !UICore.SharedLayoutOpen;

    // The new menu's X, or Escape.
    private static void CloseNewMenu(string way) {
        if(way == "escape") { UICore.EscapeDown(); return; }
        Button close = QuartzShell.Window.TitleBarRight.Query<Button>().ToList().LastOrDefault();
        if(close != null) Submit(close);
    }

    // The hotkey, then the hub's Settings: the new menu, opened from the hub.
    private static IEnumerator FromHub(bool[] up) {
        UICore.Toggle();
        yield return Frames(2);
        up[0] = ClickHubSettings();
        yield return Frames(2);
        up[0] &= FacetBridge.NewMenuVisible();
    }

    // Nothing open, as the hotkey over the hub leaves it.
    private static IEnumerator ShutAll() {
        HandOff.ReturnTo = HandOff.Menu.None;
        QuartzShell.Hide();
        if(UICore.IsOpen) UICore.Close(true);
        yield return Frames(2);
    }

    // Settings' Classic menu (an "Open"), then, unless stay, that classic panel's close: the new menu is back. ok[0] says
    // each step held.
    private static IEnumerator ClassicTrip(bool[] ok, bool stay = false) {
        QuartzShell.Go(CorePages.SettingsPageKey);
        yield return Frames(2);
        Button open = Body.Q<Button>("MENU_CLASSIC_MENU-input");
        if(open != null) Submit(open);
        yield return Frames(2);
        ok[0] &= open != null && UICore.IsOpen && !UICore.IsReorganizing && !FacetBridge.NewMenuVisible();
        if(stay) yield break;
        UICore.Close();
        yield return Frames(2);
        ok[0] &= FacetBridge.NewMenuVisible() && !UICore.IsOpen;
    }

    // Home's Edit layout, then that session closed with Escape: the new menu is back.
    private static IEnumerator LayoutTrip(bool[] ok) {
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
        Button edit = Body.Q<Button>("menu-edit-layout-input");
        if(edit != null) Submit(edit);
        int frames = 0;
        for(; frames < 60 && !UICore.SharedLayoutOpen; frames++) yield return null;
        ok[0] &= edit != null && UICore.SharedLayoutOpen && !FacetBridge.NewMenuVisible();
        yield return Frames(1);
        ok[0] &= FacetBridge.HandleEscape();
        for(int i = 0; i < 10 && !FacetBridge.NewMenuVisible(); i++) yield return null;
        ok[0] &= FacetBridge.NewMenuVisible() && !UICore.SharedLayoutOpen && !UICore.IsOpen;
    }

    // A new menu opened from the hub goes back there after a trip away and back: a classic page's Open and that page's close,
    // or Edit layout's session. The hotkey over any screen of it shuts everything. Edit layout needs PrismLib.UI.
    private static IEnumerator HubTrips(Action<string, bool, string> check) {
        bool layout = PrismBridge.UiAvailable;
        var ok = new bool[1];
        foreach(string way in new[] { "close", "escape" }) {
            yield return FromHub(ok);
            yield return ClassicTrip(ok);
            CloseNewMenu(way);
            yield return Frames(3);
            Step(check, $"switch on: from the hub, after a classic Open and its close, the new menu's {way} goes back to the hub", ok[0] && AtHub());
            if(layout) {
                yield return ShutAll();   // each trip starts afresh, so one that fails leaves the next its own result
                yield return FromHub(ok);
                yield return LayoutTrip(ok);
                CloseNewMenu(way);
                yield return Frames(3);
                Step(check, $"switch on: from the hub, after Edit layout's session, the new menu's {way} goes back to the hub", ok[0] && AtHub());
            }
            yield return ShutAll();
        }
        yield return FromHub(ok);
        yield return ClassicTrip(ok, stay: true);
        UICore.Toggle();
        yield return Frames(3);
        Step(check, "switch on: from the hub, the hotkey over a classic page opened from the new menu shuts everything", ok[0] && AllShut());
        yield return ShutAll();
        yield return FromHub(ok);
        yield return ClassicTrip(ok);
        if(layout) yield return LayoutTrip(ok);
        UICore.Toggle();
        yield return Frames(3);
        Step(check, "switch on: from the hub, the hotkey over the new menu back from its trips shuts everything",
            ok[0] && AllShut() && HandOff.ReturnTo == HandOff.Menu.None);
        yield return ShutAll();
        yield return FxHop(ok);
        UICore.Toggle();
        yield return Frames(3);
        Step(check, "switch on: from the hub, the hotkey over the new menu a classic page's own button opened shuts everything",
            ok[0] && AllShut() && HandOff.ReturnTo == HandOff.Menu.None);
        yield return ShutAll();
        yield return FxHop(ok);
        CloseNewMenu("close");
        yield return Frames(3);
        Step(check, "switch on: from the hub, the close of the new menu a classic page's own button opened goes back to the hub", ok[0] && AtHub());
        yield return ShutAll();
    }

    // From the hub, Settings' Classic menu, then the classic Effect Remover page's "Open in the new menu (preview)": the
    // session stays the hub's.
    private static IEnumerator FxHop(bool[] ok) {
        yield return FromHub(ok);
        yield return ClassicTrip(ok, stay: true);
        int state = NavRegistry.StateFor(FxPage);
        MenuFactory.SetState(state);
        yield return Frames(2);
        ok[0] &= UICore.Pages.TryGetValue(state, out RectTransform page) && page != null
            && ClickLabel(page, MainCore.Tr.Get("FXRM_OPEN_NEW_MENU", "Open in the new menu (preview)"));
        yield return Frames(2);
        ok[0] &= FacetBridge.NewMenuVisible() && !UICore.IsOpen && QuartzShell.CurrentPage == FxPage;
    }

    // Holding the hotkey resets the classic panel's saved size, but not when the press hid the new menu: that panel was not
    // the one toggled. With nothing open the press opens the hub, and the hold still resets. The hotkey is a free key of
    // F13-F15 for the checks; the hotkey, the size and the settings file are the player's and are put back.
    private static IEnumerator HoldOverNewMenu(Action<string, bool, string> check) {
        IO.CoreSettings c = MainCore.Conf;
        string path = MainCore.ConfMgr.Path;
        KeyCode key = new[] { KeyCode.F13, KeyCode.F14, KeyCode.F15 }.FirstOrDefault(k => (int)k != c.ToggleKey && !c.ToggleKeybinds.Any(b => b.Value.Key == (int)k));
        if(key == KeyCode.None) { check("a long hotkey press over the new menu", false, "F13-F15 all bound by the player"); yield break; }
        MainCore.ConfMgr.Save();   // pending saves land now: the bytes compared at the end are these
        byte[] file = File.ReadAllBytes(path);
        var was = (c.ToggleKey, c.ToggleModifier, c.PanelWidth, c.PanelHeight, c.PanelWidthFrac, c.PanelHeightFrac, UICore.LastPanelSize, UICore.LastPanelPosition);
        var known = (1234f, 567f, 0.61f, 0.57f);
        (float, float, float, float) Size() => (c.PanelWidth, c.PanelHeight, c.PanelWidthFrac, c.PanelHeightFrac);
        IEnumerator Hold() {
            HoldKey(key);
            PressNextFrame(key);
            float t0 = Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup - t0 < 0.7f) yield return null;
            HoldKey(KeyCode.None);
            yield return Frames(2);
        }
        try {
            (c.ToggleModifier, c.ToggleKey) = ((int)Keybind.KeyModifier.None, (int)key);
            (c.PanelWidth, c.PanelHeight, c.PanelWidthFrac, c.PanelHeightFrac) = known;
            FacetBridge.OpenNewMenu();
            yield return Frames(2);
            bool up = FacetBridge.NewMenuVisible();
            yield return Hold();
            Step(check, "holding the hotkey over the new menu hides it and leaves the classic panel's saved size alone",
                up && AllShut() && Size() == known, $"up={up} size={Size()}");
            yield return Hold();
            bool hub = AtHub();
            Step(check, "holding it with nothing open opens the hub and still resets the classic panel's saved size",
                hub && Size() == (0f, 0f, 0f, 0f), $"hub={hub} size={Size()}");
        } finally {
            HoldKey(KeyCode.None);
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);   // first: a close stores the panel's size as the last one
            MainCore.ConfMgr.CancelPendingSave();
            (c.ToggleKey, c.ToggleModifier, c.PanelWidth, c.PanelHeight, c.PanelWidthFrac, c.PanelHeightFrac, UICore.LastPanelSize, UICore.LastPanelPosition) = was;
        }
        // A save the reset asked for, already handed to a worker, can land after the finally: settle, then save what was put back.
        float until = Time.realtimeSinceStartup + 1f;
        while(Time.realtimeSinceStartup < until) yield return null;
        MainCore.ConfMgr.Save();
        check("the player's hotkey, panel size and settings file are as before the long presses", SameBytes(path, file),
            $"bytes={File.ReadAllBytes(path).Length}/{file.Length}");
    }

    // The hub's Settings button, released over itself as a click is; false when the hub shows no such button.
    private static bool ClickHubSettings() =>
        ClickLabel(UICore.CanvasObj.transform.Find("ReorganizeHub"), MainCore.Tr.Get("SETTINGS", "Settings"));

    // A classic button under root, found by its label, clicked as the pointer's release over it does.
    private static bool ClickLabel(Transform root, string want) {
        TMPro.TextMeshProUGUI label = root == null ? null : root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).FirstOrDefault(t => t.text == want);
        EventSystem es = EventSystem.current;
        if(root == null || label == null || es == null || !root.gameObject.activeInHierarchy) return false;
        GameObject button = label.transform.parent.gameObject;
        var up = new PointerEventData(es) { button = PointerEventData.InputButton.Left, pointerCurrentRaycast = new RaycastResult { gameObject = button } };
        return ExecuteEvents.Execute(button, up, ExecuteEvents.pointerUpHandler);
    }

    // Any other opener (the update toast's "open settings") over the new menu, then that classic menu's close.
    private static IEnumerator OpenerOver(Action<string, bool, string> check) {
        FacetBridge.OpenNewMenu();
        yield return Frames(2);
        bool up = FacetBridge.NewMenuVisible();
        UICore.Open();
        yield return Frames(2);
        Step(check, "an opener over the new menu hides it and opens the classic menu", up && UICore.IsOpen && !FacetBridge.NewMenuVisible(), $"up={up}");
        UICore.Close();
        yield return Frames(2);
        Step(check, "closing that classic menu brings the new one back", FacetBridge.NewMenuVisible() && !UICore.IsOpen);
        QuartzShell.Hide();
        yield return Frames(2);
    }

    // Effect Remover's button, "Open", then each close back down the chain, which ends with neither menu.
    private static IEnumerator PingPong(Action<string, bool, string> check) {
        UICore.Open(true);
        yield return Frames(2);
        Step(check, "ping-pong 1: the classic menu opens", UICore.IsOpen);
        FacetBridge.OpenNewMenu(FxPage, fromClassic: true);
        yield return Frames(2);
        Step(check, "ping-pong 2: Effect Remover's button swaps it for the new menu on that page",
            FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == FxPage && HandOff.ReturnTo == HandOff.Menu.Classic);
        QuartzShell.Go(FacetSelfTestFixture.PageKey);
        yield return Frames(2);
        Button open = Body.Q<Button>("menu-classic-input");
        if(open != null) Submit(open);
        yield return Frames(2);
        int state = NavRegistry.StateFor(FacetSelfTestFixture.PageKey);
        Step(check, "ping-pong 3: Open swaps back to the classic menu on that page (fixture page)",
            open != null && UICore.IsOpen && UICore.CurrentMenuState == state && HandOff.ReturnTo == HandOff.Menu.New,
            $"button={open != null} state={UICore.CurrentMenuState}/{state}");
        UICore.Close();
        yield return Frames(2);
        Step(check, "ping-pong 4: closing the classic menu brings the new one back (fixture page)",
            FacetBridge.NewMenuVisible() && !UICore.IsOpen && HandOff.ReturnTo == HandOff.Menu.None);
        Button close = QuartzShell.Window.TitleBarRight.Query<Button>().ToList().LastOrDefault();
        if(close != null) Submit(close);
        yield return Frames(2);
        bool shut = !FacetBridge.NewMenuVisible() && !UICore.IsOpen;
        yield return Frames(4);
        Step(check, "ping-pong 5: the new menu's close button reopens nothing, and both are still shut 4 frames later",
            close != null && shut && !FacetBridge.NewMenuVisible() && !UICore.IsOpen, $"close={close != null} shut={shut}");
    }

    // A rebuild behind either menu (show-on-startup re-runs the startup open inside it) leaves the same one showing.
    private static IEnumerator RebuildBehind(Action<string, bool, string> check) {
        MainCore.Conf.ShowOnStartup = true;   // SwitchHandOff puts it back
        FacetBridge.OpenNewMenu();
        yield return Frames(2);
        bool up = FacetBridge.NewMenuVisible();
        UICore.Rebuild();
        yield return Frames(2);
        Step(check, "a rebuild behind the new menu keeps it up and the classic menu shut", up && FacetBridge.NewMenuVisible() && !UICore.IsOpen, $"up={up}");
        QuartzShell.Go(FacetSelfTestFixture.PageKey);
        yield return Frames(2);
        Button open = Body.Q<Button>("menu-classic-input");
        if(open != null) Submit(open);
        yield return Frames(2);
        bool classic = UICore.IsOpen && !FacetBridge.NewMenuVisible();
        UICore.Rebuild();
        yield return Frames(2);
        Step(check, "a rebuild behind a classic menu opened from the new one keeps it open, the new one hidden and the hand-back pending (fixture page)",
            classic && UICore.IsOpen && !FacetBridge.NewMenuVisible() && HandOff.ReturnTo == HandOff.Menu.New, $"classic={classic}");
        UICore.Close();
        yield return Frames(2);
        QuartzShell.Hide();
        yield return Frames(2);
    }

    // A profile load or reset re-reads the switch and runs ApplyRuntimeSettings; if it comes back off, a pending
    // hand-back to the new menu has nothing to go back to.
    private static IEnumerator ProfileLoadSwitchesOff(Action<string, bool, string> check) {
        MethodInfo apply = typeof(Quartz.IO.ProfileManager).GetMethod("ApplyRuntimeSettings", BindingFlags.NonPublic | BindingFlags.Static);
        bool was = MainCore.Conf.ExperimentalNewMenu;
        try {
            MainCore.Conf.ExperimentalNewMenu = true;
            HandOff.ReturnTo = HandOff.Menu.New;
            apply.Invoke(null, null);
            bool keptOn = HandOff.ReturnTo == HandOff.Menu.New;
            MainCore.Conf.ExperimentalNewMenu = false;   // what a loaded profile with the switch off leaves
            apply.Invoke(null, null);
            check("a profile load that leaves the new menu on keeps a pending hand-back, and one that turns it off clears it",
                keptOn && HandOff.ReturnTo == HandOff.Menu.None, $"keptOn={keptOn} returnTo={HandOff.ReturnTo}");
        } finally {
            MainCore.Conf.ExperimentalNewMenu = was;
            HandOff.ReturnTo = HandOff.Menu.None;
        }
        yield break;
    }

    // On a moved page: Effect Remover switched off from its own header, then on from its off page's header.
    private static IEnumerator HeaderSwitch(Action<string, bool, string> check) {
        FacetBridge.OpenNewMenu(FxPage);
        yield return Frames(2);
        string tab = SelectedTab();
        VisualElement track = Body.Q(name: "FXRM_HEADER-input");
        if(track == null || !FeatureService.IsEnabled(Fx)) { Step(check, "Effect Remover's header switch", false, $"track={track != null}"); yield break; }
        Click(track);
        yield return Frames(3);
        NavPage now = NavRegistry.ByKey(QuartzShell.CurrentPage);
        VisualElement off = Body.Q(name: "menu-header-input");
        bool failed = AnyFailed();
        Step(check, "Effect Remover off from its header: within 3 frames the same tab shows its off page, with no failed card",
            !FeatureService.IsEnabled(Fx) && FeaturePages.IsStub(now) && FeaturePages.FeatureOf(now) == Fx && tab != null && SelectedTab() == tab
                && off != null && Shows(off, false) && !failed,
            $"tab={SelectedTab()} want={tab} switch={off != null} failed={failed}");
        if(off != null) Click(off);
        yield return Frames(3);
        Step(check, "back on from the off page's header: within 3 frames the real page is back on the same tab",
            FeatureService.IsEnabled(Fx) && QuartzShell.CurrentPage == FxPage && Body.Q(name: "FXRM_MODE") != null && SelectedTab() == tab && !AnyFailed(),
            $"tab={SelectedTab()} want={tab}");
        QuartzShell.Hide();
        yield return Frames(2);
    }
}
#endif
