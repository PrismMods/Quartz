#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Core;
using Quartz.Features;
using Quartz.UI.Factory;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The experimental switch: the menu the hotkey opens, the Settings row and Effect Remover's button, then the two menus
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
            yield return PingPong(check);
            yield return RebuildBehind(check);
            yield return HeaderSwitch(check);
        } finally {
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
        UICore.Toggle();
        yield return Frames(2);
        Step(check, "switch off: the hotkey again shuts both", !UICore.IsOpen && !FacetBridge.NewMenuVisible());
        Step(check, "switch off: Effect Remover's page has no new-menu button", !FacetSelfTest.PreviewButtonOnPage());
        Step(check, "switch off: the classic Settings page has the new-menu switch", FacetSelfTest.SwitchRowOnSettingsPage());
        yield return Frames(2);
    }

    private static IEnumerator SwitchOn(Action<string, bool, string> check) {
        yield return LivePageButton(check, true);   // leaves the switch on for the steps after it
        UICore.Toggle();
        yield return Frames(2);
        Step(check, "switch on: the hotkey opens the new menu, which counts as a menu showing",
            FacetBridge.NewMenuVisible() && UICore.MenuShowing && !UICore.IsOpen);
        UICore.Toggle();
        yield return Frames(2);
        Step(check, "switch on: the hotkey again hides it, nothing open", !FacetBridge.NewMenuVisible() && !UICore.IsOpen);
        Step(check, "switch on: Effect Remover's page has the new-menu button", FacetSelfTest.PreviewButtonOnPage());
    }

    // Any other opener (the update toast's "open settings") over the new menu, then that classic menu's close.
    private static IEnumerator OpenerOver(Action<string, bool, string> check) {
        UICore.Toggle();
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

    // Review Focus 2: Effect Remover's button, "Open it", then each close back down the chain, which ends with neither menu.
    private static IEnumerator PingPong(Action<string, bool, string> check) {
        UICore.Open(true);
        yield return Frames(2);
        Step(check, "ping-pong 1: the classic menu opens", UICore.IsOpen);
        FacetBridge.OpenNewMenu(FxPage, fromClassic: true);
        yield return Frames(2);
        Step(check, "ping-pong 2: Effect Remover's button swaps it for the new menu on that page",
            FacetBridge.NewMenuVisible() && QuartzShell.CurrentPage == FxPage && HandOff.ReturnTo == HandOff.Menu.Classic);
        QuartzShell.Go("profiles.main");
        yield return Frames(2);
        Button open = Body.Q<Button>("menu-classic-input");
        if(open != null) Submit(open);
        yield return Frames(2);
        int state = NavRegistry.StateFor("profiles.main");
        Step(check, "ping-pong 3: Open it swaps back to the classic menu on that page",
            open != null && UICore.IsOpen && UICore.CurrentMenuState == state && HandOff.ReturnTo == HandOff.Menu.New,
            $"button={open != null} state={UICore.CurrentMenuState}/{state}");
        UICore.Close();
        yield return Frames(2);
        Step(check, "ping-pong 4: closing the classic menu brings the new one back",
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
        UICore.Toggle();
        yield return Frames(2);
        bool up = FacetBridge.NewMenuVisible();
        UICore.Rebuild();
        yield return Frames(2);
        Step(check, "a rebuild behind the new menu keeps it up and the classic menu shut", up && FacetBridge.NewMenuVisible() && !UICore.IsOpen, $"up={up}");
        QuartzShell.Go("profiles.main");
        yield return Frames(2);
        Button open = Body.Q<Button>("menu-classic-input");
        if(open != null) Submit(open);
        yield return Frames(2);
        bool classic = UICore.IsOpen && !FacetBridge.NewMenuVisible();
        UICore.Rebuild();
        yield return Frames(2);
        Step(check, "a rebuild behind a classic menu opened from the new one keeps it open, the new one hidden and the hand-back pending",
            classic && UICore.IsOpen && !FacetBridge.NewMenuVisible() && HandOff.ReturnTo == HandOff.Menu.New, $"classic={classic}");
        UICore.Close();
        yield return Frames(2);
        QuartzShell.Hide();
        yield return Frames(2);
    }

    // Review Focus 3 on a moved page: Effect Remover switched off from its own header, then on from its off page's header.
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
