using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Factory.Page;
namespace Quartz.UI.Nav;
internal static class CorePages {
    internal const string HomeCategoryKey = "home";
    internal const string HomePageKey = "home.overview";
    internal const string SearchPageKey = "search.main";
    internal const string FeaturesCategoryKey = "features";
    internal const string AddonsCategoryKey = FeaturesCategoryKey;
    internal const string AddonsPageKey = "addons.main";
    internal const string FeaturesPageKey = "features.main";
    internal const string SettingsPageKey = "settings.main";
    private static bool registered;
    internal static void EnsureRegistered() {
        if(registered) return;
        registered = true;
        Categories();
        Overlay();
        Gameplay();
        Visuals();
        Standalone();
    }
    // Hidden in the KeyViewer flavour. A predicate, not #if, so both builds keep the same registry shape.
    private static bool CoreNav => !Info.KeyViewerOnly;
    private static void Categories() {
        Category(HomeCategoryKey, "Home", "HOME", UISprite.House128, 0,
            visible: static () => CoreNav);
        // The KeyViewer flavour runs a fixed feature set, so feature switches and addons are hidden.
        Category(FeaturesCategoryKey, "Features", "FEATURES", UISprite.Sparkles128, 5, alwaysSubmenu: true,
            visible: static () => CoreNav);
        Category("overlay", "Overlay", "OVERLAY", UISprite.Monitor128, 10, togglable: true);
        Category("gameplay", "Gameplay", "GAMEPLAY", UISprite.Gamepad128, 20, togglable: true);
        Category("visuals", "Visuals", "VISUALS", UISprite.Image128, 30, togglable: true);
        Category("tweaks", "Tweaks", "TWEAKS", UISprite.AdjustmentsHorizontal128, 40, togglable: true);
        // Pages come from the editor feature; the category hides itself while it is off.
        Category("editor", "Editor", "EDITOR", UISprite.Pencil128, 50, togglable: true);
        Category("others", "Others", "OTHERS", UISprite.Grid128, 75, togglable: true);
        Category("tuf", "TUF", "TUF", UISprite.Planet128, 76,
            visible: static () => CoreNav && Quartz.Features.FeatureCategories.IsEnabled("others"));
        Category("search", "Search", "SEARCH", UISprite.MagnifyingGlass128, 80, visible: static () => CoreNav);
        Category("profiles", "Profiles", "PROFILES", UISprite.Users128, 90, visible: static () => CoreNav);
        Category("import", "Import", "IMPORT", UISprite.Book128, 100, visible: static () => CoreNav);
        Category("settings", "Settings", "SETTINGS", UISprite.Gear128, 120, visible: static () => CoreNav);
        Category("credits", "Credits", "CREDITS", UISprite.Star128, 140, visible: static () => CoreNav);
        Category("developer", "Developer", "DEVELOPER", UISprite.Code128, 150, visible: static () => CoreNav && Info.IsDev);
    }
    private static void Overlay() {
    }
    private static void Gameplay() {
    }
    private static void Visuals() {
    }
    private static void Standalone() {
        Page(HomePageKey, HomeCategoryKey, 10, "Home", "HOME", PageHome.Create);
        Page(SearchPageKey, "search", 10, "Search", "SEARCH", PageSearch.Create);
        Page("profiles.main", "profiles", 10, "Profiles", "PROFILES", PageProfiles.Create);
        Page("import.main", "import", 10, "Import", "IMPORT", PageImport.Create);
        Page(FeaturesPageKey, FeaturesCategoryKey, 0, "Features", "FEATURES", PageFeatures.Create,
            visible: static () => CoreNav);
        Page(AddonsPageKey, AddonsCategoryKey, 90, "Addons", "ADDONS", PageAddons.Create, separatorBefore: true,
            visible: static () => CoreNav);
        Page(SettingsPageKey, "settings", 10, "Settings", "SETTINGS", PageSettings.Create);
        Page("credits.main", "credits", 10, "Credits", "CREDITS", PageCredits.Create);
        Page("developer.main", "developer", 10, "Developer", "DEVELOPER", PageDeveloper.Create,
            visible: static () => CoreNav && Info.IsDev);
    }
    private static void Category(
        string key, string title, string localeKey, UISprite icon, int order,
        bool alwaysSubmenu = false, Func<bool> visible = null, bool togglable = false, float iconScale = 1f
    ) => NavRegistry.AddCategory(new NavCategory {
        Key = key,
        Title = title,
        LocaleKey = localeKey,
        Icon = icon,
        Order = order,
        IconScale = iconScale,
        AlwaysSubmenu = alwaysSubmenu,
        Togglable = togglable,
        Visible = togglable
            ? visible == null
                ? () => Quartz.Features.FeatureCategories.IsEnabled(key)
                : () => visible() && Quartz.Features.FeatureCategories.IsEnabled(key)
            : visible,
    });
    private static void Page(
        string key, string categoryKey, int order, string title, string localeKey,
        Action<UnityEngine.RectTransform> build, Func<string> searchLabel = null, Func<bool> visible = null,
        bool separatorBefore = false
    ) => NavRegistry.AddPage(new NavPage {
        Key = key,
        CategoryKey = categoryKey,
        Order = order,
        Title = title,
        LocaleKey = localeKey,
        Build = build,
        SearchLabel = searchLabel,
        Visible = visible,
        OwnScroll = true,
        SeparatorBefore = separatorBefore,
    });
}
