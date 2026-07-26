using Quartz.Interop;
namespace Quartz.Features.Tweaks;
public sealed class TweaksImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.EnhancedEffectRemover => ApplyEnhanced(source),
        ImportSourceKind.AdofaiTweaks => ApplyAdofaiTweaks(source),
        _ => 0,
    };
    private static int ApplyV1(ImportSource source) {
        Tweaks.EnsureConf();
        TweaksSettings c = Tweaks.Conf;
        int count = 0;
        void Flag(string name, Action<bool> set) {
            if(!source.TryBool(name, out bool value)) return;
            set(value);
            count++;
        }
        Flag("RemoveAllCheckpoints", v => c.RemoveAllCheckpoints = v);
        Flag("RemoveBallCoreParticles", v => c.RemoveBallCoreParticles = v);
        Flag("DisableTileHitGlow", v => c.DisableTileHitGlow = v);
        Flag("RemovePlanetGlow", v => c.RemovePlanetGlow = v);
        Flag("DisableAutoPause", v => c.DisableAutoPause = v);
        Flag("BlockMouseWheelScrollWhilePlaying", v => c.BlockMouseWheelScrollWhilePlaying = v);
        return count;
    }
    private static int ApplyAdofaiTweaks(ImportSource source) {
        if(!source.TryExtra(ImportKeys.TweaksBlockScroll, out bool block)) return 0;
        Tweaks.EnsureConf();
        Tweaks.Conf.BlockMouseWheelScrollWhilePlaying = block;
        return 1;
    }
    private static int ApplyEnhanced(ImportSource source) {
        if(!source.TryBool("CheckPoints", out bool checkpoints)) return 0;
        Tweaks.EnsureConf();
        Tweaks.Conf.RemoveAllCheckpoints = checkpoints;
        return 1;
    }
    public void Refresh() {
        Tweaks.EnsureConf();
        Tweaks.RefreshAll();
        Tweaks.Save();
    }
}
