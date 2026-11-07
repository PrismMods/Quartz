#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.UI.Nav;
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
        ["visuals.effectremover"] = FacetEffectRemover.Page,
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

    /// <summary>A Lang key, when English has it; else the title as given (an addon's, or a feature whose keys unloaded with it).</summary>
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
    }

    private static NavItem CategoryItem(NavCategory c) => new(Label(c.LocaleKey, c.Title), p => BuildCategory(p, c)) {
        VisibleWhen = () => c.IsVisible && NavRegistry.PagesIn(c.Key).Count > 0,
        // Both only grow, so their sum moves whenever either does.
        Revision = static () => NavRegistry.Version + changes,
    };

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
        Tabs row = Tabs.Pages(p, pages.Select(pg => new NavItem(Label(pg.LocaleKey, pg.Title), b => BuildPage(b, pg)) { VisibleWhen = pg.Visible }).ToList(), tab);
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

    // Addon pages, the Key Viewer page and every page not moved yet.
    private static void Classic(VisualElement p, NavPage page) {
        string feature = FeaturePages.FeatureOf(page);
        Controls.PageHeader(p, Label(page.LocaleKey, page.Title), feature == null ? null : FeatureSwitch(feature), o => o.Id = "menu-header");
        Controls.Action(Controls.Card(p, null), "MENU_CLASSIC_PAGE", "MENU_CLASSIC_OPEN", () => OpenClassic(page.State), o => o.Id = "menu-classic");
    }
}
#endif
