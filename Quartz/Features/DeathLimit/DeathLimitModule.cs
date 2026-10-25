using Quartz.UI.Nav;
namespace Quartz.Features.DeathLimit;
[Feature("deathlimit", "gameplay", 40, "Death Limit", "SECTION_DEATH_LIMIT")]
public sealed class DeathLimitModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(DeathLimitModule), $"Quartz.Features.DeathLimit.Lang.{lang}.json");
        DeathLimit.EnsureConf();
        FeatureMasters.Claim(DeathLimit.ConfMgr, DeathLimit.Conf.DeathLimitEnabled, static c => c.DeathLimitEnabled = true);
        Context.AddPage(new NavPage {
            Key = "gameplay.death",
            CategoryKey = "gameplay",
            Order = 40,
            Title = "Death Limit",
            LocaleKey = "SECTION_DEATH_LIMIT",
            Build = PageDeathLimit.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new DeathLimitImport());
        Context.PatchAll();
    }
}
