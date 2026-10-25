using Quartz.UI.Nav;
namespace Quartz.Features.AutoDeafen;
[Feature("autodeafen", "gameplay", 50, "Auto Deafen", "SECTION_AUTO_DEAFEN_DISCORD")]
public sealed class AutoDeafenModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(AutoDeafenModule), $"Quartz.Features.AutoDeafen.Lang.{lang}.json");
        AutoDeafen.EnsureConf();
        FeatureMasters.Claim(AutoDeafen.ConfMgr, AutoDeafen.Conf.Enabled, static c => c.Enabled = true);
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
        Context.PatchAll();
        Context.OnModDisable("AutoDeafen", AutoDeafen.Stop);
    }
    public override void OnUnload() => AutoDeafen.Stop();
}
