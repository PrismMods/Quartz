using Quartz.UI.Nav;
namespace Quartz.Features.ProgressBar;
[Feature("progressbar", "overlay", 30, "Progress Bar", "SECTION_PROGRESS_BAR", Deps = new[] { "overlay" })]
public sealed class ProgressBarModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(ProgressBarModule), $"Quartz.Features.ProgressBar.Lang.{lang}.json");
        ProgressBarOverlay.EnsureConf();
        FeatureMasters.Claim(ProgressBarOverlay.ConfMgr, ProgressBarOverlay.Conf.Enabled, static c => c.Enabled = true);
        Context.ReserveOverlayBand(ProgressBarOverlay.BandId, ProgressBarOverlay.BottomEdge);
        Context.RegisterRescalable("progressbar", ProgressBarOverlay.Rescale);
        Context.AddPage(new NavPage {
            Key = "overlay.progressbar", CategoryKey = "overlay", Order = 30,
            Title = "Progress Bar", LocaleKey = "SECTION_PROGRESS_BAR", Build = PageProgressBar.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new ProgressBarImport());
        Context.PatchAll();
        Context.OnModEnable("ProgressBarOverlay", () => ProgressBarOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("ProgressBarOverlay", ProgressBarOverlay.Dispose);
    }
    public override void OnUnload() => ProgressBarOverlay.Dispose();
    public override void OnTick() => ProgressBarOverlay.Gate();
}
