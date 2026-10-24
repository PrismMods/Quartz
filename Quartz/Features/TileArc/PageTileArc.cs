using Quartz.Features.TileArc;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageTileArc {
    public static void Create(RectTransform parent) =>
        CreateTileArc(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateTileArc(Transform content) {
        TileArc.EnsureConf();
        TileArcSettings conf = TileArc.Conf;
        TileArcSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Tile Arc",
            v => {
                conf.Enabled = v;
                TileArc.Refresh();
                TileArc.Save();
            },
            conf.Enabled,
            "Enable Tile Arc", "tilearc_enable", def.Enabled
        );
        UISlider intensity = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.Intensity * 100f, 0f, 100f, conf.Intensity * 100f,
            Mathf.Round,
            v => conf.Intensity = Mathf.Clamp01(v / 100f),
            v => {
                conf.Intensity = Mathf.Clamp01(v / 100f);
                TileArc.Refresh();
                TileArc.Save();
            },
            "Arc Intensity", "tilearc_intensity"
        );
        intensity.Format = "0";
        intensity.Rect.AddToolTip(
            "DESC_TILEARC_INTENSITY",
            "How far the rounded corner reaches, as a percentage of the tile's width. 100% is a full half-circle corner; 0% leaves the turn sharp."
        );
    }
}
