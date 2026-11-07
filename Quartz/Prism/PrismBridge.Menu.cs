using System.Runtime.CompilerServices;
using Quartz.Core;
namespace Quartz.Prism;
// The new menu's half of the bridge. Same rules as PrismBridge.cs: PrismLib types only inside NoInlining ...Impl methods,
// never in a field; wrappers exist in every flavour, with gated bodies.
internal static partial class PrismBridge {
    /// <summary>
    /// Dresses a Facet theme (passed as object so no Facet type shows here) in the Prism suite look. False without
    /// PrismLib.UI, for a null theme, or when this PrismLib.UI predates the skin: the caller keeps Facet's own look.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool WearSuite(object facetTheme) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(facetTheme == null || !UiAvailable || suiteGone) return false;
        try { return WearSuiteImpl(facetTheme); } catch(Exception e) {
            suiteGone = true;
            MainCore.Log.Wrn($"PrismLib.UI suite skin unavailable: {e}");
        }
#endif
        return false;
    }
    /// <summary>Shows Prism · Settings, Quartz's own copy whoever holds the debug claim; one already showing stays.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void OpenPrismSettings() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(UiAvailable && !winOff) {
            hosted = true;
            try { PrismWindows.ShowSettings(); } catch(Exception e) { WindowsOff(e); }
        }
#endif
    }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    private static bool suiteGone;
    // The one place outside FacetUi that names a Facet type: reached only past UiAvailable, which means Facet loaded.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool WearSuiteImpl(object theme) {
        PrismLib.UI.Toolkit.Suite.Wear((Facet.Theme)theme);
        return true;
    }
#endif
}
