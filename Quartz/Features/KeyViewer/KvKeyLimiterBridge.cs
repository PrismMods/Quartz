using System.Runtime.CompilerServices;
namespace Quartz.Features.KeyViewer;
// The only file that touches the key limiter. It can be off or absent (KeyViewer flavour): check Available first.
internal static class KvKeyLimiterBridge {
    internal const string ModuleId = "keylimiter";
    internal static bool Available => FeatureService.Find(ModuleId) is { Loaded: true };
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int[] AllowedKeys() {
        Features.KeyLimiter.KeyLimiter.EnsureConf();
        return Features.KeyLimiter.KeyLimiter.Conf?.AllowedKeys;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void SetAllowedKeys(int[] keys) => Features.KeyLimiter.KeyLimiter.SetAllowedKeys(keys);
}
