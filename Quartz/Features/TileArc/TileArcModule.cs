using Quartz.UI.Nav;
namespace Quartz.Features.TileArc;
[Feature("tilearc", "visuals", 25, "Tile Arc", "SECTION_TILE_ARC")]
public sealed class TileArcModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(TileArcModule), $"Quartz.Features.TileArc.Lang.{lang}.json");
        TileArc.EnsureConf();
        FeatureMasters.Claim(TileArc.ConfMgr, TileArc.Conf.Enabled, static c => c.Enabled = true);
        Context.AddPage(new NavPage {
            Key = "visuals.tilearc", CategoryKey = "visuals", Order = 25,
            Title = "Tile Arc", LocaleKey = "SECTION_TILE_ARC", Build = PageTileArc.Create,
            OwnScroll = true,
        });
        Context.PatchAll();
        Context.OnModEnable("TileArc", TileArc.Refresh);
        Context.OnModDisable("TileArc", TileArc.Refresh);
    }
    public override void OnUnload() => TileArc.ClearMeshCache();
}
