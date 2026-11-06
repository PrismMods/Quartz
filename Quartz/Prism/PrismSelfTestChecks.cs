#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using Quartz.Core;
using Quartz.UI.FacetUi;
using UnityEngine;
namespace Quartz.Prism;
/// <summary>
/// The PrismLib-naming self-test checks, run only once PrismBridge.Available. PrismLib types stay in
/// the NoInlining helpers below, never in the iterator's locals or a lambda's signature: either one
/// becomes a typed field of a compiler-made class.
/// </summary>
internal static class PrismSelfTestChecks {
    internal static IEnumerator Loading(Action<string, bool, string> check, Action<string> info) {
        Assembly[] ui = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "PrismLib.UI").ToArray();
        Version loaded = ui.Length > 0 ? ui[0].GetName().Version : null;
        string game = Path.GetDirectoryName(PrismRules.ModsRoot(MainCore.Host.ModsPath, umm: PrismBridge.Loader == "UMM"));
        string store = PrismBridge.StoreDir(MainCore.Host.QuartzFilePath);
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
        check("prism: Quartz's log is in the debug registry", LogListed(MainCore.Tr.Get("PRISM_DEBUG_LOG", "log")), null);
        check("prism: Quartz's fields are in the debug registry", FieldsListed(MainCore.Tr.Get("PRISM_DEBUG_STATE", "State")), null);
        check("prism: the menu key is registered", MenuKeyBound(), null);
        HarmonyLib.Patches gate = HarmonyLib.Harmony.GetPatchInfo(typeof(Input).GetMethod("GetKey", [typeof(KeyCode)]));
        check("prism: the input gate is patched",
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
        // Quartz's TickAnim copies anim.duration into Facet: see 240 in the same frame (other mods copy it every
        // frame too, so waiting would pass without Quartz).
        int seen = AnimProbe();
        check("prism: anim.duration reaches Facet in Quartz's own tick", seen == 240, $"Anim.Duration={seen}");
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
        PrismBridge.ToggleDebug();
        yield return Frames(2);
        check("windows: and closes it", !Ui.AnyWindowOpen, null);
        PrismWindows.ToggleOwnDebug();
        yield return Frames(2);
        check("windows: Quartz's own debug window opens", Ui.AnyWindowOpen, null);
        PrismWindows.ToggleOwnDebug();
        yield return Frames(2);
        check("windows: and closes", !Ui.AnyWindowOpen, null);
        if(owner == PrismBridge.Id) {
            string log = MainCore.Tr.Get("PRISM_DEBUG_LOG", "log"), state = MainCore.Tr.Get("PRISM_DEBUG_STATE", "State");
            string[] tabs = PrismWindows.DebugTabs().Select(t => t.Name).ToArray();
            check("windows: debug tabs list Quartz", new[] { "Quartz · " + log, "Quartz · " + state, "Prism" }.All(tabs.Contains), string.Join(" | ", tabs));
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
        } else info("windows: Words.Language and Window.Write not asserted beside other Prism mods");
        // The holder lets go (whoever it is: Sapphire, Topaz, or Quartz itself): Quartz's once-a-second retry takes the claim.
        string holder = DebugOwner();
        if(holder != null) ReleaseOwner(holder);
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
    }
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
            if(k.Owner == PrismBridge.Id && k.Id == "Menu" && k.KeyCode == MainCore.Conf.ToggleKey) return true;
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
