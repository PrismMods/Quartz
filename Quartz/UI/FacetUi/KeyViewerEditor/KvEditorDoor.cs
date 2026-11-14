#if !IL2CPP
using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.Prism;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
/// <summary>
/// The only way into the key viewer editor, which is PrismLib.UI's: Quartz starts without PrismLib.UI, and another mod's
/// older copy may be the one loaded and lack the editor's types. So no PrismLib.UI type appears in a field or signature
/// here; every entry checks Usable and calls a NoInlining ...Impl inside a try. A missing or mismatched PrismLib.UI turns
/// the door off for the session with one log line; any other throw is logged (rate-limited) and keeps it.
/// </summary>
internal static class KvEditorDoor {
#if !QUARTZ_KEYVIEWER
    private static VisualElement editorRoot;
    private static bool gone, probed, switcherMade;
    private static float nextLog;
#endif
#if DEBUG && !QUARTZ_KEYVIEWER
    /// <summary>The self-test's way to see the fallback card.</summary>
    internal static bool ForceGone;
#endif

    /// <summary>What Build, OpenSwitcher, ToggleSwitcher and the hub read. False in the KeyViewer flavour.</summary>
    internal static bool Usable {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
#if QUARTZ_KEYVIEWER
            return false;
#else
#if DEBUG
            if(ForceGone) return false;
#endif
            if(gone || !PrismBridge.UiAvailable) return false;
            if(!probed) {
                probed = true;   // before the probe: a load failure sets gone, which no later read retries
                try { ProbeImpl(); } catch(Exception e) { Failed(e); }
            }
            return !gone;
#endif
        }
    }

    /// <summary>An editor is built and on a panel.</summary>
#if QUARTZ_KEYVIEWER
    internal static bool Live => false;
#else
    internal static bool Live => editorRoot?.panel != null;
#endif

    /// <summary>Builds the editor into right; false when PrismLib.UI is unusable or the build failed (right is then empty),
    /// and the caller draws the fallback card.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Build(VisualElement right) {
#if !QUARTZ_KEYVIEWER
        if(!Usable) return false;
        try {
            editorRoot = BuildImpl(right);
            return true;
        } catch(Exception e) {
            Failed(e);
            editorRoot = null;
            right.Clear();   // a half-built editor must not sit over the card
        }
#endif
        return false;
    }

    /// <summary>Per frame, through FacetBridge.TickKvEditor: nothing unless an editor is live and the menu shows (a hidden
    /// menu keeps its panel, so Live alone would tick it through every level). A change made while hidden is seen on the
    /// first shown frame.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Tick() {
#if !QUARTZ_KEYVIEWER
        if(gone || !Live || QuartzShell.Surface?.Visible != true) return;
        // A capture owns the keyboard, and on the frame it ends its key is still down.
        bool keys = !(Keybind.Capturing || KeyCaptureService.IsCapturing || KeyCaptureCoordinator.ReleasedThisFrame || FacetBridge.TextFocused());
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        try { TickImpl(ctrl, shift, alt, keys); } catch(Exception e) { Failed(e); }
#endif
    }

    /// <summary>Opens the layout switcher.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void OpenSwitcher() {
#if !QUARTZ_KEYVIEWER
        if(!Usable) return;
        try {
            switcherMade = true;
            OpenSwitcherImpl();
        } catch(Exception e) { Failed(e); }
#endif
    }

    /// <summary>Opens the layout switcher, or hides it when it shows.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ToggleSwitcher() {
#if !QUARTZ_KEYVIEWER
        if(!Usable) return;
        try {
            switcherMade = true;
            ToggleSwitcherImpl();
        } catch(Exception e) { Failed(e); }
#endif
    }

    /// <summary>Hides the layout switcher; nothing when it was never opened.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void HideSwitcher() {
#if !QUARTZ_KEYVIEWER
        if(!switcherMade) return;
        try { HideSwitcherImpl(); } catch(Exception e) { Failed(e); }
#endif
    }

    /// <summary>The layout switcher shows.</summary>
    internal static bool SwitcherVisible {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
#if !QUARTZ_KEYVIEWER
            if(!switcherMade) return false;
            try { return SwitcherVisibleImpl(); } catch(Exception e) { Failed(e); }
#endif
            return false;
        }
    }

    /// <summary>Quartz is unloading: the switcher is disposed and the editor's statics let go, so no stale window or model
    /// outlives it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Shutdown() {
#if !QUARTZ_KEYVIEWER
        if(!switcherMade && editorRoot == null) return;
        switcherMade = false;
        editorRoot = null;
        try { ShutdownImpl(); } catch(Exception e) { Failed(e); }
#endif
    }

#if !QUARTZ_KEYVIEWER
    // A missing or older PrismLib.UI throws one of these from the JIT or the loader; anything else is a bug in a build.
    private static void Failed(Exception e) {
        Exception cause = e is TypeInitializationException { InnerException: { } inner } ? inner : e;
        if(cause is TypeLoadException or MissingMemberException or FileNotFoundException or BadImageFormatException) {
            if(gone) return;
            gone = true;
            MainCore.Log.Wrn($"Key viewer editor unavailable: {e}");
            return;
        }
        if(Time.unscaledTime < nextLog) {
            Diag.Ignore(e);   // logged within the last few seconds
            return;
        }
        nextLog = Time.unscaledTime + 5f;
        MainCore.Log.Wrn($"Key viewer editor error: {e}");
    }

    private static readonly Func<KeyCode, bool> down = Input.GetKeyDown, held = Input.GetKey;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeImpl() => _ = PrismLib.UI.Toolkit.KeyLayoutText.Ids.Count;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static VisualElement BuildImpl(VisualElement right) => KvEditorPage.Build(right);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OpenSwitcherImpl() => KvEditorPage.OpenSwitcher();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ToggleSwitcherImpl() => KvEditorPage.ToggleSwitcher();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void HideSwitcherImpl() => KvEditorPage.Switcher?.Hide();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool SwitcherVisibleImpl() => KvEditorPage.Switcher?.Visible == true;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ShutdownImpl() => KvEditorPage.Shutdown();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TickImpl(bool ctrl, bool shift, bool alt, bool keys) => KvEditorPage.Tick(down, held, ctrl, shift, alt, keys);
#endif
}
#endif
