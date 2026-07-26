using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Restriction;
public sealed class RestrictionModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(RestrictionModule), $"Quartz.Features.Restriction.Lang.{lang}.json");
        Restriction.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "gameplay.judgement",
            CategoryKey = "gameplay",
            Order = 30,
            Title = "Judgement Restriction",
            LocaleKey = "SECTION_JUDGEMENT_RESTRICTION",
            Build = PageRestriction.JudgementPage,
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "gameplay.death",
            CategoryKey = "gameplay",
            Order = 40,
            Title = "Death Limit",
            LocaleKey = "SECTION_DEATH_LIMIT",
            Build = PageRestriction.DeathLimitPage,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new RestrictionImport());
        Context.PatchAll(typeof(RestrictionModule));
    }
}
