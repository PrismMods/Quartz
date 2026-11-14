#if !IL2CPP
using Facet;
using Quartz.Features.TileArc;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.VisualsPages;
/// <summary>Visuals ▸ Tile Arc in the new menu. The arc rebuilds the tile meshes, so it applies and saves on release.</summary>
internal static class TileArcPage {
    private static readonly TileArcSettings Def = new();

    internal static void Build(VisualElement p) {
        TileArc.EnsureConf();
        ShellPage.Feature(p, "tilearc", "SECTION_TILE_ARC", "TILEARC_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        ShellRows.ReleaseSlider(card, "TILEARC_INTENSITY", Bind.To(() => TileArc.Conf.Intensity * 100f, v => TileArc.Conf.Intensity = Mathf.Clamp01(v / 100f)),
            0f, 100f, 1f, "0", null, () => {
                TileArc.Refresh();
                TileArc.Save();
            }, o => {
                o.Default = Def.Intensity * 100f;
                o.Tooltip = "DESC_TILEARC_INTENSITY";
            });
    }
}
#endif
