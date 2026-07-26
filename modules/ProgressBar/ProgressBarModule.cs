using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.ProgressBar;
public sealed class ProgressBarModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(ProgressBarModule), $"Quartz.Features.ProgressBar.Lang.{lang}.json");
        ProgressBarOverlay.EnsureConf();
        Context.ReserveOverlayBand(ProgressBarOverlay.BandId, ProgressBarOverlay.BottomEdge);
        Context.RegisterRescalable("progressbar", ProgressBarOverlay.Rescale);
        Context.AddPage(new NavPage {
            Key = "overlay.progressbar", CategoryKey = "overlay", Order = 30,
            Title = "Progress Bar", LocaleKey = "SECTION_PROGRESS_BAR", Build = PageProgressBar.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new ProgressBarImport());
        Context.PatchAll(typeof(ProgressBarModule));
        Context.OnModEnable("ProgressBarOverlay", () => ProgressBarOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("ProgressBarOverlay", ProgressBarOverlay.Dispose);
    }
    public override void OnUnload() => ProgressBarOverlay.Dispose();
}
