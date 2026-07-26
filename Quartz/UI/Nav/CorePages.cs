using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Factory.Page;
using Quartz.UI.Generator;
namespace Quartz.UI.Nav;
internal static class CorePages {
    internal const string HomeCategoryKey = "home";
    internal const string HomePageKey = "home.overview";
    internal const string SearchPageKey = "search.main";
    internal const string ModulesCategoryKey = "modules";
    internal const string AddonsCategoryKey = ModulesCategoryKey;
    internal const string AddonsPageKey = "addons.main";
    internal const string ModulesPageKey = "modules.main";
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
    private static void Categories() {
        Category(HomeCategoryKey, "Home", "HOME", UISprite.QuartzLogo, 0, iconScale: 1.45f);
        Category(ModulesCategoryKey, "Modules", "MODULES", UISprite.Wrench128, 5, alwaysSubmenu: true);
        Category("overlay", "Overlay", "OVERLAY", UISprite.Monitor128, 10, togglable: true);
        Category("gameplay", "Gameplay", "GAMEPLAY", UISprite.Gamepad128, 20, togglable: true);
        Category("visuals", "Visuals", "VISUALS", UISprite.Image128, 30, togglable: true);
        Category("tweaks", "Tweaks", "TWEAKS", UISprite.AdjustmentsHorizontal128, 40, togglable: true);
        // Pages come from the editor module; the category hides itself when it is not installed.
        Category("editor", "Editor", "EDITOR", UISprite.Wrench128, 50, togglable: true);
        // Pages come from the nostalgia module; the category hides itself when it is not installed.
        Category("nostalgia", "Nostalgia", "NOSTALGIA", UISprite.ClockRewind128, 60, togglable: true);
        Category("tuf", "TUF", "TUF", UISprite.TufLogo, 70, togglable: true);
        Category("search", "Search", "SEARCH", UISprite.MagnifyingGlass128, 80);
        Category("profiles", "Profiles", "PROFILES", UISprite.Users128, 90);
        Category("import", "Import", "IMPORT", UISprite.Book128, 100);
        Category("settings", "Settings", "SETTINGS", UISprite.Gear128, 120);
        Category("help", "Help", "HELP", UISprite.QuestionMarkCircle128, 130, alwaysSubmenu: true);
        Category("credits", "Credits", "CREDITS", UISprite.Star128, 140);
        Category("developer", "Developer", "DEVELOPER", UISprite.Wrench128, 150, visible: static () => Info.IsDev);
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
        Page(ModulesPageKey, ModulesCategoryKey, 0, "All Modules", "MODULES_ALL", PageModules.Create);
        foreach(NavCategory category in NavRegistry.AllCategories) {
            if(!category.Togglable) continue;
            string key = category.Key;
            Page("modules." + key, ModulesCategoryKey, category.Order, category.Title, category.LocaleKey,
                rect => PageModuleCategory.Create(rect, key),
                () => GenerateUI.Tr("MODULES", "Modules") + " · " + GenerateUI.Tr(category.LocaleKey, category.Title),
                () => Quartz.Modules.ModuleCategories.IsEnabled(key));
        }
        Page(AddonsPageKey, AddonsCategoryKey, 90, "Addons", "ADDONS", PageAddons.Create, separatorBefore: true);
        Page(SettingsPageKey, "settings", 10, "Settings", "SETTINGS", PageSettings.Create);
        Page("help.faq", "help", 10, "FAQ", "FAQ", PageHelp.FaqPage,
            static () => GenerateUI.Tr("HELP", "Help") + " · " + GenerateUI.Tr("FAQ", "FAQ"));
        Page("credits.main", "credits", 10, "Credits", "CREDITS", PageCredits.Create);
        Page("developer.main", "developer", 10, "Developer", "DEVELOPER", PageDeveloper.Create,
            visible: static () => Info.IsDev);
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
                ? () => Quartz.Modules.ModuleCategories.IsEnabled(key)
                : () => visible() && Quartz.Modules.ModuleCategories.IsEnabled(key)
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
