using Quartz.Features.VisualTweaks;
using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageVisualTweaks {
    public static void Create(RectTransform parent) =>
        CreateVisualTweaks(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateVisualTweaks(Transform content) {
        VisualTweaks.EnsureConf();
        VisualTweaksSettings conf = VisualTweaks.Conf;
        VisualTweaksSettings def = new();
        var sec = GenerateUI.FlatSection(content, "Visual Tweaks");
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemoveAllCheckpoints,
            conf.RemoveAllCheckpoints,
            v => { conf.RemoveAllCheckpoints = v; VisualTweaks.RefreshCheckpointTweak(); VisualTweaks.Save(); },
            "Remove All Checkpoints",
            "tw_cp",
            "Strips checkpoint icons and behavior from the level \u2014 dying always restarts the run. Turning this off needs a level reload to bring icons back."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemoveBallCoreParticles,
            conf.RemoveBallCoreParticles,
            v => { conf.RemoveBallCoreParticles = v; VisualTweaks.RefreshBallCoreParticlesTweak(); VisualTweaks.Save(); },
            "Remove Ball Core Particles",
            "tw_bcp",
            "Removes the planets' core and spark particles."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.DisableTileHitGlow,
            conf.DisableTileHitGlow,
            v => { conf.DisableTileHitGlow = v; VisualTweaks.RefreshTileHitGlowTweak(); VisualTweaks.Save(); },
            "Disable Tile Hit Glow",
            "tw_glow",
            "Suppresses the glow flash tiles get when the planet lands on them."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemovePlanetGlow,
            conf.RemovePlanetGlow,
            v => { conf.RemovePlanetGlow = v; VisualTweaks.RefreshPlanetGlowTweak(); VisualTweaks.Save(); },
            "Remove Planet Glow",
            "tw_pglow",
            "Hides the glow sprite drawn around the planets."
        );
    }
}
