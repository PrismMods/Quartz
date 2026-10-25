using Quartz.UI.Nav;
namespace Quartz.Features.Restriction;
[Feature("restriction", "gameplay", 30, "Judgement Restriction", "SECTION_JUDGEMENT_RESTRICTION")]
public sealed class RestrictionModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(RestrictionModule), $"Quartz.Features.Restriction.Lang.{lang}.json");
        Restriction.EnsureConf();
        FeatureMasters.Claim(Restriction.ConfMgr, Restriction.Conf.JRestrictEnabled, static c => c.JRestrictEnabled = true);
        Context.AddPage(new NavPage {
            Key = "gameplay.judgement",
            CategoryKey = "gameplay",
            Order = 30,
            Title = "Judgement Restriction",
            LocaleKey = "SECTION_JUDGEMENT_RESTRICTION",
            Build = PageRestriction.JudgementPage,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new RestrictionImport());
        Context.PatchAll();
    }
}
