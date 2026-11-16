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
    /// <summary>
    /// Gives a hotkey window (its Facet surface and window element, as objects) the hotkey-menu look every Prism mod
    /// shares: the wash behind it, no chrome, centred labels. False without PrismLib.UI or when the loaded copy predates
    /// that look (one log line): the caller keeps its own look.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool WearHubLook(object surface, object window) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(surface == null || !UiAvailable || hubLookGone) return false;
        try {
            if(HubLookImpl()) {
                WearHubLookImpl(surface, window);
                return true;
            }
            HubLookOff("this PrismLib.UI predates it");
        } catch(Exception e) { HubLookOff(e.Message); }
#endif
        return false;
    }
    /// <summary>Fades a surface that wears the hotkey-menu look in, as it shows.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void FadeHubIn(object surface) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(surface == null || !UiAvailable || hubLookGone) return;
        try { FadeHubInImpl(surface); } catch(Exception e) { HubLookOff(e.Message); }
#endif
    }
#if DEBUG
    /// <summary>The shared wash colour's alpha, for the self-test; NaN without the look.</summary>
    internal static float HubWashAlpha {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
#if !IL2CPP && !QUARTZ_KEYVIEWER
            if(UiAvailable && !hubLookGone) {
                try { return HubWashAlphaImpl(); } catch(Exception e) { HubLookOff(e.Message); }
            }
#endif
            return float.NaN;
        }
    }
    /// <summary>The shared wash's element name, for the self-test; null without the look.</summary>
    internal static string HubWashName {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
#if !IL2CPP && !QUARTZ_KEYVIEWER
            if(UiAvailable && !hubLookGone) {
                try { return HubWashNameImpl(); } catch(Exception e) { HubLookOff(e.Message); }
            }
#endif
            return null;
        }
    }
#endif
    /// <summary>Shows Prism · Settings, Quartz's own copy whoever holds the debug claim; one already showing stays.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void OpenPrismSettings() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(UiAvailable && !winOff) {
            hosted = true;
            try { ShowSettingsImpl(); } catch(Exception e) { WindowsOff(e); }
        }
#endif
    }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    private static bool suiteGone, hubLookGone;
    private static void HubLookOff(string why) {
        hubLookGone = true;
        MainCore.Log.Wrn($"PrismLib.UI hotkey-menu look unavailable, so Quartz's shortcut menu keeps its card: {why}");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HubLookImpl() => typeof(PrismLib.UI.Toolkit.Tokens).Assembly.GetType("PrismLib.UI.Toolkit.HubLook") != null;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WearHubLookImpl(object surface, object window) =>
        PrismLib.UI.Toolkit.HubLook.Wear((Facet.Surface)surface, (UnityEngine.UIElements.VisualElement)window);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FadeHubInImpl(object surface) => PrismLib.UI.Toolkit.HubLook.FadeIn((Facet.Surface)surface);
#if DEBUG
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float HubWashAlphaImpl() => PrismLib.UI.Toolkit.Tokens.Wash.a;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string HubWashNameImpl() => PrismLib.UI.Toolkit.HubLook.WashName;
#endif
    [MethodImpl(MethodImplOptions.NoInlining)] private static void ShowSettingsImpl() => PrismWindows.ShowSettings();   // see PrismBridge.cs: never name PrismWindows outside an Impl
    // The one place outside FacetUi that names Facet types: reached only past UiAvailable, which means Facet loaded.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool WearSuiteImpl(object theme) {
        PrismLib.UI.Toolkit.Suite.Wear((Facet.Theme)theme);
        return true;
    }
#endif
}
