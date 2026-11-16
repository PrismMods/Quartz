#if !IL2CPP && !QUARTZ_KEYVIEWER
using System.Runtime.CompilerServices;
using PrismLib.Bootstrap;
using PrismLib.UI.Toolkit;
using Quartz.Core;
using Quartz.Compat;
using Quartz.UI;
using Quartz.UI.FacetUi;
using Quartz.UI.Utility;
using UnityEngine;
using PrismTheme = PrismLib.UI.Toolkit.Theme;
namespace Quartz.Prism;
/// <summary>
/// The shared debug window (Alt+D) and Prism · Settings (Alt+P), drawn by Quartz when it holds PrismLib's
/// DebugPanel claim (or when PrismLib is absent). PrismLib.UI types may sit in fields here, so this class loads
/// only after PrismBridge.UiAvailable; PrismLib.dll types stay in NoInlining helpers reached after Available.
/// </summary>
internal static class PrismWindows {
    private static DebugPanel debug;
    private static SettingsPanel settings;
    private static PrismTheme scheme; // Default plus Quartz's accent: neutrals stay the suite's
    private static bool noMac, wired;
    private static float nextClaimCheck;
    /// <summary>Quartz draws the debug window: it holds the claim, or no PrismLib is loaded to hold one.</summary>
    internal static bool OwnsDebug => PrismRules.OwnsChords(PrismBridge.Available, PrismBridge.DebugClaimed, PrismBridge.ForeignLib);
#if DEBUG
    /// <summary>Self-test hook: the next Tick throws, standing in for any failure inside window hosting.</summary>
#pragma warning disable CS0649 // set by the self-test, which a stubs build leaves out
    internal static bool ThrowInTick;
#pragma warning restore CS0649
#endif
    internal static void Tick() {
#if DEBUG
        if(ThrowInTick) throw new InvalidOperationException("prism self-test: window hosting failure");
#endif
        FpsCounter.Tick(PrismBridge.Available && FpsShown());
        if(Time.unscaledTime >= nextClaimCheck) {
            nextClaimCheck = Time.unscaledTime + 1f;
            PrismBridge.RefreshClaim();
        }
        // Read every frame, before any early return: on macOS the letter's edge comes from a per-frame poll.
        bool alt = KeyHeld(KeyCode.LeftAlt) || KeyHeld(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
        bool pDown = KeyDown(KeyCode.P), dDown = KeyDown(KeyCode.D);
        PrismSettingRows.Tick();
        bool own = OwnsDebug;
        // The wheel has to be polled: the game's input module does not forward it to UI Toolkit panels.
        if(own) Ui.TickWheel(Input.mousePosition, Input.mouseScrollDelta.y);
        // Undo and redo while a Prism window is open (the game's editor keeps its own Ctrl+Z). History is every mod's, so exactly
        // one poller acts on a press: the window host, and nobody while a shared layout editor is open (whoever draws it), which reads it itself.
        // Through Facet's chord: it stands down during a canvas drag, a key capture or typing.
        bool zDown = Input.GetKeyDown(KeyCode.Z), yDown = Input.GetKeyDown(KeyCode.Y);
        if(own && (zDown || yDown) && !UICore.SharedLayoutOpen && !QuartzLayout.Visible && Ui.AnyWindowOpen && !LayoutEditorProbe.Shown())
            FacetBridge.HandleUndo(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand)
                || Input.GetKey(KeyCode.RightCommand), Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift), zDown, yDown);
        // Not while one of Quartz's key captures owns the keyboard: its runner spends this Escape on the cancel.
        if(Input.GetKeyDown(KeyCode.Escape) && Ui.AnyWindowOpen && !Keybind.Capturing) Ui.CloseTop();
        // A capture in progress owns the keyboard: no window opens with its key.
        if(own && !PrismSettingRows.Capturing) {
            if(alt && pDown) ToggleSettings();
            if(alt && dDown) ToggleDebug();
        }
        debug?.Tick();
        settings?.Tick();
    }
    internal static void ToggleDebug() {
        if(OwnsDebug || !PrismBridge.Available) { ToggleOwn(); return; } // no PrismLib to ask the holder through: the button opens Quartz's own
        try { PrismBridge.RequestToggle(); } catch(Exception e) { MainCore.Log.Wrn("[prism] debug window request failed: " + e); } // another mod draws it: ask that one
    }
    // Quartz's own window, whoever holds the claim (the self-test drives it beside other mods).
    private static void ToggleOwn() {
        try {
            BuildDebug();
            debug?.Toggle();
        } catch(Exception e) { MainCore.Log.Wrn("[prism] debug window failed: " + e); }
    }
#if DEBUG
    /// <summary>Self-test: Quartz's own debug window whoever holds the claim.</summary>
    internal static void ToggleOwnDebug() => ToggleOwn();
#endif
    internal static bool SettingsVisible => settings?.Visible == true;
    internal static void ShowSettings() {
        if(!SettingsVisible) ToggleSettings();
    }
    internal static void ToggleSettings() {
        try {
            if(!PrismBridge.Available) { MainCore.Log.Msg("[prism] settings window needs PrismLib"); return; }
            BuildSettings();
            settings?.Toggle();
        } catch(Exception e) { MainCore.Log.Wrn("[prism] settings window failed: " + e); }
    }
    internal static void Shutdown() {
        debug?.Dispose();
        settings?.Dispose();
        debug = null;
        settings = null;
        PrismSettingRows.Cancel();
    }
    private static void BuildDebug() {
        if(debug != null) return;
        Wire();
        using(PrismTheme.Use(Scheme())) debug = new DebugPanel("Prism · Debug", DebugTabs);
    }
    private static void BuildSettings() {
        if(settings != null) return;
        Wire();
        using(PrismTheme.Use(Scheme())) settings = new SettingsPanel("Prism · Settings", SettingRows);
    }
    // Quartz's language, log and settings file for what the Prism windows say, report and remember. PrismLib.UI keeps these
    // hooks for every mod's windows, so each one survives a Quartz that is gone (a failed reload, the quit order) and
    // Unwire hands back the ones still Quartz's.
    internal static readonly Func<string> LanguageHook = Language;
    internal static readonly Action<string> LogHook = m => MainCore.Runtime?.Logger.Msg("[prism] " + m);
    private static readonly Func<string, string> ReadHook = key =>
        MainCore.Runtime is { } r && r.Config.Data.FacetWindows.TryGetValue(key, out string v) ? v : null;
    private static readonly Action<string, string> WriteHook = (key, value) => {
        if(MainCore.Runtime != null) QuartzHost.Write(key, value); // a drag writes every frame; only a changed value asks for a save
    };
    internal static void Wire() {
        if(wired) return;
        wired = true;
        Words.Language = LanguageHook;
        Ui.Log = LogHook;
        Window.Read = ReadHook;
        Window.Write = WriteHook;
    }
    internal static void Unwire() {
        wired = false;
        if(Words.Language == LanguageHook) Words.Language = null;
        if(Ui.Log == LogHook) Ui.Log = _ => { };
        if(Window.Read == ReadHook) Window.Read = null;
        if(Window.Write == WriteHook) Window.Write = null;
    }
    internal static string Language() => MainCore.Runtime == null || QuartzHost.LanguageCode(MainCore.Tr.Language) != "ko" ? "en" : "ko";
    internal static PrismTheme Scheme() {
        if(scheme == null) scheme = new PrismTheme { Accent = MainCore.Conf.GetAccentColor() };
        return scheme;
    }
    /// <summary>Every accent change passes UIColors.ApplyAccent, which calls this through PrismBridge.</summary>
    internal static void Recolour(Color accent) => scheme?.Recolour(accent);
    // Unity drops some letters' keydowns while Option is held on macOS: these read through MacKeys, which asks the
    // window server there. MacKeys is newer than a PrismLib.UI another mod may have loaded: a miss falls back to Input.
    private static bool KeyDown(KeyCode k) {
        if(!noMac) try { return MacDown(k); } catch(Exception e) { MacOff(e); }
        return Input.GetKeyDown(k);
    }
    private static bool KeyHeld(KeyCode k) {
        if(!noMac) try { return MacHeld(k); } catch(Exception e) { MacOff(e); }
        return Input.GetKey(k);
    }
    private static void MacOff(Exception e) {
        noMac = true;
        MainCore.Log.Wrn($"PrismLib.UI MacKeys unavailable for the Prism chords: {e.Message}");
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool MacDown(KeyCode k) => MacKeys.Down(k);
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool MacHeld(KeyCode k) => MacKeys.Held(k);
    // Quartz's own capture coordinator and key filter for the shared settings rows' key capture. Also the
    // coordinator's cancel when an old-menu capture takes over (Release is then a no-op: it is no longer the owner).
    private static readonly object CaptureOwner = new();
    static PrismWindows() {
        PrismSettingRows.Claim = cancel => KeyCaptureCoordinator.Claim(CaptureOwner, cancel);
        PrismSettingRows.Release = () => KeyCaptureCoordinator.Release(CaptureOwner);
        PrismSettingRows.Usable = Keybind.IsUsableKey;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FpsShown() => PrismLib.Config.GetBool(PrismLib.Prism.FpsKey);
    // PrismLib.SettingEntry -> the panel's own row type: PrismLib.UI does not reference PrismLib.dll.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IEnumerable<SettingRow> SettingRows() => PrismBridge.Available ? PrismSettingRows.Rows() : new List<SettingRow>();
    internal static IEnumerable<DebugTab> DebugTabs() {
        var tabs = new List<DebugTab>();
        if(PrismBridge.Available) AddPrismTabs(tabs);
        else tabs.Add(new DebugTab { Name = PrismRules.TabPrefix + PrismRules.DebugLog, Lines = QuartzLogger.Recent, Clear = QuartzLogger.ClearRecent });
        return tabs;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddPrismTabs(List<DebugTab> tabs) {
        foreach(PrismLib.LogSource log in PrismLib.Diagnostics.Logs)
            tabs.Add(new DebugTab { Name = log.Owner + " · " + log.Name, Lines = log.Tail, Path = log.Path, Clear = log.Clear });
        foreach(PrismLib.FieldGroup group in PrismLib.Diagnostics.Fields) {
            var read = group.Read; // the delegate, not the group: a closure keeps no PrismLib type
            tabs.Add(new DebugTab { Name = group.Owner + " · " + group.Name, Lines = () => Pairs(read()) });
        }
        tabs.Add(new DebugTab { Name = "Prism", Lines = PrismState });
    }
    private static IEnumerable<string> Pairs(IEnumerable<KeyValuePair<string, string>> src) {
        foreach(KeyValuePair<string, string> kv in src) yield return kv.Key + " = " + kv.Value;
    }
    // Who is loaded, who owns what, and which hotkeys collide.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<string> PrismState() {
        var lines = new List<string>();
        try {
            lines.Add("PrismLib " + PrismLib.Prism.Version);
            foreach(string id in PrismLib.Prism.LoadedMods) {
                PrismLib.Prism.IsLoaded(id, out Version v);
                lines.Add($"  mod  {id} {v}");
            }
            lines.Add("");
            lines.Add("— state claims —");
            foreach(KeyValuePair<PrismLib.StateKey, string> kv in PrismLib.Claims.Held) lines.Add($"  {kv.Key}  ->  {kv.Value}");
            lines.Add("");
            lines.Add("— key conflicts —");
            bool any = false;
            foreach(var c in PrismLib.Keys.Conflicts()) { lines.Add($"  {c.Key}  vs  {c.Value}"); any = true; }
            if(!any) lines.Add("  (none)");
            lines.Add("");
            lines.Add("— registered keys —");
            foreach(PrismLib.KeyBinding k in PrismLib.Keys.All) lines.Add("  " + k);
        } catch(Exception e) { lines.Add("(unavailable: " + e.Message + ")"); }
        return lines;
    }
}
#endif
