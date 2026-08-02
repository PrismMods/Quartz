using Quartz.Modules;
using Quartz.Resource;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Countdown;
public sealed class CountdownModule : QuartzModule {
    public static ResourceManager Resources { get; private set; }
    public override void OnLoad() {
        Resources = Context.Resources(typeof(CountdownModule), "Quartz.Features.Countdown.Resource.");
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(CountdownModule), $"Quartz.Features.Countdown.Lang.{lang}.json");
        CountdownFeature.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "gameplay.countdown",
            CategoryKey = "gameplay",
            Order = 40,
            Title = "Countdown",
            LocaleKey = "SECTION_COUNTDOWN",
            OwnScroll = true,
            Build = PageCountdown.Create,
        });
        Context.PatchAll(typeof(CountdownModule));
        Context.OnModEnable("CountdownFeature", CountdownFeature.Initialize);
        Context.OnModDisable("CountdownFeature", CountdownFeature.Dispose);
    }
    public override void OnUnload() {
        CountdownFeature.Dispose();
        Resources?.Dispose();
        Resources = null;
    }
}
