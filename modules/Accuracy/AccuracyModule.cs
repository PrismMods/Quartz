using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Accuracy;
public sealed class AccuracyModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(AccuracyModule), $"Quartz.Features.Accuracy.Lang.{lang}.json");
        AccuracyOverlay.EnsureConf();
        AccuracyOverlay.RegisterStats();
        Context.AddPage(new NavPage {
            Key = "overlay.accuracy", CategoryKey = "overlay", Order = 90,
            Title = "Too Much Accuracy", LocaleKey = "SECTION_ACCURACY", Build = PageAccuracy.Create,
            OwnScroll = true,
        });
        Context.PatchAll(typeof(AccuracyModule));
    }
}
