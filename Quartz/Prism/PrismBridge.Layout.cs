using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.UI;
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
        LayoutEditorProbe.Forget(); // the host may build its document now, after the probe's last scan
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
        try { OpenEditorImpl(Available, Id); } catch(Exception e) { LayoutOff(e); }
#endif
    }
    /// <summary>
    /// Closes the shared layout editor: Quartz's own when it shows, else the one on top through Facet's Escape (another mod's).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void CloseLayoutEditor() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(UiAvailable && !layoutOff) {
            try {
                if(CloseEditorImpl()) return;
            } catch(Exception e) { LayoutOff(e); }
        }
#endif
        Quartz.UI.FacetUi.FacetBridge.HandleEscape();
    }
    /// <summary>
    /// The shared layout editor can start on the tab of the mod that opened it: the loaded PrismLib.UI has the filter, and
    /// the editor is Quartz's (it hosts, or nobody does, or there is no PrismLib) or the loaded PrismLib passes Quartz's name
    /// to the mod that hosts it.
    /// </summary>
    internal static bool FilterReady {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
#if IL2CPP || QUARTZ_KEYVIEWER
            return false;
#else
            if(!UiAvailable || layoutOff || !FilterUi) return false;
            if(!Available) return true;
            try { return HostFiltersImpl(); } catch(Exception e) {
                FilterOff("prismlib", e.Message);
                return false;
            }
#endif
        }
    }
    /// <summary>The loaded PrismLib.UI has LayoutEditor.Filter (probed once).</summary>
    internal static bool FilterUi {
        get {
#if IL2CPP || QUARTZ_KEYVIEWER
            return false;
#else
            if(!UiAvailable) return false;
            if(filterUi == 0) filterUi = Probe(FilterUiImpl, "prismlib-ui", "this PrismLib.UI has no LayoutEditor.Filter; the menu hotkey opens the hub") ? 1 : 2;
            return filterUi == 1;
#endif
        }
    }
    /// <summary>The loaded PrismLib passes the opener's name with a layout request (probed once).</summary>
    internal static bool RequestFor {
        get {
#if IL2CPP || QUARTZ_KEYVIEWER
            return false;
#else
            if(!Available) return false;
            if(requestFor == 0) requestFor = Probe(RequestForImpl, "prismlib", "this PrismLib has no Layout.RequestOpen(owner); with another host the menu hotkey opens the hub") ? 1 : 2;
            return requestFor == 1;
#endif
        }
    }
    /// <summary>Why the filter is off: null, "prismlib-ui" or "prismlib", the first miss of the run.</summary>
    internal static string FilterOffReason { get; private set; }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    private static object layoutClaim; // a PrismLib.Claim, as object
    private static int filterUi, requestFor; // 0 not probed, 1 yes, 2 no (or turned off by a throw)
    private static void ProbeFilter() {
        _ = FilterUi;
        if(Available) _ = RequestFor;
    }
    private static bool Probe(Func<bool> probe, string reason, string why) {
        try {
            if(probe()) return true;
            FilterOff(reason, why);
        } catch(Exception e) { FilterOff(reason, e.Message); }
        return false;
    }
    /// <summary>
    /// The library the reason names lacks a filter member or threw: the filter stays off for the run, with one line. The
    /// editor still opens, on All.
    /// </summary>
    internal static void FilterOff(string reason, string why) {
        if(reason == "prismlib-ui") filterUi = 2;
        else requestFor = 2;
        if(FilterOffReason != null) return;
        FilterOffReason = reason;
        MainCore.Log.Wrn($"Quartz's layout filter off ({reason}): {why}");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FilterUiImpl() => typeof(PrismLib.UI.Toolkit.LayoutEditor).GetProperty("Filter") != null;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool RequestForImpl() => typeof(PrismLib.Layout).GetMethod("RequestOpen", [typeof(string)]) != null;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RequestOpenForImpl(string owner) => PrismLib.Layout.RequestOpen(owner);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string RequestedOwnerImpl() => PrismLib.Layout.RequestedOwner;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HostFiltersImpl() => LayoutClaimed || PrismLib.Claims.OwnerOf(PrismLib.StateKey.LayoutEditor) == null || RequestFor;
    // A request that names Quartz, so the host starts on Quartz's tab; false when this PrismLib cannot carry a name.
    private static bool RequestNamed() {
        if(!RequestFor) return false;
        try {
            RequestOpenForImpl(Id);
            return true;
        } catch(Exception e) {
            FilterOff("prismlib", e.Message);
            return false;
        }
    }
    // The mod a request named, for the tab Quartz's editor starts on; null (All) when this PrismLib cannot say.
    private static string RequestedBy() {
        if(!Available || !RequestFor) return null;
        try { return RequestedOwnerImpl(); } catch(Exception e) {
            FilterOff("prismlib", e.Message);
            return null;
        }
    }
    /// <summary>Quartz holds PrismLib's shared layout editor claim: every mod's "Edit layout" opens Quartz's editor.</summary>
    internal static bool LayoutClaimed => layoutClaim != null;
    // The layout half (Quartz's targets in PrismLib's registry and the editor Quartz hosts) fails on its own: one log line,
    // and the windows, the gate and TickAnim carry on.
    private static bool layoutOff;
    private static void LayoutOff(Exception e) {
        layoutOff = true;
        MainCore.Log.Wrn($"Quartz's layout targets off: {e}");
        try { ShutdownLayoutImpl(); } catch(Exception d) { Diag.Ignore(d); }
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
            TickEditorImpl();
        } catch(Exception e) { LayoutOff(e); }
    }
    // QuartzLayout holds a LayoutEditor field: named only in these Impls (see PrismBridge.cs).
    [MethodImpl(MethodImplOptions.NoInlining)] private static void OpenEditorImpl(bool all, string owner) => QuartzLayout.Open(all, owner);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void TickEditorImpl() => QuartzLayout.Tick();
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool CloseEditorImpl() => QuartzLayout.Close();
    [MethodImpl(MethodImplOptions.NoInlining)] private static void ReloadEditorImpl() => QuartzLayout.Reload();
    [MethodImpl(MethodImplOptions.NoInlining)] private static void ShutdownLayoutImpl() => QuartzLayout.Shutdown();
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
        if(!RequestNamed()) PrismLib.Layout.RequestOpen();
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
        LayoutEditorProbe.Forget();
        string owner = RequestedBy(); // read inside the event: the next request overwrites it
        try { OpenEditorImpl(true, owner); } catch(Exception e) { LayoutOff(e); }
    }
    private static void LayoutChanged() {
        if(layoutOff) return;
        try { ReloadEditorImpl(); } catch(Exception e) { LayoutOff(e); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnregisterLayout() {
        PrismLib.Layout.OpenRequested -= LayoutRequested;
        PrismLib.Layout.Changed -= LayoutChanged;
        PrismLib.Layout.Unregister(Id);
    }
#endif
}
