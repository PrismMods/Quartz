using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Panels;
public sealed class PanelsModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(PanelsModule), $"Quartz.Features.Panels.Lang.{lang}.json");
        PanelsOverlay.EnsureConf();
        Context.RegisterRescalable("panels", PanelsOverlay.Rescale);
        Context.AddPage(new NavPage {
            Key = "overlay.panels", CategoryKey = "overlay", Order = 70,
            Title = "Panels", LocaleKey = "SECTION_PANELS", Build = PagePanels.Create,
            OwnScroll = true,
        });
        Context.PatchAll(typeof(PanelsModule));
        Context.OnModEnable("PanelsOverlay", () => PanelsOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("PanelsOverlay", PanelsOverlay.Dispose);
    }
    public override void OnUnload() => PanelsOverlay.Dispose();
}
