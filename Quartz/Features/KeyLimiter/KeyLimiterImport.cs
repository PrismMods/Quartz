using Quartz.Interop;
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
        return count;
    }
    private static int ApplyV1(ImportSource source) {
        int count = 0;
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
        KeyLimiter.EnsureConf();
        KeyLimiter.Save();
    }
}
