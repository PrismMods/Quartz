using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.TileArc;
public sealed class TileArcModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(TileArcModule), $"Quartz.Features.TileArc.Lang.{lang}.json");
        TileArc.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "visuals.tilearc", CategoryKey = "visuals", Order = 25,
            Title = "Tile Arc", LocaleKey = "SECTION_TILE_ARC", Build = PageTileArc.Create,
            OwnScroll = true,
        });
        Context.PatchAll(typeof(TileArcModule));
        Context.OnModEnable("TileArc", TileArc.Refresh);
        Context.OnModDisable("TileArc", TileArc.Refresh);
    }
    public override void OnUnload() => TileArc.ClearMeshCache();
}
