#if !IL2CPP
using Facet;
using Quartz.Features.Optimizer;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TweaksPages;
/// <summary>Tweaks ▸ Optimizer in the new menu, row for row as PageOptimizer: every switch applies then saves, except lossy
/// texture compression, which only saves (it takes effect as textures load).</summary>
internal static class OptimizerPage {
    private static OptimizerSettings C => Optimizer.Conf;
    private static readonly OptimizerSettings Def = new();

    internal static void Build(VisualElement p) {
        Optimizer.EnsureConf();
        ShellPage.Feature(p, "optimizer", "SECTION_OPTIMIZER", "OPTIMIZER_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        Row(card, "OPT_SMOOTHGC", () => C.SmoothGC, v => C.SmoothGC = v, Def.SmoothGC);
        Row(card, "OPT_LEAKGUARD", () => C.LeakGuard, v => C.LeakGuard = v, Def.LeakGuard);
        Row(card, "OPT_COLLECTONLOAD", () => C.CollectOnLevelLoad, v => C.CollectOnLevelLoad = v, Def.CollectOnLevelLoad);
        Row(card, "OPT_PRIORITY", () => C.BoostProcessPriority, v => C.BoostProcessPriority = v, Def.BoostProcessPriority);
        Row(card, "OPT_RUNINBG", () => C.RunInBackground, v => C.RunInBackground = v, Def.RunInBackground);
        Row(card, "OPT_LOSSYTEXTURE", () => C.LossyTextureCompression, v => C.LossyTextureCompression = v, Def.LossyTextureCompression, false);
        Row(card, "OPT_FASTBLOOM", () => C.FastBloom, v => C.FastBloom = v, Def.FastBloom);
        Row(card, "OPT_SKIPNOOPFILTERS", () => C.SkipNoOpScreenFilters, v => C.SkipNoOpScreenFilters = v, Def.SkipNoOpScreenFilters);
        Row(card, "OPT_CACHESCREENSCALE", () => C.CacheScreenScale, v => C.CacheScreenScale = v, Def.CacheScreenScale);
        Row(card, "OPT_SKIPIDLEPARTICLES", () => C.SkipIdleParticles, v => C.SkipIdleParticles = v, Def.SkipIdleParticles);
        Row(card, "OPT_PAUSEOFFSCREENPARTICLES", () => C.PauseOffscreenParticles, v => C.PauseOffscreenParticles = v, Def.PauseOffscreenParticles);
        Row(card, "OPT_RENDERHITSOUNDS", () => C.RenderAllHitSounds, v => C.RenderAllHitSounds = v, Def.RenderAllHitSounds);
    }

    // The classic id is the key in lower case; the tooltip is DESC_<key>.
    private static void Row(VisualElement card, string key, Func<bool> get, Action<bool> set, bool def, bool apply = true) =>
        ShellRows.Toggle(card, key, key.ToLowerInvariant(), Bind.To(get, set), apply ? Optimizer.Apply : null, Optimizer.Save, o => {
            o.Default = def;
            o.Tooltip = "DESC_" + key;
        });
}
#endif
