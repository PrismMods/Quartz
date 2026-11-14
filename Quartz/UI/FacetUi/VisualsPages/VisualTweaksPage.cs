#if !IL2CPP
using Facet;
using Quartz.Features.VisualTweaks;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.VisualsPages;
/// <summary>Visuals ▸ Visual Tweaks in the new menu, row for row as PageVisualTweaks. Both speed label lists always show,
/// as on the classic page.</summary>
internal static class VisualTweaksPage {
    private static VisualTweaksSettings C => VisualTweaks.Conf;
    private static readonly VisualTweaksSettings Def = new();

    internal static void Build(VisualElement p) {
        VisualTweaks.EnsureConf();
        ShellPage.Feature(p, "visualtweaks", "SECTION_VISUAL_TWEAKS", "VISUALTWEAKS_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        Action save = VisualTweaks.Save, labels = VisualTweaks.RefreshSpeedBpmLabels;
        VisualElement card = Controls.Card(p, null);
        ShellRows.Toggle(card, "TW_CP", "tw_cp", Bind.To(() => C.RemoveAllCheckpoints, v => C.RemoveAllCheckpoints = v), VisualTweaks.RefreshCheckpointTweak,
            save, o => { o.Default = Def.RemoveAllCheckpoints; o.Tooltip = "DESC_TW_CP"; });
        ShellRows.Toggle(card, "TW_BCP", "tw_bcp", Bind.To(() => C.RemoveBallCoreParticles, v => C.RemoveBallCoreParticles = v),
            VisualTweaks.RefreshBallCoreParticlesTweak, save, o => { o.Default = Def.RemoveBallCoreParticles; o.Tooltip = "DESC_TW_BCP"; });
        ShellRows.Toggle(card, "TW_GLOW", "tw_glow", Bind.To(() => C.DisableTileHitGlow, v => C.DisableTileHitGlow = v), VisualTweaks.RefreshTileHitGlowTweak,
            save, o => { o.Default = Def.DisableTileHitGlow; o.Tooltip = "DESC_TW_GLOW"; });
        ShellRows.Toggle(card, "TW_PGLOW", "tw_pglow", Bind.To(() => C.RemovePlanetGlow, v => C.RemovePlanetGlow = v), VisualTweaks.RefreshPlanetGlowTweak,
            save, o => { o.Default = Def.RemovePlanetGlow; o.Tooltip = "DESC_TW_PGLOW"; });
        ShellRows.Toggle(card, "TW_SBPM", "tw_sbpm", Bind.To(() => C.ShowSpeedChangeBpm, v => C.ShowSpeedChangeBpm = v), labels, save,
            o => { o.Default = Def.ShowSpeedChangeBpm; o.Tooltip = "DESC_TW_SBPM"; });
        Controls.Dropdown(card, "TW_SBPM_SHOW_LABEL", QuartzShell.Writes(() => C.SpeedBpmDisplay, v => {
            C.SpeedBpmDisplay = v;
            labels();
            save();
        }), [VisualTweaks.SpeedBpmShowBpm, VisualTweaks.SpeedBpmShowMultiplier, VisualTweaks.SpeedBpmShowBoth], m => m switch {
            VisualTweaks.SpeedBpmShowMultiplier => "TW_SBPM_SHOW_MULT",
            VisualTweaks.SpeedBpmShowBoth => "TW_SBPM_SHOW_BOTH",
            _ => "TW_SBPM_SHOW_BPM",
        }, o => {
            o.Id = "TW_SBPM_SHOW_LABEL";
            o.Default = Def.SpeedBpmDisplay;
        });
        Controls.Dropdown(card, "TW_SBPM_POS_LABEL", QuartzShell.Writes(() => C.SpeedBpmPosition, v => {
            C.SpeedBpmPosition = v;
            labels();
            save();
        }), [VisualTweaks.SpeedBpmAbove, VisualTweaks.SpeedBpmBelow], m => m == VisualTweaks.SpeedBpmBelow ? "TW_SBPM_POS_BELOW" : "TW_SBPM_POS_ABOVE", o => {
            o.Id = "TW_SBPM_POS_LABEL";
            o.Default = Def.SpeedBpmPosition;
        });
    }
}
#endif
