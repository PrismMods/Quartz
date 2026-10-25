using Quartz.UI.Nav;
namespace Quartz.Features.Overlay;
[Feature("overlay", "overlay", 5, "Overlay Core", "MODULE_OVERLAY_CORE", Patches = new string[0])]
public sealed class OverlayModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(OverlayModule), $"Quartz.Features.Overlay.Lang.{lang}.json");
        OverlaySwitch.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "overlay.general",
            CategoryKey = "overlay",
            Order = 10,
            Title = "General",
            LocaleKey = "OVERLAY_GENERAL",
            Build = PageOverlayGeneral.Create,
            OwnScroll = true,
            // The KeyViewer flavour has one page; its Reorganize button is on the key viewer page.
            Visible = static () => !Quartz.Core.Info.KeyViewerOnly,
        });
        Context.PatchAll();
        Context.OnModEnable("OverlayMotion", OverlayMotion.Attach);
        Context.OnModDisable("OverlayMotion", OverlayMotion.Detach);
    }
    public override void OnUnload() => OverlayMotion.Detach();
}
