using System.Runtime.CompilerServices;
using Quartz.Core;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The only door from Quartz into Facet. Facet ships beside Quartz.dll but can be
/// missing or unloadable; on Mono, JIT-compiling a method that names a Facet type
/// throws when the assembly cannot load. So every public entry here is NoInlining and
/// only calls a separate NoInlining method that names Facet types: the throw then
/// happens inside the try, and callers never touch a Facet type themselves.
/// Add new wrappers in the same shape.
/// </summary>
public static class FacetBridge {
    private static int state; // 0 = not probed, 1 = available, 2 = unavailable
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Available() {
        if(state == 0) state = Probe() ? 1 : 2;
        return state == 1;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Probe() {
#if IL2CPP
        return false; // Facet is not referenced under IL2CPP.
#else
        try {
            return LoadedFacetName() == "Facet.0.1";
        } catch(Exception e) { // optional-dependency boundary: a mismatched Facet can also throw MissingMethod/TypeInitialization
            MainCore.Log.Wrn($"Facet unavailable: {e.Message}");
            return false;
        }
#endif
    }
#if !IL2CPP
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string LoadedFacetName() => typeof(global::Facet.Host).Assembly.GetName().Name;
#endif
}
