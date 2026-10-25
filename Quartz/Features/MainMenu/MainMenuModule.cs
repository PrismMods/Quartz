using Quartz.UI.Nav;
namespace Quartz.Features.MainMenu;
[Feature("mainmenu", "tweaks", 15, "Main Menu", "SECTION_MAIN_MENU")]
public sealed class MainMenuModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(MainMenuModule), $"Quartz.Features.MainMenu.Lang.{lang}.json");
        MenuTweaks.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "tweaks.mainmenu",
            CategoryKey = "tweaks",
            Order = 30,
            Title = "Main Menu",
            LocaleKey = "SECTION_MAIN_MENU",
            Build = PageMainMenu.Create,
            OwnScroll = true,
        });
        Context.PatchAll();
    }
}
