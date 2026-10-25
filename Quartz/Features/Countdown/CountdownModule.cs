using Quartz.UI.Nav;
namespace Quartz.Features.Countdown;
[Feature("countdown", "gameplay", 40, "Countdown", "SECTION_COUNTDOWN")]
public sealed class CountdownModule : QuartzFeature {
    public override void OnLoad() {
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
        Context.PatchAll();
        Context.OnModEnable("CountdownFeature", CountdownFeature.Initialize);
        Context.OnModDisable("CountdownFeature", CountdownFeature.Dispose);
    }
    public override void OnUnload() => CountdownFeature.Dispose();
}
