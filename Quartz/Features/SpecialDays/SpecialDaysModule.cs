using Quartz.UI.Nav;
namespace Quartz.Features.SpecialDays;
[Feature("specialdays", "tweaks", 17, "Special Days", "SECTION_SPECIAL_DAYS")]
public sealed class SpecialDaysModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(SpecialDaysModule), $"Quartz.Features.SpecialDays.Lang.{lang}.json");
        SpecialDays.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "tweaks.specialdays", CategoryKey = "tweaks", Order = 17,
            Title = "Special Days", LocaleKey = "SECTION_SPECIAL_DAYS",
#if QUARTZ_CLASSIC_UI
            Build = PageSpecialDays.Create,
#endif
            OwnScroll = true,
        });
        Context.PatchAll();
    }
}
