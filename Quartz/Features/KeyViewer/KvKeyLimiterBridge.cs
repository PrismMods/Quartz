using System.Runtime.CompilerServices;
namespace Quartz.Features.KeyViewer;
/// <summary>
/// Every reference to the key-limiter feature lives here.
/// </summary>
/// <remarks>
/// The key limiter is a companion, not a dependency: it can be switched off, and the
/// standalone KeyViewer build never runs it, so "Sync Keys to Key Limiter" is only
/// offered while it is loaded. Check <see cref="Available"/> before any other call.
/// </remarks>
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
