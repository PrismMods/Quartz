using Quartz.UI.Nav;
namespace Quartz.Features.Panels;
[Feature("panels", "overlay", 70, "Panels", "SECTION_PANELS", Deps = new[] { "overlay" })]
public sealed class PanelsModule : QuartzFeature {
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
        Context.PatchAll();
        Context.OnModEnable("PanelsOverlay", () => PanelsOverlay.Initialize(Quartz.Core.MainCore.Root));
        Context.OnModDisable("PanelsOverlay", PanelsOverlay.Dispose);
    }
    public override void OnUnload() => PanelsOverlay.Dispose();
    public override void OnTick() => PanelsOverlay.Gate();
}
