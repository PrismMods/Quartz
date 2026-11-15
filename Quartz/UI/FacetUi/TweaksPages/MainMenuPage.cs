#if !IL2CPP
using Facet;
using Quartz.Features.MainMenu;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TweaksPages;
/// <summary>Tweaks ▸ Main Menu in the new menu, row for row as PageMainMenu. Both tempo sliders always show and save on
/// release, as on the classic page.</summary>
internal static class MainMenuPage {
    private static MenuTweaksSettings C => MenuTweaks.Conf;
    private static readonly MenuTweaksSettings Def = new();

    internal static void Build(VisualElement p) {
        MenuTweaks.EnsureConf();
        ShellPage.Feature(p, "mainmenu", "SECTION_MAIN_MENU", "MAINMENU_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        Action save = MenuTweaks.Save;
        VisualElement card = Controls.Card(p, null);
        ShellRows.Toggle(card, "TW_MENUMUSIC", "tw_menumusic", Bind.To(() => C.DisableMenuMusic, v => C.DisableMenuMusic = v), null, save,
            o => { o.Default = Def.DisableMenuMusic; o.Tooltip = "DESC_TW_MENUMUSIC"; });
        ShellRows.Toggle(card, "TW_MENUBPM", "tw_menubpm", Bind.To(() => C.MenuBpmEnabled, v => C.MenuBpmEnabled = v), null, save,
            o => { o.Default = Def.MenuBpmEnabled; o.Tooltip = "DESC_TW_MENUBPM"; });
        ShellRows.ReleaseSlider(card, "TW_MENUSLOWBPM", Bind.To(() => C.MenuSlowBpm, v => C.MenuSlowBpm = v), 30f, 600f, 1f, "0", null, save,
            o => o.Default = Def.MenuSlowBpm);
        ShellRows.ReleaseSlider(card, "TW_MENUHIGHBPM", Bind.To(() => C.MenuHighBpm, v => C.MenuHighBpm = v), 30f, 600f, 1f, "0", null, save,
            o => o.Default = Def.MenuHighBpm);
    }
}
#endif
