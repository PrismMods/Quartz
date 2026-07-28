using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.HideJudgements;
public sealed class HideJudgementsModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(HideJudgementsModule), $"Quartz.Features.HideJudgements.Lang.{lang}.json");
        JudgementPopupHider.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "visuals.hidejudgements", CategoryKey = "visuals", Order = 20,
            Title = "Hide Judgements", LocaleKey = "SECTION_HIDE_JUDGEMENTS", Build = PageHideJudgements.Create,
            OwnScroll = true,
        });
        Context.PatchAll(typeof(HideJudgementsModule));
    }
}
