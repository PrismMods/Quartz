using Quartz.Interop;
namespace Quartz.Features.VisualTweaks;
public sealed class VisualTweaksImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.EnhancedEffectRemover => ApplyEnhanced(source),
        _ => 0,
    };
    private static int ApplyV1(ImportSource source) {
        VisualTweaks.EnsureConf();
        VisualTweaksSettings c = VisualTweaks.Conf;
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
        return count;
    }
    private static int ApplyEnhanced(ImportSource source) {
        if(!source.TryBool("CheckPoints", out bool checkpoints)) return 0;
        VisualTweaks.EnsureConf();
        VisualTweaks.Conf.RemoveAllCheckpoints = checkpoints;
        return 1;
    }
    public void Refresh() {
        VisualTweaks.EnsureConf();
        VisualTweaks.RefreshAll();
        VisualTweaks.Save();
    }
}
