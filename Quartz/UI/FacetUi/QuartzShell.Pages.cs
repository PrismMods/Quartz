#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Resource;
using Quartz.UI.Nav;
using Quartz.UI.Search;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The shell's rail and pages. A category is a rail item; one with a submenu is a tab row of its pages. A page is
/// drawn by its Moved builder, as a "feature is off" page for a stub, or as a card that opens it in the classic menu.
/// </summary>
internal static partial class QuartzShell {
    private const float RailWidth = 160f;
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
    };
    /// <summary>The tab row of the open category, when it has one.</summary>
    internal static Tabs TabRow { get; private set; }
    // The open page's feature, kept beside it: switching the feature off removes the page, and with it the way to the feature.
    private static string currentFeature;
    // The page Go asked for, while the Nav builds its category.
    private static NavPage going;
    private static int changes;
    private static string builtCategories;
    private static readonly Dictionary<string, NavItem> categoryItems = new();

    /// <summary>Something the rail or pages show changed that NavRegistry.Version does not count: the open page rebuilds.</summary>
    internal static void Changed() => changes++;

    /// <summary>A Lang key, when English has it; else the title as given (an addon page's, which has no key).</summary>
    internal static string Label(string localeKey, string title) =>
        MainCore.Tr.HasKeyForLanguage(localeKey, "en-US") ? localeKey : title;

    internal static Bind<bool> FeatureSwitch(string featureId) =>
        Bind.To(() => FeatureService.IsEnabled(featureId), v => FeatureService.SetEnabled(featureId, v));

    /// <summary>Opens a page: its category in the rail, and its tab there.</summary>
    internal static void Go(string pageKey) {
        NavPage page = NavRegistry.ByKey(pageKey);
        if(Nav == null || page == null || !categoryItems.TryGetValue(page.CategoryKey, out NavItem item)) return;
        going = page;
        try { Nav.Go(item); } finally { going = null; }
    }

    /// <summary>A category's icon as the classic sidebar picks it: an addon's registered asset wins over the built-in sprite.</summary>
    internal static Sprite CategoryIcon(NavCategory c) => c.IconAsset != null ? SpriteRegistry.Get(c.IconAsset) : MainCore.Spr.Get(c.Icon);

    private static string CategoryKeys() => string.Join(",", NavRegistry.AllCategories.Select(c => c.Key));

    // Three ruled groups: Home and Features, the feature categories, then the app's own pages.
    private static void BuildNav() {
        Window.Body.Clear();
        categoryItems.Clear();
        var groups = new List<NavItem>[] { [], [], [] };
        foreach(NavCategory c in NavRegistry.AllCategories) {
            int g = ShellModel.RailGroup(c.Key);
            if(g >= 0) groups[g].Add(categoryItems[c.Key] = CategoryItem(c));
        }
        builtCategories = CategoryKeys();
        Nav = new Facet.Nav(Window.Body, groups.Select(g => NavItem.Heading(null, [.. g])), RailWidth);
        Nav.OnInsetChanged = Window.SetContentInset;
        Window.SetContentInset(RailWidth + 7f);   // the Nav reported its inset before anyone listened
        Nav.AddSearchSource(ClassicRows);
        Nav.OnCompactChanged = c => QuartzHost.Host.Write(CompactStore, c ? "1" : null);
        ApplySidebar();
    }

    internal const string SidebarStore = "menu.sidebar", CompactStore = "menu.compact";

    /// <summary>The Sidebar setting: 0 toggleable (a button folds the rail to its icons, and the choice is kept), 1 always
    /// visible, 2 always hidden (the icon strip, widening over the page while the pointer rests on it). Applies at once.</summary>
    internal static int SidebarMode {
        get => QuartzHost.Host.Read(SidebarStore) switch { "1" => 1, "2" => 2, _ => 0 };
        set {
            QuartzHost.Host.Write(SidebarStore, value is 1 or 2 ? value.ToString() : null);
            ApplySidebar();
        }
    }

    internal static void ApplySidebar() {
        if(Nav == null) return;
        int mode = SidebarMode;
        Nav.CompactButton = mode == 0;
        Nav.Compact = mode == 2 || mode == 0 && QuartzHost.Host.Read(CompactStore) == "1";
    }

    private static NavItem CategoryItem(NavCategory c) => new(Label(c.LocaleKey, c.Title), p => BuildCategory(p, c)) {
        Icon = CategoryIcon(c) is { } icon ? Background.FromSprite(icon) : default,   // the rail draws a fixed square: IconScale has no say
        VisibleWhen = () => c.IsVisible && NavRegistry.PagesIn(c.Key).Count > 0,
        // Both only grow, so their sum moves whenever either does.
        Revision = static () => NavRegistry.Version + changes,
        // A category without tabs is its one page, which can change under it: asked when the hit lands.
        OnHit = h => {
            IReadOnlyList<NavPage> pages = NavRegistry.PagesIn(c.Key);
            if(!NavRegistry.ShowsSubmenu(c) && pages.Count > 0) ClassicHit(pages[0])?.Invoke(h);
        },
    };

    private static bool IsClassic(NavPage page) => !Moved.ContainsKey(page.Key) && !FeaturePages.IsStub(page);

    // A search result on a page still in the classic menu goes on to that menu; a moved or off page is the destination.
    private static Action<SearchIndex.Hit> ClassicHit(NavPage page) => IsClassic(page) ? _ => OpenClassic(page.State) : null;

    // The rows of the classic Settings page's Updates and Font sections, which the moved page leaves there.
    private static readonly HashSet<string> LeftClassic = [
        "UPDATES", "CHECK_FOR_UPDATES", "UPDATE_NOTES", "UPDATE_SKIP", "UPDATE_INSTALL", "UPDATE_UNDO", "UPDATE_RESTART",
        "FONT", "FONT_SCALE", "FONT_LINE_SPACING", "FONT_RENAME", "FONT_DELETE", "SETTINGS_FONT",
    ];

    // Pages moved in part: their editors stay in the classic menu, and so do those rows' hits. A row the moved page has is its own.
    private static readonly HashSet<string> PartlyMoved = ["overlay.keyviewer", "overlay.panels"];

    // A row its moved page leaves to the classic menu: the Settings page's Updates and Font rows, or a partly moved page's
    // rows that the moved page has no row for. Its "Use this feature" is the moved page's header switch. ids holds each partly
    // moved page's row Ids, from a throwaway build of it.
    private static bool LeftClassicOn(NavPage page, string key, Dictionary<string, HashSet<string>> ids) {
        if(page.Key == CorePages.SettingsPageKey) return LeftClassic.Contains(key);
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
    private static IEnumerable<SearchIndex.Entry> ClassicRows() {
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

    private static void BuildCategory(VisualElement p, NavCategory c) {
        IReadOnlyList<NavPage> pages = NavRegistry.PagesIn(c.Key);
        if(!NavRegistry.ShowsSubmenu(c)) {
            if(p.panel != null) TabRow = null;
            if(pages.Count > 0) BuildPage(p, pages[0]);
            return;
        }
        var keyed = pages.Select(pg => new KeyValuePair<string, string>(pg.Key, FeaturePages.FeatureOf(pg))).ToList();
        int tab = going != null
            ? ShellModel.TabIndex(keyed, going.Key, FeaturePages.FeatureOf(going))
            : ShellModel.TabIndex(keyed, CurrentPage, currentFeature);
        Tabs row = Tabs.Pages(p, pages.Select(pg => new NavItem(Label(pg.LocaleKey, pg.Title), b => BuildPage(b, pg)) {
            VisibleWhen = pg.Visible,
            OnHit = ClassicHit(pg),
        }).ToList(), tab);
        if(p.panel != null) TabRow = row;
    }

    private static void BuildPage(VisualElement p, NavPage page) {
        if(p.panel != null) {   // on screen, not the search index's throwaway
            CurrentPage = page.Key;
            currentFeature = FeaturePages.FeatureOf(page);
            QuartzHost.Host.Write(PageStore, page.Key);
        }
        if(Moved.TryGetValue(page.Key, out Action<VisualElement> build)) build(p);
        else if(FeaturePages.IsStub(page)) Stub(p, page);
        else Classic(p, page);
    }

    private static void Stub(VisualElement p, NavPage page) {
        string id = FeaturePages.FeatureOf(page);
        FeatureService.Handle handle = FeatureService.Find(id);
        Controls.PageHeader(p, Label(page.LocaleKey, page.Title), FeatureSwitch(id), o => {
            o.Id = "menu-header";
            o.DescriptionText = () => OffText(handle);
        });
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

    // Addon pages and every page not moved yet.
    private static void Classic(VisualElement p, NavPage page) {
        string feature = FeaturePages.FeatureOf(page);
        Controls.PageHeader(p, Label(page.LocaleKey, page.Title), feature == null ? null : FeatureSwitch(feature), o => o.Id = "menu-header");
        if(p.panel == null) return;   // the search index's throwaway build: the card is the same on every classic page
        VisualElement card = Controls.Card(p, null);
        Controls.Action(card, "MENU_CLASSIC_PAGE", "MENU_CLASSIC_OPEN", () => OpenClassic(page.State), o => o.Id = "menu-classic");
        // The feature puts an element on screen: its elements move in the shared layout editor.
        if(ShellModel.HasLayout(feature)) LayoutRow(card, "menu-classic-layout");
    }
}
#endif
