using System.Runtime.CompilerServices;
using Quartz.Modules;
namespace Quartz.Features.KeyViewer;
/// <summary>
/// Every reference to the key-limiter module lives here.
/// </summary>
/// <remarks>
/// The key limiter is a companion, not a dependency: the standalone KeyViewer
/// build ships without it, so the manifest no longer lists it under deps and
/// "Sync Keys to Key Limiter" simply isn't offered when it is absent.
/// <para>
/// The methods that touch its types are <see cref="MethodImplOptions.NoInlining"/>
/// on purpose. Inlined into a caller, the type reference travels with them and
/// the caller then fails to JIT on an install where Quartz.Module.KeyLimiter was
/// never loaded — which is exactly the install this indirection exists for.
/// Keep the type out of the signatures here for the same reason.
/// </para>
/// </remarks>
internal static class KvKeyLimiterBridge {
    internal const string ModuleId = "keylimiter";
    internal static bool Available => ModuleService.Find(ModuleId) is { Loaded: true };
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int[] AllowedKeys() {
        Features.KeyLimiter.KeyLimiter.EnsureConf();
        return Features.KeyLimiter.KeyLimiter.Conf?.AllowedKeys;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void SetAllowedKeys(int[] keys) => Features.KeyLimiter.KeyLimiter.SetAllowedKeys(keys);
}
