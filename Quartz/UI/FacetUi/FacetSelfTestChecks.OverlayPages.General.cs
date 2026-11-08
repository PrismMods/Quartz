#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.Overlay;
using Quartz.UI.Nav;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ General in the new menu: its rows against the classic page, its edges, the master switch seen by the classic
/// page, the slide-out hotkey, Korean initials, and Overlay Core switched off from its header.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string GeneralPage = "overlay.general";
    private static readonly string[] GeneralRows =
        ["OVERLAY_ENABLED", "SECTION_POP_IN_ANIMATION", "OVERLAY_POP_EDGE_LABEL", "OVERLAY_POP_EASE_LABEL", "OVERLAY_POP_SECONDS", "OVERLAY_POP_EXIT"];

    private static IEnumerator OverlayGeneral(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        OverlaySwitch.EnsureConf();
        OverlaySettings c = OverlaySwitch.Conf;
        bool popWas = c.PopEnabled;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            c.PopEnabled = true;   // the group the animation rows sit in shows
            FacetBridge.OpenNewMenu(GeneralPage);
            yield return Frames(3);
            yield return Parity(check, GeneralPage, GeneralRows);
            string misfits = EdgeMisfits(PageContent());
            check($"{GeneralPage}: every part of the page draws on the cards' left and right edges (±1)", misfits == "", misfits);
            // As the rows write them: the master switch's setter saves.
            Bind<bool> popExit = Bind.To(() => OverlaySwitch.Conf.PopExit, v => {
                OverlaySwitch.Conf.PopExit = v;
                OverlaySwitch.Save();
            });
            yield return ClassicSees(check, GeneralPage, "OVERLAY_ENABLED", Bind.To(() => OverlaySwitch.Enabled, v => OverlaySwitch.Enabled = v));
            yield return Hotkey(check, GeneralPage, "OVERLAY_POP_EXIT", "overlay_pop_exit", "DESC_OVERLAY_POP_EXIT", popExit);
            yield return OptionChord(check, GeneralPage, "OVERLAY_POP_EXIT", "overlay_pop_exit", popExit);
            yield return SearchKorean(check, "OVERLAY_POP_EXIT", "OVERLAY_GENERAL", "Korean initials find Overlay General's slide-out row on its page");
            QuartzShell.Nav.Filter("");
            yield return OverlayCoreOff(check);
        } finally {
            OverlaySwitch.Conf.PopEnabled = popWas;
            OverlaySwitch.Save();
            QuartzShell.Nav?.Filter("");
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(3);
    }

    // Overlay Core switched off from General's own header: its tab turns to the off page, and Combo's tab says Combo needs it.
    private static IEnumerator OverlayCoreOff(Action<string, bool, string> check) {
        // Every feature is read before any is flipped, and put back in reverse: "overlay" is the others' dependency.
        var saved = FeatureService.Features.Select(h => (id: h.Id, was: h.Enabled, had: FeatureService.State.Features.TryGetValue(h.Id, out bool v), value: v)).ToList();
        try {
            QuartzShell.Go(GeneralPage);
            yield return Frames(2);
            VisualElement head = Body.Q(name: "MODULE_OVERLAY_CORE-input");
            if(head != null) Click(head);
            int frames = 0;
            for(; frames < 3 && !FeaturePages.IsStub(NavRegistry.ByKey(QuartzShell.CurrentPage)); frames++) yield return null;
            NavPage off = NavRegistry.ByKey(QuartzShell.CurrentPage);
            bool offPage = !FeatureService.IsEnabled("overlay") && FeaturePages.IsStub(off) && FeaturePages.FeatureOf(off) == "overlay";
            string combo = NavRegistry.KeyFor(FeaturePages.StateFor("combo"));
            QuartzShell.Go(combo);
            yield return Frames(2);
            List<Label> labels = Body.Q(name: "menu-header")?.Query<Label>().ToList() ?? [];
            string line = labels.Count > 1 ? labels[1].text : null, core = QuartzHost.Host.Translate("MODULE_OVERLAY_CORE");
            check("Overlay Core switched off from General's header: within 3 frames General's tab is its off page, and Combo's says it needs Overlay Core",
                head != null && offPage && FeaturePages.IsStub(NavRegistry.ByKey(combo)) && QuartzShell.CurrentPage == combo && line?.Contains(core) == true,
                $"switch={head != null} offPage={offPage} page={off?.Key} frames={frames} combo={combo} line={line} want={core}");
        } finally {
            for(int i = saved.Count - 1; i >= 0; i--) RestoreFeature(saved[i].id, saved[i].was, saved[i].had, saved[i].value);
        }
        yield return Frames(3);
    }
}
#endif
