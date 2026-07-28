using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Judgement;
public sealed class JudgementModule : QuartzModule {
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
        Context.PatchAll(typeof(JudgementModule));
        Context.OnModEnable("JudgementOverlay", () => JudgementOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("JudgementOverlay", JudgementOverlay.Dispose);
    }
    public override void OnUnload() => JudgementOverlay.Dispose();
}
