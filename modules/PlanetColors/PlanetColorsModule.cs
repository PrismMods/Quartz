using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.PlanetColors;
public sealed class PlanetColorsModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(PlanetColorsModule), $"Quartz.Features.PlanetColors.Lang.{lang}.json");
        PlanetColors.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "visuals.planetcolors",
            CategoryKey = "visuals",
            Order = 40,
            Title = "Planet Colors",
            LocaleKey = "SECTION_PLANET_COLORS",
            Build = PagePlanetColors.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new PlanetColorsImport());
        Context.PatchAll(typeof(PlanetColorsModule));
        Context.OnModEnable("PlanetColors", PlanetColors.Refresh);
        Context.OnModDisable("PlanetColors", PlanetColors.Restore);
    }
    public override void OnUnload() => PlanetColors.Restore();
}
