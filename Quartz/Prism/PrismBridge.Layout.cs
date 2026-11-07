using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.UI.Utility;
namespace Quartz.Prism;
// The layout half of the bridge: Quartz's elements in PrismLib's layout registry, and the shared layout editor when Quartz
// hosts it (QuartzLayout). Same rules as PrismBridge.cs: PrismLib types only inside NoInlining ...Impl methods, never in a field.
internal static partial class PrismBridge {
    /// <summary>
    /// "Edit layout": PrismLib's shared layout editor, so Quartz's overlays and every other Prism mod's elements move in one
    /// session. Asks the mod that hosts it, or opens it here (with Quartz's own elements only when PrismLib is absent).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void OpenLayoutEditor() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(Available && layoutClaim == null) {
            try {
                if(RequestLayoutImpl()) return;
            } catch(Exception e) { MainCore.Log.Wrn($"PrismLib layout editor request failed: {e.Message}"); }
        }
        if(!UiAvailable || layoutOff) {
            MainCore.Log.Msg("Layout editor unavailable");
            return;
        }
        hosted = true;
        try { QuartzLayout.Open(all: Available); } catch(Exception e) { LayoutOff(e); }
#endif
    }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    private static object layoutClaim; // a PrismLib.Claim, as object
    /// <summary>Quartz holds PrismLib's shared layout editor claim: every mod's "Edit layout" opens Quartz's editor.</summary>
    internal static bool LayoutClaimed => layoutClaim != null;
    // The layout half (Quartz's targets in PrismLib's registry and the editor Quartz hosts) fails on its own: one log line,
    // and the windows, the gate and TickAnim carry on.
    private static bool layoutOff;
    private static void LayoutOff(Exception e) {
        layoutOff = true;
        MainCore.Log.Wrn($"Quartz's layout targets off: {e}");
        try { QuartzLayout.Shutdown(); } catch(Exception d) { Diag.Ignore(d); }
        try { if(Available) ReleaseLayoutClaimImpl(); } catch(Exception d) { Diag.Ignore(d); }
    }
    private static int described = int.MinValue;
    private static string describedIn;
    /// <summary>
    /// What the targets' labels read in: the language, or "loading" while the table loads (Tr answers in English then; at
    /// startup it loads after the first describe). A change means the labels need reading again.
    /// </summary>
    internal static string LabelState => MainCore.Tr.IsLoading ? "loading" : MainCore.Tr.Language;
    // Quartz's targets go to PrismLib's registry again when a handle is tagged or destroyed, or the labels' state changes.
    private static void TickLayout() {
        if(layoutOff) return;
        try {
            int version = ReorganizeHandle.LayoutVersion;
            string labels = LabelState;
            if(Available && (version != described || describedIn != labels)) {
                described = version;
                describedIn = labels;
                DescribeLayoutImpl();
            }
            QuartzLayout.Tick();
        } catch(Exception e) { LayoutOff(e); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DescribeLayoutImpl() => ((PrismLib.ModHandle)handle).DescribeLayout(QuartzLayout.LayoutTargets());
    // One mod draws the shared layout editor; the claim says which, and the others ask it through Layout.RequestOpen.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClaimLayoutImpl() {
        layoutClaim = ((PrismLib.ModHandle)handle).ClaimState(PrismLib.StateKey.LayoutEditor, "shared layout editor");
        if(layoutClaim == null) return;
        PrismLib.Layout.OpenRequested -= LayoutRequested; // never twice
        PrismLib.Layout.OpenRequested += LayoutRequested;
        PrismLib.Layout.Changed -= LayoutChanged;
        PrismLib.Layout.Changed += LayoutChanged;
    }
    // Another mod hosts the editor: ask it. Nobody does: take the claim, so this and every later request opens Quartz's.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool RequestLayoutImpl() {
        if(PrismLib.Claims.OwnerOf(PrismLib.StateKey.LayoutEditor) == null && UiAvailable && !layoutOff) ClaimLayoutImpl();
        if(layoutClaim != null) return false;
        PrismLib.Layout.RequestOpen();
        return true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RefreshLayoutClaimImpl() {
        string owner = PrismLib.Claims.OwnerOf(PrismLib.StateKey.LayoutEditor);
        if(owner == Id) return;
        if(layoutClaim != null) ReleaseLayoutClaimImpl(); // lost it (released by someone): stop answering requests
        if(owner == null) ClaimLayoutImpl();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ReleaseLayoutClaimImpl() {
        (layoutClaim as PrismLib.Claim)?.Release();
        layoutClaim = null;
        PrismLib.Layout.OpenRequested -= LayoutRequested;
        PrismLib.Layout.Changed -= LayoutChanged;
    }
    private static void LayoutRequested() {
        if(layoutClaim == null || layoutOff) return;
        hosted = true;
        try { QuartzLayout.Open(all: true); } catch(Exception e) { LayoutOff(e); }
    }
    private static void LayoutChanged() {
        if(layoutOff) return;
        try { QuartzLayout.Reload(); } catch(Exception e) { LayoutOff(e); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnregisterLayout() {
        PrismLib.Layout.OpenRequested -= LayoutRequested;
        PrismLib.Layout.Changed -= LayoutChanged;
        PrismLib.Layout.Unregister(Id);
    }
#endif
}
