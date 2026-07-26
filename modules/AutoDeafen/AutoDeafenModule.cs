using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.AutoDeafen;
public sealed class AutoDeafenModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(AutoDeafenModule), $"Quartz.Features.AutoDeafen.Lang.{lang}.json");
        AutoDeafen.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "gameplay.autodeafen",
            CategoryKey = "gameplay",
            Order = 50,
            Title = "Auto Deafen (Discord)",
            LocaleKey = "SECTION_AUTO_DEAFEN_DISCORD",
            OwnScroll = true,
            Build = PageAutoDeafen.Create,
        });
        Context.RegisterInjectedKeySource(AutoDeafen.IsInjectedKey, () => AutoDeafen.InjectGuardActive);
        Context.PatchAll(typeof(AutoDeafenModule));
        Context.OnModDisable("AutoDeafen", AutoDeafen.Stop);
    }
    public override void OnUnload() => AutoDeafen.Stop();
}
