using Quartz.UI.Nav;
namespace Quartz.Features.PlanetColors;
[Feature("planetcolors", "visuals", 40, "Planet Colors", "SECTION_PLANET_COLORS")]
public sealed class PlanetColorsModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(PlanetColorsModule), $"Quartz.Features.PlanetColors.Lang.{lang}.json");
        PlanetColors.EnsureConf();
        FeatureMasters.Claim(PlanetColors.ConfMgr, PlanetColors.Conf.Enabled, static c => c.Enabled = true);
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
        Context.PatchAll();
        Context.OnModEnable("PlanetColors", PlanetColors.Refresh);
        Context.OnModDisable("PlanetColors", PlanetColors.Restore);
    }
    public override void OnUnload() => PlanetColors.Restore();
}
