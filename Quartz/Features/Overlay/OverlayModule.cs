using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
using Quartz.Features;
namespace Quartz.Overlay;
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
            // The standalone KeyViewer build is meant to be exactly one page, and
            // this one carries settings for overlays it does not ship. Its Reorganize
            // button also lives on the key viewer's own page, so nothing is lost.
            Visible = static () => !Quartz.Core.Info.KeyViewerOnly,
        });
        Context.PatchAll();
        Context.OnModEnable("OverlayMotion", OverlayMotion.Attach);
        Context.OnModDisable("OverlayMotion", OverlayMotion.Detach);
    }
    public override void OnUnload() => OverlayMotion.Detach();
}
