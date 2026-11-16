using Quartz.UI.Nav;
namespace Quartz.Features.HideJudgements;
[Feature("hidejudgements", "visuals", 20, "Hide Judgements", "SECTION_HIDE_JUDGEMENTS")]
public sealed class HideJudgementsModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(HideJudgementsModule), $"Quartz.Features.HideJudgements.Lang.{lang}.json");
        JudgementPopupHider.EnsureConf();
        FeatureMasters.Claim(JudgementPopupHider.ConfMgr, JudgementPopupHider.Conf.Enabled, static c => c.Enabled = true);
        Context.AddPage(new NavPage {
            Key = "visuals.hidejudgements", CategoryKey = "visuals", Order = 20,
            Title = "Hide Judgements", LocaleKey = "SECTION_HIDE_JUDGEMENTS",
#if QUARTZ_CLASSIC_UI
            Build = PageHideJudgements.Create,
#endif
            OwnScroll = true,
        });
        Context.PatchAll();
    }
}
