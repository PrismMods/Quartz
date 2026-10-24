using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Tweaks;
[Feature("tweaks", "tweaks", 10, "Tweaks", "TWEAKS")]
public sealed class TweaksModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(TweaksModule), $"Quartz.Features.Tweaks.Lang.{lang}.json");
        Tweaks.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "tweaks.general",
            CategoryKey = "tweaks",
            Order = 10,
            Title = "General",
            LocaleKey = "TWEAKS_GENERAL",
            Build = PageTweaks.GeneralPage,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new TweaksImport());
        Context.PatchAll();
    }
}
