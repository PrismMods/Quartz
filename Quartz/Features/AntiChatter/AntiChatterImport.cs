using Quartz.Interop;
using UnityEngine;
namespace Quartz.Features.AntiChatter;
public sealed class AntiChatterImport : IImportHandler {
    public int Apply(ImportSource source) {
        bool v1 = source.Kind == ImportSourceKind.KorenResourcePackV1;
        int count = 0;
        if(v1 ? source.TryBool("KCBOn", out bool on) : source.TryExtra(ImportKeys.ChatterEnabled, out on)) {
            AntiChatter.EnsureConf();
            AntiChatter.Conf.Enabled = on;
            count++;
        }
        if(TryThreshold(source, v1, out float threshold)) {
            AntiChatter.EnsureConf();
            AntiChatter.Conf.ThresholdMs = Mathf.Max(0f, threshold);
            count++;
        }
        return count;
    }
    private static bool TryThreshold(ImportSource source, bool v1, out float threshold) {
        if(v1) return source.TryFloat("KCBThresholdMs", out threshold);
        bool found = source.TryExtra(ImportKeys.ChatterThresholdMs, out int ms);
        threshold = ms;
        return found;
    }
    public void Refresh() {
        AntiChatter.EnsureConf();
        AntiChatter.Save();
    }
}
