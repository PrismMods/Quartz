#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.UI;
using Quartz.UI.FacetUi;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.Prism;
/// <summary>
/// The PrismLib-naming self-test checks, run only once PrismBridge.Available. PrismLib types stay in
/// the NoInlining helpers below, never in the iterator's locals or a lambda's signature: either one
/// becomes a typed field of a compiler-made class.
/// </summary>
internal static partial class PrismSelfTestChecks {
    internal static IEnumerator Loading(Action<string, bool, string> check, Action<string> info) {
        Assembly[] ui = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "PrismLib.UI").ToArray();
        Version loaded = ui.Length > 0 ? ui[0].GetName().Version : null;
        string game = Path.GetDirectoryName(PrismRules.ModsRoot(MainCore.Host.ModsPath, umm: PrismBridge.Loader == "UMM"));
        string store = PrismBridge.StoreDir(MainCore.Host);
        IReadOnlyList<string> copies = PrismRules.UiCopies(game);
        check("prism: PrismLib wired", PrismBridge.Available && PrismBridge.UiAvailable, $"lib={PrismBridge.Available} ui={PrismBridge.UiAvailable}");
        check("prism: exactly one PrismLib.UI loaded", ui.Length == 1, string.Join(", ", ui.Select(a => a.Location)));
        check("prism: the loaded PrismLib.UI is the newest copy on disk",
            loaded != null && copies.All(p => AssemblyName.GetAssemblyName(p).Version <= loaded),
            $"loaded={loaded}; " + string.Join(", ", copies.Select(p => p + "=" + AssemblyName.GetAssemblyName(p).Version)));
        string lib = PrismLibLocation();
        check("prism: PrismLib.dll came from a shared PrismLib folder", FromSharedFolder(game, lib, PrismLibVersion()), lib.Length > 0 ? lib : "byte-loaded");
        check("prism: the store holds the bundle, never PrismLib.dll",
            File.Exists(Path.Combine(store, "PrismLib.bundle")) && !File.Exists(Path.Combine(store, "PrismLib.dll")), store);
        check("prism: Quartz is registered", Registered(), null);
        check("prism: Quartz's log is in the debug registry", LogListed(PrismRules.DebugLog), null);
        check("prism: Quartz's fields are in the debug registry", FieldsListed(PrismRules.DebugState), null);
        // Registered in English, shown in Quartz's language: the tab text goes through Words where it is drawn.
        string[] shown = [Words.T(PrismRules.TabPrefix + PrismRules.DebugLog), Words.T(PrismRules.TabPrefix + PrismRules.DebugState)];
        check("prism: Quartz's debug tabs show in Quartz's language",
            shown[0] == PrismRules.TabPrefix + MainCore.Tr.Get("PRISM_DEBUG_LOG", PrismRules.DebugLog)
            && shown[1] == PrismRules.TabPrefix + MainCore.Tr.Get("PRISM_DEBUG_STATE", PrismRules.DebugState),
            $"{MainCore.Tr.Language}: {string.Join(" | ", shown)}");
        check("prism: the menu key is registered", MenuKeyBound(), null);
        HarmonyLib.Patches gate = HarmonyLib.Harmony.GetPatchInfo(typeof(Input).GetMethod("GetKey", [typeof(KeyCode)]));
        // Under UMMBridge Quartz's HarmonyLib is the UMM-side copy, which does not list patches made through MelonLoader's:
        // with no patch info but an installer on record, the gate is someone else's. The windows check's key probe is the guard.
        string installer = gate == null && PrismBridge.Loader == "UMM" ? GateInstaller() : null;
        if(installer != null) info($"prism: the input gate is installed by {installer} through another Harmony; Quartz's HarmonyLib sees no patch info (UMMBridge's split Harmony)");
        else check("prism: the input gate is patched",
            gate?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName?.StartsWith("PrismLib.UI.Toolkit.InputGate", StringComparison.Ordinal) == true) == true,
            "owners=" + (gate == null ? "none" : string.Join(",", gate.Postfixes.Select(p => p.owner))));
        // Alone, Quartz's gate is the only one there is: its patches carry Quartz's Harmony id.
        if(PrismLibMods().Count == 1)
            check("prism: the input gate is Quartz's", gate?.Postfixes.Any(p => p.owner == PrismGate.HarmonyId) == true, null);
        int ran = 0;
        PrismBridge.Exempt(() => ran++);
        check("prism: Exempt runs its action once", ran == 1, $"ran={ran}");
        var thrown = new InvalidOperationException("prism self-test");
        Exception caught = null;
        try { PrismBridge.Exempt(() => throw thrown); } catch(Exception e) { caught = e; }
        check("prism: an exception from Exempt's action reaches the caller and keeps PrismLib.UI on",
            ReferenceEquals(caught, thrown) && PrismBridge.UiAvailable, $"caught={caught?.GetType().Name} ui={PrismBridge.UiAvailable}");
        // A throw from Quartz's menu update still runs the Prism tick that frame, and still reaches the caller.
        int gateTicks = PrismGate.Ticks;
        caught = null;
        try { Quartz.Core.Service.UIService.Tick(() => throw thrown); } catch(Exception e) { caught = e; }
        check("prism: a throw from the menu update still runs the Prism tick", ReferenceEquals(caught, thrown) && PrismGate.Ticks == gateTicks + 1,
            $"caught={caught?.GetType().Name} gate ticks {gateTicks} -> {PrismGate.Ticks}");
        // Quartz's TickAnim copies anim.duration into Facet: see 240 in the same frame (other mods copy it every
        // frame too, so waiting would pass without Quartz).
        int seen = AnimProbe();
        check("prism: anim.duration reaches Facet in Quartz's own tick", seen == 240, $"Anim.Duration={seen}");
        // Layout labels in Quartz's language once its table has loaded: at startup the first describe can come while it loads
        // (Tr answers in English until then). Then that race on demand: a reload with a describe in the middle of it.
        yield return Labels("layout: target labels are in Quartz's language", check);
        _ = MainCore.Tr.Load(MainCore.Paths.LangPath);
        PanelsOverlay.Rebuild(); // tags a handle again: Quartz describes while the table loads
        yield return Labels("layout: ...and after a reload that described while it loaded", check);
        info($"MODE {Mode()} loader={PrismBridge.Loader}");
        var sw = new Stopwatch();
        for(int i = 0; i < 600; i++) {
            sw.Start();
            PrismBridge.Tick();
            sw.Stop();
            yield return null;
        }
        info($"[perf] PrismBridge.Tick mean over 600 frames, nothing open: {sw.Elapsed.TotalMilliseconds * 1000 / 600:0.0} µs");
    }
    private static IEnumerator Labels(string name, Action<string, bool, string> check) {
        for(float until = Time.unscaledTime + 10f; MainCore.Tr.IsLoading && Time.unscaledTime < until;) yield return null;
        yield return Frames(3);
        string combo = MineById(LayoutIds.Combo)?.Label;
        string[] stale = [.. Mine().Where(t => ReorganizeHandle.FindLayout(t.Id)?.GetName?.Invoke() is { } now && now != t.Label).Select(t => t.Id + "=" + t.Label)];
        check(name, combo == MainCore.Tr.Get("COMBO", "Combo") && stale.Length == 0, $"{MainCore.Tr.Language}: combo={combo} stale={string.Join(",", stale)}");
    }
    // One Ctrl+Z undoes one step. History is every mod's, so of its pollers (each Prism mod and an open layout editor)
    // exactly one may act on a press. The press goes through Input for one frame, as every poller reads it.
    internal static IEnumerator Undo(Action<string, bool, string> check) {
        // The windows checks can leave the debug claim free for up to a second (Quartz's retry takes it back when alone).
        for(float until = Time.unscaledTime + 1.5f; DebugOwner() == null && Time.unscaledTime < until;) yield return null;
        PrismBridge.TogglePrismSettings();
        yield return Frames(2);
        yield return OnePress("undo: one Ctrl+Z with Prism · Settings open undoes one step", false, check);
        PrismBridge.TogglePrismSettings();
        yield return Frames(4);
        PrismBridge.OpenLayoutEditor();
        yield return Frames(4);
        yield return OnePress("undo: one Ctrl+Z in the shared layout editor undoes one step", true, check);
        Ui.CloseTop();
        yield return Frames(4);
    }
    private static IEnumerator OnePress(string name, bool inEditor, Action<string, bool, string> check) {
        int undone = 0;
        History.Record(name, name + "#1", () => undone++, () => { }); // keys of their own: History coalesces a repeated key
        History.Record(name, name + "#2", () => undone++, () => { });
        bool window = Ui.AnyWindowOpen, editor = UICore.SharedLayoutOpen;
        EnsureProbe();
        chordFrame = Time.frameCount + 1;
        yield return Frames(4);
        check(name, window && editor == inEditor && undone == 1, $"undone={undone} window={window} editor={editor}");
    }
    // Last, as it turns the UI half off for the rest of the session: a failing UI tick drops Quartz's claims, so another mod's
    // retry can take them and draw what Quartz no longer can.
    internal static IEnumerator UiOff(Action<string, bool, string> check) {
        string debug0 = DebugOwner(), layout0 = LayoutHost();
        PrismGate.ThrowInTick = true;
        yield return Frames(2);
        PrismGate.ThrowInTick = false;
        check("prism: a failing UI tick turns PrismLib.UI off and drops Quartz's claims",
            !PrismBridge.UiAvailable && DebugOwner() != PrismBridge.Id && LayoutHost() != PrismBridge.Id,
            $"ui={PrismBridge.UiAvailable} debug {debug0} -> {DebugOwner()} layout {layout0} -> {LayoutHost()}");
    }
    // Quartz's Prism · Settings rows: described once, writing Quartz's own settings, and the hotkey rebinding in PrismLib's registry.
    internal static IEnumerator Settings(Action<string, bool, string> check, Action<string> info) {
        string[] ids = RowIds();
        check("settings: Quartz describes three rows",
            ids.OrderBy(k => k).SequenceEqual(new[] { "quartz.experimental.newMenu", "quartz.menu.key", "quartz.menu.modifier" }), string.Join(",", ids));
        check("settings: the new-menu switch defaults off", !new Quartz.IO.CoreSettings().ExperimentalNewMenu, null);
        bool was = MainCore.Conf.ExperimentalNewMenu;
        var windows = new Dictionary<string, string>(MainCore.Conf.FacetWindows);
        var (nmGet, nmSet) = Row("quartz.experimental.newMenu");
        int calls = UICore.NewMenuSwitchCalls;
        nmSet(true);
        check("settings: the new-menu row goes through the shared switch helper", UICore.NewMenuSwitchCalls == calls + 1, null);
        check("settings: the switch writes Quartz's settings", MainCore.Conf.ExperimentalNewMenu && (bool)nmGet(), null);
        check("settings: the switch is saved", MainCore.Conf.Serialize()["ExperimentalNewMenu"]?.Value<bool>() == true, null);
        nmSet(was);
        // On, it built the new menu ahead, which stores its page: put the map back (saved below), and let the classic pages'
        // rebuild land before the arrange checks enter reorganize mode.
        MainCore.Conf.FacetWindows.Clear();
        foreach(var kv in windows) MainCore.Conf.FacetWindows[kv.Key] = kv.Value;
        yield return Frames(3);
        var dflt = new Quartz.IO.CoreSettings { ExperimentalNewMenu = true };
        dflt.Deserialize(new JObject());
        check("settings: a config without the switch reads off, not the previous value", !dflt.ExperimentalNewMenu, null);
        int k0 = MainCore.Conf.ToggleKey, m0 = MainCore.Conf.ToggleModifier;
        var (_, keySet) = Row("quartz.menu.key");
        var (_, modSet) = Row("quartz.menu.modifier");
        try {
            modSet(2);
            keySet("J");
            check("settings: the hotkey row rebinds", MainCore.Conf.ToggleKey == (int)KeyCode.J && MenuKeyBound(), $"key={MainCore.Conf.ToggleKey}");
            keySet("D");
            check("settings: a Prism chord is refused", MainCore.Conf.ToggleKey == (int)KeyCode.J, null);
            keySet("LeftShift");
            check("settings: a bare modifier is refused", MainCore.Conf.ToggleKey == (int)KeyCode.J, null);
            keySet("106");
            check("settings: a numeric string is refused", MainCore.Conf.ToggleKey == (int)KeyCode.J, null);
            modSet(1);
            check("settings: the modifier row writes Ctrl", MainCore.Conf.ToggleModifier == 1 && MenuKeyBound(), null);
            // A profile switch, reset or import re-reads Conf and runs ApplyRuntimeSettings: the registry must follow.
            MainCore.Conf.ToggleKey = (int)KeyCode.H;
            MainCore.Conf.ToggleModifier = 3;
            typeof(Quartz.IO.ProfileManager).GetMethod("ApplyRuntimeSettings", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            check("settings: a profile reload rebinds the hotkey in PrismLib's registry", MenuKeyBound(), $"key={MainCore.Conf.ToggleKey} mod={MainCore.Conf.ToggleModifier}");
        } finally {
            // Straight to Conf: the user's real hotkey may be one the row refuses (Alt+D).
            MainCore.Conf.ToggleKey = k0;
            MainCore.Conf.ToggleModifier = m0;
            MainCore.ConfMgr.RequestSave();
            PrismBridge.RebindMenuKey();
        }
        check("settings: the hotkey is restored", MainCore.Conf.ToggleKey == k0 && MainCore.Conf.ToggleModifier == m0 && MenuKeyBound(), null);
        string others = WantedBy(MainCore.Conf.ToggleKey, MainCore.Conf.ToggleModifier);
        check("keys: the menu hotkey is wanted by no other mod", others.Length == 0, others);
        check("settings: every Prism label has a Lang key", PrismRules.EnglishToKey.Values.All(k => MainCore.Tr.HasKey(k)), null);
        if(Keybind.IsMac) {
            Keybind.KeyHeld(KeyCode.K); // a read through the Mac path
            check("keys: the Mac path reads PrismLib's MacKeys", Keybind.MacKeysInUse, $"ui={PrismBridge.UiAvailable}");
        } else info("keys: not a Mac, MacKeys not exercised");
        yield break;
    }
    // The new menu's link to Prism · Settings, and its hotkey capture under an open blocking window.
    internal static IEnumerator NewMenu(Action<string, bool, string> check, Action<string> info) {
        bool was = PrismWindows.SettingsVisible;
        try {
            PrismBridge.OpenPrismSettings();
            yield return Frames(2);
            bool shown = PrismWindows.SettingsVisible;
            PrismBridge.OpenPrismSettings();
            yield return Frames(2);
            check("new menu: Open Prism · Settings shows it, and a second call leaves it showing", shown && PrismWindows.SettingsVisible, $"first={shown}");
            // The capture runner polls outside every mod's UI tick: past the input gate, or an open window would hide every key from it.
            bool blocking = InputGate.Blocking, ended = false;
            KeyCode got = KeyCode.A;
            KeyCaptureService.Start(k => got = k, () => ended = true);
            EnsureProbe();
            chordKey = KeyCode.Escape;
            chordFrame = Time.frameCount + 1;
            yield return Frames(4);
            check("new menu: a key capture hears Escape while a blocking window is open, and ends without a key",
                blocking && ended && got == KeyCode.None && !KeyCaptureService.IsCapturing, $"blocking={blocking} ended={ended} got={got}");
            // Every other Prism mod polls Escape too, and one that offers it to Facet before Quartz's runner spends it closes the window.
            if(PrismLibMods().Count == 1) check("new menu: that Escape leaves the window open", PrismWindows.SettingsVisible, null);
            else info($"new menu: beside other Prism mods, the window after that Escape is {(PrismWindows.SettingsVisible ? "open" : "closed")}");
        } finally {
            chordKey = KeyCode.Z;
            KeyCaptureService.Cancel();
            if(PrismWindows.SettingsVisible != was) PrismBridge.TogglePrismSettings();
        }
        yield return Frames(2);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string[] RowIds() => PrismLib.Settings.Of(PrismBridge.Id).Select(kv => kv.Value.Id).ToArray();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Func<object>, Action<object>) Row(string id) {
        PrismLib.SettingEntry e = PrismLib.Settings.Of(PrismBridge.Id).First(kv => kv.Value.Id == id).Value;
        return (e.Get, e.Set);
    }
    // Quartz's modifier (None, Ctrl, Alt, Shift, Cmd) as PrismLib's bits.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string WantedBy(int key, int modifier) =>
        string.Join(",", PrismLib.Keys.Wanting(key, (PrismLib.KeyMods)PrismRules.ToPrismMods(modifier), PrismBridge.Id).Select(b => b.Owner));
    // Sets anim.duration to 240 and Facet's copy to a sentinel, ticks Quartz once and returns what Facet now holds. Both put back.
    private static int AnimProbe() {
        string was = AnimGet();
        int facetWas = FacetAnim();
        try {
            AnimSet("240");
            FacetAnimSet(77);
            PrismBridge.Tick();
            return FacetAnim();
        } finally {
            AnimSet(was ?? "120");
            FacetAnimSet(facetWas);
        }
    }
    private static IEnumerator Frames(int n) {
        for(int i = 0; i < n; i++) yield return null;
    }
    internal static IEnumerator Windows(Action<string, bool, string> check, Action<string> info) {
        bool quartzOnly = PrismLibMods().Count == 1;
        string owner = DebugOwner();
        check("windows: the debug window has an owner", quartzOnly ? owner == PrismBridge.Id : owner != null, $"owner={owner}");
        PrismBridge.ToggleDebug();
        yield return Frames(2);
        check("windows: the Alt+D path opens the debug window", Ui.AnyWindowOpen, null);
        check("windows: an open Prism window withholds game input", InputGate.Blocking, null); // the self-test ticks outside Exempt
        var (gated, exempt) = ProbeKey();
        check("windows: the gate turns a pressed key into no key for the game, and Exempt still reads it", !gated && exempt, $"game={gated} exempt={exempt}");
        PrismBridge.ToggleDebug();
        yield return Frames(2);
        check("windows: and closes it", !Ui.AnyWindowOpen, null);
        var (open, _) = ProbeKey();
        check("windows: with no window open the game reads that key", open, $"game={open}");
        PrismWindows.ToggleOwnDebug();
        yield return Frames(2);
        check("windows: Quartz's own debug window opens", Ui.AnyWindowOpen, null);
        PrismWindows.ToggleOwnDebug();
        yield return Frames(2);
        check("windows: and closes", !Ui.AnyWindowOpen, null);
        if(owner == PrismBridge.Id) {
            string[] tabs = PrismWindows.DebugTabs().Select(t => t.Name).ToArray();
            check("windows: debug tabs list Quartz", new[] { PrismRules.TabPrefix + PrismRules.DebugLog, PrismRules.TabPrefix + PrismRules.DebugState, "Prism" }.All(tabs.Contains),
                string.Join(" | ", tabs));
        }
        PrismBridge.TogglePrismSettings();
        yield return Frames(2);
        check("windows: Prism · Settings opens", Ui.AnyWindowOpen, null);
        check("windows: it lists PrismLib's own rows", PrismWindows.SettingRows().Any(r => r.Owner == "PrismLib" && r.Label == "Show FPS"), null);
        PrismBridge.TogglePrismSettings();
        yield return Frames(2);
        check("windows: and closes", !Ui.AnyWindowOpen, null);
        // Another mod's window build re-points these shared statics at its own language and store, so only a Quartz-only session pins them.
        if(quartzOnly) {
            string want = QuartzHost.LanguageCode(MainCore.Tr.Language) == "ko" ? "ko" : "en";
            check("windows: PrismLib's words follow Quartz's language", Words.Language() == want, $"{Words.Language()} vs {want}");
            Window.Write("prism-selftest", "1");
            check("windows: geometry lands in Quartz's settings", MainCore.Conf.FacetWindows.TryGetValue("prism-selftest", out string v) && v == "1", null);
            Window.Write("prism-selftest", null); // the writer drops a null key again
            // Quartz's shutdown hands back PrismLib.UI's shared hooks it still holds; the next window build takes them again.
            PrismWindows.Unwire();
            bool handed = Ui.Log != PrismWindows.LogHook && Words.Language == null && Window.Read == null && Window.Write == null;
            PrismWindows.Wire();
            check("windows: Quartz hands PrismLib.UI's shared hooks back, and takes them again", handed && Ui.Log == PrismWindows.LogHook && Words.Language == PrismWindows.LanguageHook, null);
        } else info("windows: Words.Language and Window.Write not asserted beside other Prism mods");
        // Another holder (Sapphire, Topaz) lets go: Quartz's once-a-second retry takes the claim. Quartz never releases its
        // own, which would drop every claim it holds until that retry.
        string holder = DebugOwner();
        object held = holder != null && holder != PrismBridge.Id ? HeldBy(holder) : null;
        try {
            if(held != null) ReleaseOwner(holder);
            float until = Time.unscaledTime + 1.5f;
            while(DebugOwner() != PrismBridge.Id && Time.unscaledTime < until) yield return null;
            check("windows: Quartz takes the debug claim over when its holder lets go", DebugOwner() == PrismBridge.Id, $"holder={holder} now={DebugOwner()}");
            // A throw inside window hosting must cost only the windows: the claim and the windows go, TickAnim and the gate carry on.
            PrismWindows.ToggleOwnDebug();
            yield return Frames(2);
            bool wasOpen = Ui.AnyWindowOpen;
            int gateTicks = PrismGate.Ticks;
            PrismWindows.ThrowInTick = true;
            yield return Frames(3);
            PrismWindows.ThrowInTick = false;
            check("windows: a failing window tick drops the windows and the debug claim",
                wasOpen && !Ui.AnyWindowOpen && DebugOwner() != PrismBridge.Id, $"wasOpen={wasOpen} open={Ui.AnyWindowOpen} owner={DebugOwner()}");
            int anim = AnimProbe();
            check("windows: ...and anim.duration still reaches Facet", anim == 240 && PrismBridge.UiAvailable, $"Anim.Duration={anim} ui={PrismBridge.UiAvailable}");
            check("windows: ...and the input gate is still ticked", PrismGate.Ticks >= gateTicks + 3, $"ticks {gateTicks} -> {PrismGate.Ticks}");
            PrismBridge.ResetWindowsOff();
            yield return Frames(2);
        } finally {
            if(held != null) check("windows: the debug holder gets its claims back", GiveBack(holder, held) && DebugOwner() == holder, $"holder={holder} now={DebugOwner()}");
        }
    }
    // The gate at work, whoever installed it and through whichever Harmony: a prefix of the self-test's own makes
    // Input.GetKey(F15) read as pressed, and Harmony still runs every postfix after a prefix that skips the original, so the
    // gate's postfix decides what the caller sees. (game read, read inside Exempt). The prefixes stay, inert, for the session.
    private static bool probing;
    private static int chordFrame = -1; // Ctrl+<chordKey> reads as pressed in this frame only
    private static KeyCode chordKey = KeyCode.Z;
    private static HarmonyLib.Harmony probe;
    private static void EnsureProbe() {
        if(probe != null) return;
        probe = new HarmonyLib.Harmony("Quartz.PrismSelfTest");
        Prefix("GetKey", nameof(PressKey));
        Prefix("GetKeyDown", nameof(PressKeyDown));
    }
    private static void Prefix(string read, string prefix) {
        HarmonyLib.PatchProcessor proc = probe.CreateProcessor(typeof(Input).GetMethod(read, [typeof(KeyCode)]));
        proc.AddPrefix(new HarmonyLib.HarmonyMethod(typeof(PrismSelfTestChecks).GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)));
        proc.Patch();
    }
    private static (bool, bool) ProbeKey() {
        EnsureProbe();
        probing = true;
        try {
            bool game = Input.GetKey(KeyCode.F15), exempt = false;
            PrismBridge.Exempt(() => exempt = Input.GetKey(KeyCode.F15));
            return (game, exempt);
        } finally {
            probing = false;
        }
    }
    private static bool PressKey(KeyCode __0, ref bool __result) {
        if(!(probing && __0 == KeyCode.F15 || __0 == KeyCode.LeftControl && Time.frameCount == chordFrame)) return true;
        __result = true;
        return false;
    }
    private static bool PressKeyDown(KeyCode __0, ref bool __result) {
        if(__0 != chordKey || Time.frameCount != chordFrame) return true;
        __result = true;
        return false;
    }
    // PrismLib.UI keeps the installing mod's id private; null when it has none or the field is gone.
    private static string GateInstaller() =>
        typeof(InputGate).GetField("_installedBy", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as string;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReleaseOwner(string owner) => PrismLib.Claims.ReleaseAll(owner);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string DebugOwner() => PrismLib.Claims.OwnerOf(PrismLib.StateKey.DebugPanel);
    // UMMBridge's Assembly.LoadFrom interceptor loads PrismLib from bytes when a UMM mod's bootstrap loads it
    // (seen 2026-11-07), leaving Location empty: then a shared folder must hold that version.
    private static bool FromSharedFolder(string game, string location, Version loaded) {
        if(location.Length > 0) return Path.GetFileName(Path.GetDirectoryName(location)) == "PrismLib";
        foreach(string root in new[] { "Mods", "UMMMods" }) {
            string shared = Path.Combine(game, root, "PrismLib", "PrismLib.dll");
            if(File.Exists(shared) && AssemblyName.GetAssemblyName(shared).Version == loaded) return true;
        }
        return false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string PrismLibLocation() => typeof(PrismLib.Prism).Assembly.Location;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Version PrismLibVersion() => typeof(PrismLib.Prism).Assembly.GetName().Version;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Registered() => PrismLib.Prism.IsLoaded(PrismBridge.Id, out _);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LogListed(string name) {
        foreach(PrismLib.LogSource log in PrismLib.Diagnostics.Logs) {
            if(log.Owner != PrismBridge.Id || log.Name != name) continue;
            foreach(string line in log.Tail())
                if(line.Contains("Hello")) return true;
        }
        return false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FieldsListed(string name) {
        foreach(PrismLib.FieldGroup group in PrismLib.Diagnostics.Fields) {
            if(group.Owner != PrismBridge.Id || group.Name != name) continue;
            foreach(KeyValuePair<string, string> kv in group.Read())
                if(kv.Key == "version") return true;
        }
        return false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MenuKeyBound() {
        foreach(PrismLib.KeyBinding k in PrismLib.Keys.All)
            if(k.Owner == PrismBridge.Id && k.Id == "Menu" && k.KeyCode == MainCore.Conf.ToggleKey
                && (int)k.Mods == PrismRules.ToPrismMods(MainCore.Conf.ToggleModifier)) return true;
        return false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<string> PrismLibMods() => PrismLib.Prism.LoadedMods.ToList();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Mode() {
        List<string> mods = PrismLibMods();
        return mods.Count == 1 ? "quartz-only" : "beside " + string.Join(",", mods);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string AnimGet() => PrismLib.Config.Get(PrismLib.Prism.AnimKey);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AnimSet(string value) => PrismLib.Config.Set(PrismLib.Prism.AnimKey, value);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FacetAnimSet(int ms) => PrismLib.UI.Toolkit.Anim.Duration = ms;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int FacetAnim() => PrismLib.UI.Toolkit.Anim.Duration;
}
#endif
