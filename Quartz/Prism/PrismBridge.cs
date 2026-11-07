using System.Runtime.CompilerServices;
using Quartz.Compat;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.UI;
using Quartz.UI.FacetUi;
using UnityEngine;
namespace Quartz.Prism;
/// <summary>
/// The only door from Quartz into PrismLib (shared; the bootstrap installs it from the store's
/// PrismLib.bundle) and PrismLib.UI (shipped in the store, optional: absent under IL2CPP, in the
/// KeyViewer flavour, before Unity 6 or when its Facet is missing). FacetBridge's shape: every entry is
/// NoInlining, checks its half, and calls a NoInlining ...Impl inside a try; a failure turns that half
/// off with one log line. No PrismLib or PrismLib.UI type in a field or a signature here, and no file
/// outside Quartz/Prism names one: the CLR resolves field types when a class loads and a method's types
/// when it is JITted, before any try can catch. PrismLib's tools/check-bridge.sh pins this.
/// </summary>
internal static partial class PrismBridge {
    internal const string Id = "Quartz";
#if QUARTZ_UMM
    internal const string Loader = "UMM";
#else
    internal const string Loader = "ML";
#endif
    /// <summary>PrismLib.dll is loaded and Quartz is wired to it.</summary>
    internal static bool Available { get; private set; }
    private static int ui; // 0 = not probed, 1 = available, 2 = unavailable
    /// <summary>Facet and PrismLib.UI both load. Probed once.</summary>
    internal static bool UiAvailable {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
            if(ui == 0) ui = ProbeUi() ? 1 : 2;
            return ui == 1;
        }
    }
    /// <summary>Loads PrismLib through the vendored bootstrap. Runs before anything loads PrismLib.UI or Facet.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Init(IQuartzHost host) {
#if IL2CPP || QUARTZ_KEYVIEWER
        MainCore.Log.Msg("PrismLib off: this build ships no PrismLib");
#else
        if(!FacetBridge.IsUnity6(Application.unityVersion)) {
            MainCore.Log.Msg($"PrismLib off: Unity {Application.unityVersion} is older than Unity 6");
            return;
        }
        try {
            loaded = Available = EnsureImpl(host);
            if(!loaded) MainCore.Log.Wrn($"PrismLib unavailable (below {PrismFloor.PrismLib} or missing)");
        } catch(Exception e) { LibOff(e); }
        ForeignLib = !loaded && AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "PrismLib");
#endif
    }
    /// <summary>Another mod loaded a PrismLib Quartz cannot use (older than its floor): that mod's holder answers the Prism chords.</summary>
    internal static bool ForeignLib { get; private set; }
    /// <summary>Registers Quartz with PrismLib and its menu with the input gate. After the services exist.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Register() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(Available) {
            try { RegisterImpl(); } catch(Exception e) {
                LibOff(e);
                try { UnregisterImpl(); } catch(Exception u) { Diag.Ignore(u); } // drop what registered before the throw
            }
        }
        if(UiAvailable) {
            try { UiWiringImpl(); } catch(Exception e) { UiOff(e); }
            if(Available && ui == 1) { // a mod that holds a claim must draw what it claims
                try { ClaimDebugImpl(); } catch(Exception e) { MainCore.Log.Wrn($"PrismLib debug claim failed: {e.Message}"); }
                try { ClaimLayoutImpl(); } catch(Exception e) { LayoutOff(e); }
            }
        }
#endif
    }
    /// <summary>Alt+D: Quartz's debug window, or a request to the mod that owns the shared one.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ToggleDebug() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(UiAvailable && !winOff) {
            hosted = true;
            try { ToggleDebugImpl(); } catch(Exception e) { WindowsOff(e); }
        }
#endif
    }
    /// <summary>
    /// Alt+P: Prism · Settings. Only the debug-claim owner polls the chord, so it opens one copy; this entry (a button)
    /// opens Quartz's own whoever owns the claim, because PrismLib has no request event for settings.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void TogglePrismSettings() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(UiAvailable && !winOff) {
            hosted = true;
            try { ToggleSettingsImpl(); } catch(Exception e) { WindowsOff(e); }
        }
#endif
    }
    /// <summary>Asks the mod that owns the shared debug window to toggle it. PrismLib only.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RequestToggle() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(Available) RequestToggleImpl();
#endif
    }
    /// <summary>Quartz holds PrismLib's shared debug window claim, so only Quartz draws it.</summary>
    internal static bool DebugClaimed {
        get {
#if !IL2CPP && !QUARTZ_KEYVIEWER
            return debugClaim != null;
#else
            return false;
#endif
        }
    }
    /// <summary>Re-tints Prism windows with Quartz's accent. Reads the probe's result, never starts it: runs at startup.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Recolour(Color accent) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(ui != 1 || winOff) return;
        try { RecolourImpl(accent); } catch(Exception e) { WindowsOff(e); }
#endif
    }
    /// <summary>
    /// MUST be called from every layout target's GetBox that Quartz hands PrismLib: it is the only signal Quartz has that the
    /// shared layout editor is open (the editor reads every box each frame it is open). Without it overlays never show for
    /// arranging and the reorganizer is never stood down.
    /// </summary>
    internal static void TouchLayout() => layout.Touch(Time.frameCount);
    private static readonly PrismRules.LayoutSession layout = new();
    /// <summary>Per frame, after Quartz's own UI update.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Tick() {
        try { UICore.SetSharedLayout(layout.Open(Time.frameCount)); } catch(Exception e) { MainCore.Log.Wrn($"Shared layout state failed: {e.Message}"); } // no PrismLib type: runs without PrismLib too, and then never opens
#if !IL2CPP && !QUARTZ_KEYVIEWER
        TickAnim();
        if(UiAvailable) {
            try { TickImpl(); } catch(Exception e) { UiOff(e); }
        }
#endif
    }
    /// <summary>
    /// Runs <paramref name="reads"/> exactly once, past the input gate when PrismLib.UI is there. An
    /// exception from <paramref name="reads"/> reaches the caller untouched and never turns PrismLib.UI off.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Exempt(Action reads) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(UiAvailable) {
            pending = reads;
            started = false;
            try {
                ExemptImpl(RunPending);
            } catch(Exception e) when(!started) {
                UiOff(e);
            } finally {
                pending = null;
            }
            if(started) return;
        }
#endif
        reads();
    }
    /// <summary>The window server's state of a key (macOS), past every gate. False when there is no answer.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool TryPhysical(KeyCode key, out bool held) {
        held = false;
#if !IL2CPP && !QUARTZ_KEYVIEWER
        // MacKeys is newer than a PrismLib.UI another mod may have loaded: a miss turns off only this path.
        if(UiAvailable && !macGone) {
            try { return TryPhysicalImpl(key, out held); } catch(Exception e) {
                held = false;
                macGone = true;
                MainCore.Log.Wrn($"PrismLib.UI MacKeys unavailable: {e}");
            }
        }
#endif
        return false;
    }
    /// <summary>Hands the input gate back and takes Quartz out of every PrismLib registry.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Shutdown() {
        try { UICore.SetSharedLayout(false); } catch(Exception e) { Diag.Ignore(e); } // never leave a session stuck open
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(hosted) { // whatever turned a half off: what ran is cleaned up
            try { ShutdownWindowsImpl(); } catch(Exception e) { MainCore.Log.Wrn($"Prism windows shutdown failed: {e.Message}"); } // writes geometry: before the settings save
            try { PrismGate.Shutdown(); } catch(Exception e) { MainCore.Log.Wrn($"Prism gate shutdown failed: {e.Message}"); }
            try { ShutdownLayoutImpl(); } catch(Exception e) { MainCore.Log.Wrn($"Layout editor shutdown failed: {e.Message}"); }
            try { UnwireImpl(); } catch(Exception e) { MainCore.Log.Wrn($"Prism hooks unwire failed: {e.Message}"); } // PrismLib.UI's shared hooks
        }
        if(!loaded) return;
        loaded = Available = false;
        try { UnregisterImpl(); } catch(Exception e) { MainCore.Log.Wrn($"PrismLib unregister failed: {e}"); }
#endif
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProbeUi() {
#if IL2CPP || QUARTZ_KEYVIEWER
        return false;
#else
        if(!FacetBridge.Available()) {
            MainCore.Log.Wrn("PrismLib.UI unavailable: Facet is unavailable");
            return false;
        }
        try {
            return UiName() == "PrismLib.UI";
        } catch(Exception e) {
            MainCore.Log.Wrn($"PrismLib.UI unavailable: {e}");
            return false;
        }
#endif
    }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    private static bool winOff, hosted; // winOff: window hosting failed, only the windows go (the gate and TickAnim carry on); hosted: the gate or a window has run
#if DEBUG
    internal static void ResetWindowsOff() => winOff = false;
#endif
    private static object handle, debugClaim; // PrismLib objects as object: no PrismLib type in a field
    private static bool loaded; // PrismLib.dll is loaded: Shutdown unregisters even when wiring failed
    private static void LibOff(Exception e) {
        Available = false;
        MainCore.Log.Wrn($"PrismLib unavailable: {e}");
    }
    // A failure inside window hosting costs only the windows and the debug claim, never the gate or TickAnim.
    private static void WindowsOff(Exception e) {
        winOff = true;
        MainCore.Log.Wrn($"Prism windows off: {e}");
        try { ShutdownWindowsImpl(); } catch(Exception d) { Diag.Ignore(d); }
        try { if(Available) ReleaseDebugClaimImpl(); } catch(Exception d) { Diag.Ignore(d); }
    }
    private static void TickWindows() {
        if(winOff) return;
        try { TickWindowsImpl(); } catch(Exception e) { WindowsOff(e); }
    }
    /* PrismWindows and QuartzLayout hold PrismLib.UI types in fields, and Mono lays out a class's fields when it compiles
       a call into it: a method naming either throws TypeLoadException before its first line runs when PrismLib.UI is
       missing or older. So only these name them, each reached past a check and inside a try. */
    [MethodImpl(MethodImplOptions.NoInlining)] private static void ToggleDebugImpl() => PrismWindows.ToggleDebug();
    [MethodImpl(MethodImplOptions.NoInlining)] private static void ToggleSettingsImpl() => PrismWindows.ToggleSettings();
    [MethodImpl(MethodImplOptions.NoInlining)] private static void RecolourImpl(Color accent) => PrismWindows.Recolour(accent);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void TickWindowsImpl() => PrismWindows.Tick();
    [MethodImpl(MethodImplOptions.NoInlining)] private static void ShutdownWindowsImpl() => PrismWindows.Shutdown();
    [MethodImpl(MethodImplOptions.NoInlining)] private static void UnwireImpl() => PrismWindows.Unwire();
    /// <summary>Once a second: takes the debug and layout claims when they are free (their holder unloaded), notes them when lost.</summary>
    internal static void RefreshClaim() {
        if(!Available) return;
        RefreshClaimImpl();
        if(layoutOff || !UiAvailable) return;
        try { RefreshLayoutClaimImpl(); } catch(Exception e) { LayoutOff(e); }
    }
    // Without PrismLib.UI Quartz draws neither shared window: its claims go, so another mod's retry takes them.
    private static void UiOff(Exception e) {
        ui = 2;
        MainCore.Log.Wrn($"PrismLib.UI unavailable: {e}");
        if(!Available) return;
        try { ReleaseDebugClaimImpl(); } catch(Exception d) { Diag.Ignore(d); }
        try { ReleaseLayoutClaimImpl(); } catch(Exception d) { Diag.Ignore(d); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string UiName() => typeof(PrismLib.UI.Toolkit.InputGate).Assembly.GetName().Name;
    // The store folder holding Quartz.dll (PayloadLoader loads it from there).
    internal static string StoreDir(IQuartzHost host) =>
        PrismRules.StoreDir(typeof(PrismBridge).Assembly.Location, Loader == "UMM", host.ModsPath, host.QuartzFilePath, Info.DisplayVersion);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EnsureImpl(IQuartzHost host) {
        string bundle = Path.Combine(StoreDir(host), PrismLib.Bootstrap.PrismBootstrap.BundleName);
        return PrismLib.Bootstrap.PrismBootstrap.Ensure(m => MainCore.Log.Msg(m), PrismFloor.PrismLib,
            PrismRules.ModsRoot(host.ModsPath, umm: Loader == "UMM"), bundle);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterImpl() {
        PrismLib.ModHandle me = PrismLib.Prism.Register(Id, MainCore.Version);
        handle = me;
        PrismLib.Keys.KeyName = i => ((KeyCode)i).ToString();
        // English names: the tab text is translated where it is drawn (PrismRules.Translate), so it follows the language.
        me.AddLog(PrismRules.DebugLog, QuartzLogger.Recent, null, QuartzLogger.ClearRecent);
        me.AddFields(PrismRules.DebugState, StateFields);
        me.BindKey("DebugPanel", (int)KeyCode.D, PrismLib.KeyMods.Alt, MainCore.Tr.Get("PRISM_KEY_DEBUG", "Open the debug panel"));
        me.BindKey("SettingsPanelPrism", (int)KeyCode.P, PrismLib.KeyMods.Alt, MainCore.Tr.Get("PRISM_KEY_SETTINGS", "Open the Prism settings panel"));
        BindMenuKey(me);
        me.DescribeSettings(SettingRows()); // once: it replaces whatever was described
        me.ReportPointerOverUi(() => UICore.IsOpen);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void BindMenuKey(PrismLib.ModHandle me) =>
        me.BindKey("Menu", MainCore.Conf.ToggleKey, (PrismLib.KeyMods)PrismRules.ToPrismMods(MainCore.Conf.ToggleModifier),
            MainCore.Tr.Get("PRISM_KEY_MENU", "Open the Quartz menu")); // the same id replaces the old binding
#endif
    /// <summary>Tells PrismLib's key registry where the menu hotkey is now. Runs after any change to it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RebindMenuKey() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(!Available || handle == null) return;
        try { RebindMenuKeyImpl(); } catch(Exception e) { MainCore.Log.Wrn($"PrismLib menu key rebind failed: {e.Message}"); }
#endif
    }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RebindMenuKeyImpl() => BindMenuKey((PrismLib.ModHandle)handle);
    // English labels: PrismLib.UI translates them at draw time through the translator UiWiringImpl adds.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<PrismLib.SettingEntry> SettingRows() {
        string[] mods = [
            "None",
            Keybind.ModifierName(Keybind.KeyModifier.Ctrl), Keybind.ModifierName(Keybind.KeyModifier.Alt),
            Keybind.ModifierName(Keybind.KeyModifier.Shift), Keybind.ModifierName(Keybind.KeyModifier.Cmd),
        ];
        List<PrismLib.SettingEntry> rows = [
            new PrismLib.SettingEntry {
                Id = "quartz.menu.key", Kind = PrismLib.SettingKind.Key, Page = "Quartz", Group = "Menu", Order = 0,
                Label = "Menu hotkey", Tooltip = "The key that opens Quartz's menu, with the modifier below",
                Keywords = ["keybind", "shortcut", "toggle", "open"],
                Get = () => Keybind.Format((Keybind.KeyModifier)MainCore.Conf.ToggleModifier, (KeyCode)MainCore.Conf.ToggleKey),
                Set = SetMenuKey,
            },
            new PrismLib.SettingEntry {
                Id = "quartz.menu.modifier", Kind = PrismLib.SettingKind.Enum, Page = "Quartz", Group = "Menu", Order = 1,
                Label = "Menu hotkey modifier", Options = mods, Keywords = ["keybind", "shortcut", "alt", "ctrl"],
                Get = () => MainCore.Conf.ToggleModifier,
                Set = SetMenuModifier,
            },
        ];
        if(FacetBridge.Available()) // no switch for a menu that cannot open
            rows.Add(new PrismLib.SettingEntry {
                Id = "quartz.experimental.newMenu", Kind = PrismLib.SettingKind.Bool, Page = "Quartz", Group = "Experimental", Order = 2,
                Label = "Experimental: new menu", Tooltip = "The menu hotkey opens the new menu. It is still being built; the classic menu stays available",
                Keywords = ["facet", "preview", "beta"],
                Get = () => MainCore.Conf.ExperimentalNewMenu,
                Set = v => UICore.SetNewMenuSwitch(Convert.ToBoolean(v)),
            });
        return rows;
    }
    // A name that is no key reads as none, which is refused.
    private static void SetMenuKey(object value) =>
        UICore.TrySetMenuHotkey(MainCore.Conf.ToggleModifier, Enum.TryParse(Convert.ToString(value), out KeyCode key) ? (int)key : 0);
    private static void SetMenuModifier(object value) => UICore.TrySetMenuHotkey(Convert.ToInt32(value), MainCore.Conf.ToggleKey);
    private static IEnumerable<KeyValuePair<string, string>> StateFields() => [
        new("version", Info.DisplayVersion),
        new("loader", Loader),
        new("enabled", MainCore.IsModEnabled ? "on" : "off"),
        new("menu", UICore.IsOpen ? "open" : "closed"),
        new("arranging", UICore.IsArranging ? (UICore.SharedLayoutOpen ? "shared editor" : "reorganize") : "no"),
        new("newMenu", MainCore.Conf.ExperimentalNewMenu ? "on" : "off"),
        new("hotkey", Keybind.Format((Keybind.KeyModifier)MainCore.Conf.ToggleModifier, (KeyCode)MainCore.Conf.ToggleKey)),
        new("facet", FacetBridge.Available() ? "loaded" : "unavailable"),
        new("prismlib.ui", UiAvailable ? "loaded" : "unavailable"),
        new("scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name),
    ];
    // Quartz's menu stays clickable under another mod's window; autoplay keeps its input; Prism windows speak
    // Quartz's language and report to its log (the gate installs on the first frame, before any window is built).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UiWiringImpl() {
        PrismLib.UI.Toolkit.InputGate.AddMouseExemption(MenuOpen);
        PrismLib.UI.Toolkit.InputGate.AddBypass(Autoplay);
        PrismLib.UI.Toolkit.Words.Language = PrismWindows.LanguageHook;
        PrismLib.UI.Toolkit.Words.AddTranslator(s => MainCore.Runtime == null ? null : PrismRules.Translate(s, (k, d) => MainCore.Tr.Get(k, d)));
        PrismLib.UI.Toolkit.Ui.Log = PrismWindows.LogHook;
    }
    // Exactly one mod draws the shared debug window; the claim says which, and the others ask through RequestToggle.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClaimDebugImpl() {
        debugClaim = ((PrismLib.ModHandle)handle).ClaimState(PrismLib.StateKey.DebugPanel, "shared debug window");
        if(debugClaim == null) return;
        PrismLib.Diagnostics.ToggleRequested -= ToggleDebug; // never twice
        PrismLib.Diagnostics.ToggleRequested += ToggleDebug;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RefreshClaimImpl() {
        string owner = PrismLib.Claims.OwnerOf(PrismLib.StateKey.DebugPanel);
        if(owner == Id) return;
        if(debugClaim != null) { // lost it (released by someone): stop answering requests
            debugClaim = null;
            PrismLib.Diagnostics.ToggleRequested -= ToggleDebug;
        }
        if(owner == null) ClaimDebugImpl();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ReleaseDebugClaimImpl() {
        (debugClaim as PrismLib.Claim)?.Release();
        debugClaim = null;
        PrismLib.Diagnostics.ToggleRequested -= ToggleDebug;
    }
    private static bool MenuOpen() {
        try { return UICore.IsOpen; } catch(Exception e) { Diag.Ignore(e); return false; }
    }
    private static bool Autoplay() {
        try { return RDC.auto; } catch(Exception e) { Diag.Ignore(e); return false; }
    }
    // PrismLib's animation duration -> every Facet window's speed. Both calls are newer than a PrismLib
    // or PrismLib.UI another mod may have loaded: one miss stops it for the session.
    private static bool animGone, macGone;
    private static void TickAnim() {
        if(animGone || !Available || !UiAvailable) return;
        try { SetAnim(AnimMs()); } catch(Exception e) { Diag.Ignore(e); animGone = true; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static int AnimMs() => PrismLib.Prism.AnimDuration;
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SetAnim(int ms) => PrismLib.UI.Toolkit.Anim.Duration = ms;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TickImpl() => PrismLib.UI.Toolkit.InputGate.Exempt(TickUi);
    private static void TickUi() {
        hosted = true;
        PrismGate.Tick();
        TickWindows();
        TickLayout();
    }
    // Exempt hands the gate one static method (its delegate is cached, so a frame allocates nothing).
    // `started` tells a failure to reach PrismLib.UI (before the action ran) from a throw of the action.
    // Not re-entrant: these statics are shared, so nested Exempt calls are not supported.
    private static Action pending;
    private static bool started;
    private static void RunPending() {
        started = true;
        pending();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExemptImpl(Action reads) => PrismLib.UI.Toolkit.InputGate.Exempt(reads);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryPhysicalImpl(KeyCode key, out bool held) => PrismLib.UI.Toolkit.MacKeys.TryPhysical(key, out held);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RequestToggleImpl() => PrismLib.Diagnostics.RequestToggle();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnregisterImpl() {
        PrismLib.Diagnostics.ToggleRequested -= ToggleDebug;
        debugClaim = layoutClaim = handle = null;
        PrismLib.Claims.ReleaseAll(Id);
        PrismLib.Keys.Unregister(Id);
        PrismLib.Settings.Unregister(Id);
        PrismLib.Diagnostics.Unregister(Id);
        UnregisterLayout(); // last, on its own: a PrismLib older than Layout fails only this call
    }
#endif
}
