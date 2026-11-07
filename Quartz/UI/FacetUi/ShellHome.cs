#if !IL2CPP
using System.Globalization;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.IO;
using Quartz.Prism;
using Quartz.UI.Home;
using Quartz.UI.Nav;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>The new menu's Home: the version line, quick switches for chosen features, the home cards' status lines, and Edit layout.</summary>
internal static class ShellHome {
    internal const string SwitchesStore = "home.switches";
    // A card whose Lines throws is logged once: its row asks again on every refresh.
    private static readonly HashSet<string> failedCards = [];

    internal static void Build(VisualElement p) {
        Controls.PageHeader(p, "HOME", null, o => {
            o.Id = "menu-header";
            o.DescriptionText = HomeLine;
        });
        VisualElement quick = null;
        quick = Controls.Card(p, "MENU_QUICK_SWITCHES", o => o.Actions = new List<KeyValuePair<string, Action>> { new("MENU_EDIT", () => EditSwitches(quick)) });
        VisualElement grid = Controls.Tiles(quick, o => {
            o.Id = "menu-quick";
            o.Columns = 3;
        });
        foreach(string id in Chosen()) {
            FeatureService.Handle h = FeatureService.Find(id);
            Controls.Tile(grid, QuartzShell.Label(h.Info.NameKey, h.Name), QuartzShell.FeatureSwitch(id), o => {
                o.Id = "menu-switch-" + id;
                o.Open = () => QuartzShell.Go(NavRegistry.KeyFor(FeaturePages.StateFor(id)));
            });
        }
        List<HomeCard> cards = [.. HomeRegistry.Visible().Where(c => c.Lines != null)];
        if(cards.Count > 0) {
            VisualElement status = Controls.Card(p, "MENU_STATUS");
            foreach(HomeCard c in cards)
                Controls.Info(status, QuartzShell.Label(c.LocaleKey, c.Title), () => StatusLine(c), o => o.Id = "menu-status-" + c.Key);
        }
        QuartzShell.LayoutRow(Controls.Card(p, "MENU_LAYOUT", o => o.VisibleWhen = () => PrismBridge.UiAvailable), "menu-edit-layout");
    }

    private static string HomeLine() => string.Format(CultureInfo.InvariantCulture,
        QuartzHost.Text("MENU_HOME_LINE", "{0} · {1} of {2} features on · profile {3}"),
        Info.DisplayName + " " + Info.DisplayVersion, FeatureService.Features.Count(h => h.Enabled), FeatureService.Features.Count, ProfileManager.Active);

    private static string StatusLine(HomeCard c) {
        try {
            return string.Join(" · ", c.Lines());
        } catch(Exception e) {
            if(failedCards.Add(c.Key)) MainCore.Log.Err($"[Home] card '{c.Key}' lines threw: {e}");
            else Diag.Ignore(e);
            return QuartzHost.Text("HOME_CARD_FAILED", "This card failed to build — see the log.");
        }
    }

    private static List<string> Chosen() =>
        ShellModel.ParseSwitches(QuartzHost.Host.Read(SwitchesStore), [.. FeatureService.Features.Select(h => h.Id)]);

    // Every feature, on when chosen. A pick only writes the list: rebuilding Home under the popup would close it, so Home follows on close.
    private static void EditSwitches(VisualElement anchor) => Popup.Open(anchor, content => {
        content.style.width = anchor.layout.width;   // the tiles share out the popup's width, so it needs one
        VisualElement grid = Controls.Tiles(content, o => o.Columns = 3);
        List<string> chosen = Chosen();   // once, here: the tiles' getters run on every refresh
        foreach(FeatureService.Handle h in FeatureService.Features) {
            string id = h.Id;
            Controls.Tile(grid, QuartzShell.Label(h.Info.NameKey, h.Name), Bind.To(() => chosen.Contains(id), on => Choose(chosen, id, on)), o => {
                o.Id = "menu-pick-" + id;
                o.Switch = false;
            });
        }
    }, QuartzShell.Changed);

    private static void Choose(List<string> ids, string id, bool on) {
        ids.Remove(id);
        if(on) ids.Add(id);
        QuartzHost.Host.Write(SwitchesStore, ShellModel.FormatSwitches(ids));
    }
}
#endif
