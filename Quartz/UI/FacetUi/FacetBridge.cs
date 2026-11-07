using System.Runtime.CompilerServices;
using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The only door from Quartz into Facet. Facet ships beside Quartz.dll but can be
/// missing or unloadable; on Mono, JIT-compiling a method that names a Facet type
/// throws when the assembly cannot load. So every public entry here is NoInlining and
/// only calls a separate NoInlining method that names Facet types: the throw then
/// happens inside the try, and callers never touch a Facet type themselves.
/// Add new wrappers in the same shape. Internal: it stays out of the addon SDK.
/// </summary>
internal static class FacetBridge {
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
        if(Info.KeyViewerOnly) return false;   // that flavour ships no Facet and has no Facet page: nothing to warm or report
        // Prism's UI Toolkit windows support Unity 6 only; an older game keeps the uGUI menu alone.
        if(!IsUnity6(Application.unityVersion)) {
            MainCore.Log.Wrn($"Facet unavailable: Unity {Application.unityVersion} is older than Unity 6");
            return false;
        }
        try {
            return LoadedFacetName() == "Facet.0.2";
        } catch(Exception e) { // optional-dependency boundary: a mismatched Facet can also throw MissingMethod/TypeInitialization
            MainCore.Log.Wrn($"Facet unavailable: {e}");
            return false;
        }
#endif
    }
    // "6000.3.2f1" is Unity 6; "2022.3.62f1" is not.
    internal static bool IsUnity6(string version) {
        if(string.IsNullOrEmpty(version)) return false;
        int dot = version.IndexOf('.');
        return dot > 0 && int.TryParse(version[..dot], out int major) && major >= 6000;
    }
    /// <summary>A Facet text field has keyboard focus. False when Facet is unavailable.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TextFocused() {
#if !IL2CPP
        if(Available()) {
            try { return TextFocusedImpl(); } catch(Exception e) { Fail(e); }
        }
#endif
        return false;
    }
    /// <summary>
    /// The game should not see its keys: a blocking Facet window is open, the key that just closed
    /// one is still down this frame, or the user is typing into a Facet field.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool BlocksGameInput() {
#if !IL2CPP
        if(Available()) {
            try { return BlocksGameInputImpl(); } catch(Exception e) { Fail(e); }
        }
#endif
        return false;
    }
    /// <summary>
    /// Offers this frame's Escape to Facet; true means a Facet window took it. Facet answers once
    /// per frame for every loaded mod, so call it from the Escape poll only, never deferred.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool HandleEscape() {
#if !IL2CPP
        if(Available()) {
            try { return HandleEscapeImpl(); } catch(Exception e) { Fail(e); }
        }
#endif
        return false;
    }
    /// <summary>Opens the Effect Remover page in a Facet window (the preview) in place of the menu.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OpenEffectRemover() {
#if !IL2CPP
        if(Available()) {
            try { OpenEffectRemoverImpl(); } catch(Exception e) { Fail(e); }
        }
#endif
    }
    /// <summary>Hides the Effect Remover window if it is showing (its Hidden brings the menu back); true when it was.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool HideEffectRemover() {
#if !IL2CPP
        if(state == 1) {
            try { return HideEffectRemoverImpl(); } catch(Exception e) { Fail(e); }
        }
#endif
        return false;
    }
    /// <summary>The Effect Remover preview is showing. Never probes, so it is cheap per frame.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool EffectRemoverVisible() {
#if !IL2CPP
        if(state == 1) {
            try { return EffectRemoverVisibleImpl(); } catch(Exception e) { Fail(e); }
        }
#endif
        return false;
    }
    /// <summary>Re-tints Quartz's Facet windows with the accent. No-op until Quartz has made one.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Recolour(Color accent) {
#if !IL2CPP
        if(state == 1) {
            try { RecolourImpl(accent); } catch(Exception e) { Fail(e); }
        }
#endif
    }
    /// <summary>
    /// Draws Facet's throwaway warm-up window half a second from now, so the first window Quartz opens
    /// does not pay the ~85 ms first-draw cost on the click. Facet runs it once per session for every mod.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void WarmUp() {
#if !IL2CPP
        if(Available()) _ = Task.Delay(500).ContinueWith(_ => Quartz.Async.MainThread.Enqueue(WarmUpNow));
#endif
    }
    /// <summary>Scrolls the topmost Facet window. Only after Facet has been probed: no probe per frame.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TickWheel(Vector2 pointer, float delta) {
#if !IL2CPP
        if(state == 1) {
            try { TickWheelImpl(pointer, delta); } catch(Exception e) { Fail(e); }
        }
#endif
    }
    /// <summary>Drops the language hook and disposes Quartz's Facet surfaces. No-op when Facet never ran.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Shutdown() {
#if !IL2CPP
        if(state == 1) {
            try { ShutdownImpl(); } catch(Exception e) { Fail(e); }
        }
#endif
    }
#if !IL2CPP
    // A Facet failure after a successful probe (a mismatched build): report once, stay off.
    private static void Fail(Exception e) {
        if(state == 2) return;
        state = 2;
        MainCore.Log.Wrn($"Facet unavailable: {e}");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TextFocusedImpl() => global::Facet.Input.TextFocused;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool BlocksGameInputImpl() =>
        global::Facet.Input.BlocksGameInput || global::Facet.Input.HeldThisFrame || global::Facet.Input.TextFocused;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HandleEscapeImpl() => global::Facet.Input.HandleEscape();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OpenEffectRemoverImpl() => FacetEffectRemover.Open();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HideEffectRemoverImpl() => FacetEffectRemover.Hide();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EffectRemoverVisibleImpl() => FacetEffectRemover.Surface?.Visible == true;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WarmUpNow() {
        try { WarmUpImpl(); } catch(Exception e) { Fail(e); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WarmUpImpl() => global::Facet.WarmUp.Run();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RecolourImpl(Color accent) => QuartzHost.Recolour(accent);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TickWheelImpl(Vector2 pointer, float delta) {
        global::Facet.Input.TickWheel(pointer, delta);   // Facet counts a notch once per frame across mods
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ShutdownImpl() => QuartzHost.Shutdown();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string LoadedFacetName() => typeof(global::Facet.Host).Assembly.GetName().Name;
#endif
}
