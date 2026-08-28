using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.VisualTweaks;
public sealed class VisualTweaksModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(VisualTweaksModule), $"Quartz.Features.VisualTweaks.Lang.{lang}.json");
        VisualTweaks.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "visuals.visualtweaks",
            CategoryKey = "visuals",
            Order = 30,
            Title = "Visual Tweaks",
            LocaleKey = "SECTION_VISUAL_TWEAKS",
            Build = PageVisualTweaks.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new VisualTweaksImport());
        Context.PatchAll(typeof(VisualTweaksModule));
        Context.OnModEnable("VisualTweaks", VisualTweaks.RefreshAll);
        Context.OnModDisable("VisualTweaks", VisualTweaks.RestoreAll);
    }
    public override void OnUnload() => VisualTweaks.RestoreAll();
}
