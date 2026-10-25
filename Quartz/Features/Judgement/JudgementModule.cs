using Quartz.UI.Nav;
namespace Quartz.Features.Judgement;
[Feature("judgement", "overlay", 50, "Judgement", "SECTION_JUDGEMENT", Deps = new[] { "overlay" })]
public sealed class JudgementModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(JudgementModule), $"Quartz.Features.Judgement.Lang.{lang}.json");
        JudgementOverlay.EnsureConf();
        Context.RegisterRescalable("judgement", JudgementOverlay.Rescale);
        Context.AddPage(new NavPage {
            Key = "overlay.judgement", CategoryKey = "overlay", Order = 50,
            Title = "Judgement", LocaleKey = "SECTION_JUDGEMENT", Build = PageJudgement.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new JudgementImport());
        Context.PatchAll();
        Context.OnModEnable("JudgementOverlay", () => JudgementOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("JudgementOverlay", JudgementOverlay.Dispose);
    }
    public override void OnUnload() => JudgementOverlay.Dispose();
    public override void OnTick() => JudgementOverlay.Gate();
}
