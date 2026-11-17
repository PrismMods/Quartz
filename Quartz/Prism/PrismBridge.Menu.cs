using System.Runtime.CompilerServices;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Prism;
// The new menu's half of the bridge. Same rules as PrismBridge.cs: PrismLib types only inside NoInlining ...Impl methods,
// never in a field; wrappers exist in every flavour, with gated bodies.
internal static partial class PrismBridge {
    /// <summary>
    /// Gives a hotkey window (its Facet surface and window element, as objects) the hotkey-menu look every Prism mod
    /// shares: the wash behind it, no chrome, centred labels; and, where the loaded copy has them, the shared hub buttons
    /// for the window's buttons (in <paramref name="fill"/>, the last in <paramref name="neutral"/>) and the shared logo size
    /// for <paramref name="logo"/> with <paramref name="wordmark"/> under it (<paramref name="buttons"/> says so). False without PrismLib.UI or when the loaded copy predates that look (one
    /// log line): the caller keeps its own look. A copy with the look but not the buttons logs one line too.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool WearHubLook(object surface, object window, object logo, string wordmark, Color fill, Color neutral, out bool buttons) {
        buttons = false;
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(surface == null || !UiAvailable || hubLookGone) return false;
        try {
            if(HubLookImpl()) {
                WearHubLookImpl(surface, window);
                if(HubButtonsImpl()) {
                    DressHubImpl(window, logo, wordmark, fill, neutral);
                    buttons = true;
                } else MainCore.Log.Wrn("PrismLib.UI's hotkey-menu look predates its hub buttons, so Quartz's shortcut menu keeps its own buttons");
                return true;
            }
            HubLookOff("this PrismLib.UI predates it");
        } catch(Exception e) { HubLookOff(e.Message); }
#endif
        return false;
    }
    /// <summary>Dresses a window's hub buttons in new fills, as after an accent change; adds no handlers or logo.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RefillHubButtons(object window, Color fill, Color neutral) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(window == null || !UiAvailable || hubLookGone) return;
        try { RefillHubButtonsImpl(window, fill, neutral); } catch(Exception e) { HubLookOff(e.Message); }
#endif
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
    /// <summary>
    /// The shared hub sizes, for the self-test: button width, height, corner radius and text size, logo size, wordmark
    /// text size; with the wordmark's element name. Null without the hub buttons.
    /// </summary>
    internal static (float[] sizes, string wordmark) HubSizes {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
#if !IL2CPP && !QUARTZ_KEYVIEWER
            if(UiAvailable && !hubLookGone) {
                try { if(HubButtonsImpl()) return (HubSizesImpl(), HubWordmarkNameImpl()); } catch(Exception e) { HubLookOff(e.Message); }
            }
#endif
            return (null, null);
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
    /// <summary>
    /// Lets a pointer click on a surface's root (a VisualElement, as object) leave no focus ring: PrismLib.UI blurs the
    /// clicked button or tab a tick later; text inputs keep their focus. A no-op without PrismLib.UI or on a copy that
    /// predates it (one log line).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void PointerBlurs(object root) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(root == null || !UiAvailable || blurGone) return;
        try { PointerBlursImpl(root); } catch(Exception e) {
            blurGone = true;
            MainCore.Log.Wrn($"PrismLib.UI focus-ring fix unavailable, so a clicked button keeps its ring: {e.Message}");
        }
#endif
    }
    /// <summary>
    /// Hands the key layout editor (a KeyLayoutEditor of KvElement, as object) the switch that flips its wheel pan. A no-op
    /// without PrismLib.UI or on a copy that predates it (one log line): the preview pans the natural way.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void InvertPan(object editor, Func<bool> invert) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(editor == null || !UiAvailable || invertGone) return;
        try { InvertPanImpl(editor, invert); } catch(Exception e) {
            invertGone = true;
            MainCore.Log.Wrn($"PrismLib.UI pan invert unavailable, so Invert scroll panning does nothing: {e.Message}");
        }
#endif
    }
    /// <summary>
    /// Moves the key layout editor's inspector into host (both as object: a KeyLayoutEditor of KvElement and a VisualElement). False without PrismLib.UI or on a copy that
    /// predates it (one log line): the inspector stays under the canvas.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool InspectorIn(object editor, object host) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(editor == null || !UiAvailable || inspectorGone) return false;
        try {
            InspectorInImpl(editor, host);
            return true;
        } catch(Exception e) {
            inspectorGone = true;
            MainCore.Log.Wrn($"PrismLib.UI inspector move unavailable, so the key viewer inspector stays under the preview: {e.Message}");
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
            try { ShowSettingsImpl(); } catch(Exception e) { WindowsOff(e); }
        }
#endif
    }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    private static bool hubLookGone, blurGone, invertGone, inspectorGone;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InspectorInImpl(object editor, object host) =>
        ((PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>)editor).InspectorIn((UnityEngine.UIElements.VisualElement)host);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InvertPanImpl(object editor, Func<bool> invert) =>
        ((PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>)editor).InvertPan = invert;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PointerBlursImpl(object root) =>
        PrismLib.UI.Toolkit.FocusRing.PointerBlurs((UnityEngine.UIElements.VisualElement)root);
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
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float[] HubSizesImpl() => [
        PrismLib.UI.Toolkit.HubLook.ButtonWidth, PrismLib.UI.Toolkit.HubLook.ButtonHeight, PrismLib.UI.Toolkit.HubLook.ButtonRadius,
        PrismLib.UI.Toolkit.HubLook.ButtonTextSize, PrismLib.UI.Toolkit.HubLook.LogoSize, PrismLib.UI.Toolkit.HubLook.WordmarkSize];
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string HubWordmarkNameImpl() => PrismLib.UI.Toolkit.HubLook.WordmarkName;
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HubButtonsImpl() =>
        typeof(PrismLib.UI.Toolkit.HubLook).GetMethod("Buttons", [typeof(UnityEngine.UIElements.VisualElement), typeof(Color), typeof(Color)]) != null;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DressHubImpl(object window, object logo, string wordmark, Color fill, Color neutral) {
        PrismLib.UI.Toolkit.HubLook.Buttons((UnityEngine.UIElements.VisualElement)window, fill, neutral);
        PrismLib.UI.Toolkit.HubLook.Logo((UnityEngine.UIElements.VisualElement)logo, wordmark);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RefillHubButtonsImpl(object window, Color fill, Color neutral) =>
        PrismLib.UI.Toolkit.HubLook.Buttons((UnityEngine.UIElements.VisualElement)window, fill, neutral);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void ShowSettingsImpl() => PrismWindows.ShowSettings();   // see PrismBridge.cs: never name PrismWindows outside an Impl
#endif
}
