using Quartz.Interop;
using UnityEngine;
namespace Quartz.Features.KeyLimiter;
public sealed class KeyLimiterImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        _ => ApplyShaped(source),
    };
    private static int ApplyShaped(ImportSource source) {
        int count = 0;
        if(source.TryExtra(ImportKeys.KeyLimiterEnabled, out bool limiterOn)) {
            KeyLimiter.EnsureConf();
            KeyLimiter.Conf.Enabled = limiterOn;
            count++;
        }
        if(source.TryExtra(ImportKeys.KeyLimiterAllowedKeys, out int[] keys) && keys is { Length: > 0 }) {
            KeyLimiter.SetAllowedKeys(keys);
            count++;
        }
        if(source.TryExtra(ImportKeys.ChatterThresholdMs, out int threshold)) {
            ChatterBlocker.ChatterBlocker.EnsureConf();
            ChatterBlocker.ChatterBlocker.Conf.ThresholdMs = Mathf.Max(0, threshold);
            count++;
        }
        if(source.TryExtra(ImportKeys.ChatterEnabled, out bool chatterOn)) {
            ChatterBlocker.ChatterBlocker.EnsureConf();
            ChatterBlocker.ChatterBlocker.Conf.Enabled = chatterOn;
            count++;
        }
        return count;
    }
    private static int ApplyV1(ImportSource source) {
        int count = 0;
        if(source.TryBool("KCBOn", out bool chatterOn)) {
            ChatterBlocker.ChatterBlocker.EnsureConf();
            ChatterBlocker.ChatterBlocker.Conf.Enabled = chatterOn;
            count++;
        }
        if(source.TryFloat("KCBThresholdMs", out float threshold)) {
            ChatterBlocker.ChatterBlocker.EnsureConf();
            ChatterBlocker.ChatterBlocker.Conf.ThresholdMs = Mathf.Max(0f, threshold);
            count++;
        }
        if(source.TryBool("KeyLimiterOn", out bool limiterOn)) {
            KeyLimiter.EnsureConf();
            KeyLimiter.Conf.Enabled = limiterOn;
            count++;
        }
        int[] keys = source.Keys("KeyLimiterAllowed");
        if(keys is { Length: > 0 }) {
            KeyLimiter.SetAllowedKeys(keys);
            count++;
        }
        return count;
    }
    public void Refresh() {
        ChatterBlocker.ChatterBlocker.EnsureConf();
        ChatterBlocker.ChatterBlocker.Save();
        KeyLimiter.EnsureConf();
        KeyLimiter.Save();
    }
}
