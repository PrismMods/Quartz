using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.DeathLimit;
public sealed class DeathLimitModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(DeathLimitModule), $"Quartz.Features.DeathLimit.Lang.{lang}.json");
        DeathLimit.EnsureConf();
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
        Context.PatchAll(typeof(DeathLimitModule));
    }
}
