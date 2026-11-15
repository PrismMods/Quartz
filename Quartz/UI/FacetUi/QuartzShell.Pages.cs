#if !IL2CPP
using Facet;
using Quartz.Addons;
using Quartz.Core;
using Quartz.Features;
using Quartz.Resource;
using Quartz.UI.Nav;
using Quartz.UI.Search;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu's pages, which QuartzSuite hands the suite shell. A page is drawn by its Moved builder, as a "feature is
/// off" page for a stub, or as a card that opens it in the classic menu.
/// </summary>
internal static partial class QuartzShell {
    /// <summary>Pages drawn in Facet, by page key. Every other page is a classic card.</summary>
    internal static readonly Dictionary<string, Action<VisualElement>> Moved = new() {
        [CorePages.HomePageKey] = ShellHome.Build,
        ["visuals.effectremover"] = FacetEffectRemover.Page,
        [CorePages.FeaturesPageKey] = ShellFeatures.Build,
        [CorePages.SettingsPageKey] = ShellSettings.Build,
        ["overlay.general"] = OverlayPages.General.Page,
        ["overlay.keyviewer"] = OverlayPages.KeyViewer.Page,
        ["overlay.progressbar"] = OverlayPages.ProgressBar.Page,
        ["overlay.combo"] = OverlayPages.Combo.Page,
        ["overlay.judgement"] = OverlayPages.Judgement.Page,
        ["overlay.songtitle"] = OverlayPages.SongTitle.Page,
        ["overlay.panels"] = OverlayPages.Panels.Page,
        ["overlay.calibration"] = OverlayPages.Calibration.Page,
        ["visuals.hidejudgements"] = VisualsPages.HideJudgementsPage.Build,
        ["visuals.tilearc"] = VisualsPages.TileArcPage.Build,
        ["visuals.visualtweaks"] = VisualsPages.VisualTweaksPage.Build,
        ["visuals.planetcolors"] = VisualsPages.PlanetColorsPage.Build,
        ["visuals.ottoicon"] = VisualsPages.OttoIconPage.Build,
        ["visuals.uihiding"] = VisualsPages.UiHiderPage.Build,
        ["gameplay.keylimiter"] = GameplayPages.KeyLimiterPage.Build,
        ["gameplay.chatter"] = GameplayPages.ChatterPage.Build,
        ["gameplay.judgement"] = GameplayPages.RestrictionPage.Build,
        ["gameplay.countdown"] = GameplayPages.CountdownPage.Build,
        ["gameplay.death"] = GameplayPages.DeathLimitPage.Build,
        ["gameplay.autodeafen"] = GameplayPages.AutoDeafenPage.Build,
        ["gameplay.practice"] = GameplayPages.PracticePage.Build,
        ["tweaks.general"] = TweaksPages.TweaksPage.Build,
        ["tweaks.specialdays"] = TweaksPages.SpecialDaysPage.Build,
        ["tweaks.optimizer"] = TweaksPages.OptimizerPage.Build,
        ["tweaks.mainmenu"] = TweaksPages.MainMenuPage.Build,
        ["editor.tilereadout"] = EditorPages.EditorPage.TileReadout,
        ["editor.bga"] = EditorPages.EditorPage.Bga,
        ["editor.fliprotate"] = EditorPages.EditorPage.FlipRotate,
        ["editor.decopreview"] = EditorPages.EditorPage.DecoPreview,
        ["tuf.settings"] = TufPages.TufSettingsPage.Build,
        ["developer.main"] = CoreMenuPages.DeveloperPage.Build,
        ["profiles.main"] = CoreMenuPages.ProfilesPage.Build,
        ["import.main"] = CoreMenuPages.ImportPage.Build,
        [CorePages.AddonsPageKey] = CoreMenuPages.AddonsPage.Build,
    };
#if DEBUG
    /// <summary>Builds the key's moved page into the window's content, whether or not the rail shows it (a self-test on a build
    /// whose rail hides the page). Any Go leaves it.</summary>
    internal static void ShowForTest(string key) {
        NavPage page = NavRegistry.ByKey(key);
        if(page != null && Moved.TryGetValue(key, out Action<VisualElement> build)) Nav?.Push(Label(page.LocaleKey, page.Title), build);
    }
#endif
    /// <summary>A Lang key, when English has it; else the title as given (an addon page's, which has no key).</summary>
    internal static string Label(string localeKey, string title) =>
        MainCore.Tr.HasKeyForLanguage(localeKey, "en-US") ? localeKey : title;

    internal static Bind<bool> FeatureSwitch(string featureId) =>
        Bind.To(() => FeatureService.IsEnabled(featureId), v => FeatureService.SetEnabled(featureId, v));

    /// <summary>A category's icon as the classic sidebar picks it: an addon's registered asset wins over the built-in sprite.</summary>
    internal static Sprite CategoryIcon(NavCategory c) => c.IconAsset != null ? SpriteRegistry.Get(c.IconAsset) : MainCore.Spr.Get(c.Icon);

    private static bool IsClassic(NavPage page) =>
        !Moved.ContainsKey(page.Key) && !AddonSettingsTabs.TryGet(page.Key, out _) && !FeaturePages.IsStub(page);

    /// <summary>The page's new-menu builder: its Moved builder, an addon settings tab's form under its header, or null (a stub or
    /// a classic card).</summary>
    internal static Action<VisualElement> BuilderFor(NavPage page) {
        if(page == null) return null;
        if(Moved.TryGetValue(page.Key, out Action<VisualElement> build)) return build;
        if(!AddonSettingsTabs.TryGet(page.Key, out AddonSettingsTabs.Tab tab)) return null;
        return p => {
            Header(p, Label(page.LocaleKey, page.Title), null);
            CoreMenuPages.AddonForm.Build(p, tab);
        };
    }

    // A search result on a page still in the classic menu goes on to that menu; a moved or off page is the destination.
    internal static Action<SearchIndex.Hit> ClassicHit(NavPage page) => IsClassic(page) ? _ => OpenClassic(page.State) : null;

    // Pages moved in part: their editors stay in the classic menu, and so do those rows' hits. A row the moved page has is its own.
    private static readonly HashSet<string> PartlyMoved = ["overlay.panels"];

    // A row its moved page leaves to the classic menu: a partly moved page's rows that the moved page has no row for. Its
    // "Use this feature" is the moved page's header switch. ids holds each partly moved page's row Ids, from a throwaway
    // build of it.
    private static bool LeftClassicOn(NavPage page, string key, Dictionary<string, HashSet<string>> ids) {
        if(!PartlyMoved.Contains(page.Key) || key == "FEATURE_PAGE_ENABLE" || !Moved.TryGetValue(page.Key, out Action<VisualElement> build)) return false;
        if(!ids.TryGetValue(page.Key, out HashSet<string> moved)) {
            var throwaway = new VisualElement();
            build(throwaway);
            Names(throwaway, ids[page.Key] = moved = []);
        }
        return !moved.Contains(key);
    }

    private static void Names(VisualElement e, HashSet<string> into) {
        if(!string.IsNullOrEmpty(e.name)) into.Add(e.name);
        for(int i = 0; i < e.childCount; i++) Names(e[i], into);
    }

    // The classic pages' rows, each jumping to its page in the classic menu. Asked whenever the index builds.
    internal static IEnumerable<SearchIndex.Entry> ClassicRows() {
        // ponytail: a partly moved page is built once more per index build; cache its Ids if that ever shows in [perf]
        var ids = new Dictionary<string, HashSet<string>>();
        foreach(var (key, english, state) in SettingsSearch.Rows()) {
            NavPage page = NavRegistry.ByState(state);
            // A row naming its own page is the page's title, which its header already indexes.
            if(page == null || key == page.LocaleKey) continue;
            if(!IsClassic(page) && !LeftClassicOn(page, key, ids)) continue;
            yield return new SearchIndex.Entry {
                Label = Label(key, english),
                Where = Label(page.LocaleKey, page.Title),
                Tip = english,   // an English query still finds the row in another language
                Open = () => OpenClassic(state),
            };
        }
    }

    internal static void Stub(VisualElement p, NavPage page) {
        string id = FeaturePages.FeatureOf(page);
        FeatureService.Handle handle = FeatureService.Find(id);
        Header(p, Label(page.LocaleKey, page.Title), FeatureSwitch(id), () => OffText(handle));
        if(handle?.Error == null) return;
        string error = handle.Error.Length > 900 ? handle.Error[..900] + "…" : handle.Error;
        Controls.Info(Controls.Card(p, null), "", () => error);
    }

    private static string OffText(FeatureService.Handle handle) {
        FeatureService.Handle blocker = handle == null ? null : FeaturePages.Blocker(handle);
        return blocker == null
            ? QuartzHost.Text("FEATURE_OFF_HINT", "This feature is off. Turn it on to see its settings.")
            : string.Format(QuartzHost.Text("FEATURE_OFF_NEEDS", "This feature needs {0}. Turning it on turns that on too."),
                QuartzHost.Text(blocker.Info.NameKey, blocker.Name));
    }

    // Custom addon pages and every page not moved yet.
    internal static void Classic(VisualElement p, NavPage page) {
        string feature = FeaturePages.FeatureOf(page);
        Header(p, Label(page.LocaleKey, page.Title), feature == null ? null : FeatureSwitch(feature));
        if(p.panel == null) return;   // the search index's throwaway build: the card is the same on every classic page
        // A feature that puts an element on screen gets the Edit layout row: its elements move in the shared layout editor.
        ClassicCard(p, () => OpenClassic(page.State), ShellModel.HasLayout(feature));
    }
}
#endif
