using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Tweaks;
public sealed class TweaksModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(TweaksModule), $"Quartz.Features.Tweaks.Lang.{lang}.json");
        Tweaks.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "tweaks.general",
            CategoryKey = "tweaks",
            Order = 10,
            Title = "General",
            LocaleKey = "TWEAKS_GENERAL",
            Build = PageTweaks.GeneralPage,
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "tweaks.mainmenu",
            CategoryKey = "tweaks",
            Order = 30,
            Title = "Main Menu",
            LocaleKey = "SECTION_MAIN_MENU",
            Build = PageTweaks.MainMenuPage,
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "visuals.visualtweaks",
            CategoryKey = "visuals",
            Order = 30,
            Title = "Visual Tweaks",
            LocaleKey = "SECTION_VISUAL_TWEAKS",
            Build = PageVisualTweaks.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new TweaksImport());
        Context.PatchAll(typeof(TweaksModule));
        Context.OnModEnable("Tweaks", Tweaks.RefreshAll);
        Context.OnModDisable("Tweaks", Tweaks.RestoreAll);
    }
    public override void OnUnload() => Tweaks.RestoreAll();
}
