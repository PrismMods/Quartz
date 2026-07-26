using Quartz.Core;
using Quartz.Features.Tweaks;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageVisualTweaks {
    public static void Create(RectTransform parent) =>
        CreateVisualTweaks(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateVisualTweaks(Transform content) {
        Tweaks.EnsureConf();
        TweaksSettings conf = Tweaks.Conf;
        TweaksSettings def = new();
        var sec = GenerateUI.FlatSection(content, "Visual Tweaks");
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemoveAllCheckpoints,
            conf.RemoveAllCheckpoints,
            v => { conf.RemoveAllCheckpoints = v; Tweaks.RefreshCheckpointTweak(); Tweaks.Save(); },
            "Remove All Checkpoints",
            "tw_cp",
            "Strips checkpoint icons and behavior from the level — dying always restarts the run. Turning this off needs a level reload to bring icons back."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemoveBallCoreParticles,
            conf.RemoveBallCoreParticles,
            v => { conf.RemoveBallCoreParticles = v; Tweaks.RefreshBallCoreParticlesTweak(); Tweaks.Save(); },
            "Remove Ball Core Particles",
            "tw_bcp",
            "Removes the planets' core and spark particles."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.DisableTileHitGlow,
            conf.DisableTileHitGlow,
            v => { conf.DisableTileHitGlow = v; Tweaks.RefreshTileHitGlowTweak(); Tweaks.Save(); },
            "Disable Tile Hit Glow",
            "tw_glow",
            "Suppresses the glow flash tiles get when the planet lands on them."
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RemovePlanetGlow,
            conf.RemovePlanetGlow,
            v => { conf.RemovePlanetGlow = v; Tweaks.RefreshPlanetGlowTweak(); Tweaks.Save(); },
            "Remove Planet Glow",
            "tw_pglow",
            "Hides the glow sprite drawn around the planets."
        );
    }
}
