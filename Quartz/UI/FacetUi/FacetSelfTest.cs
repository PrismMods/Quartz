#if DEBUG && !IL2CPP
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.Features;
using Quartz.IO;
using Quartz.IO.Interface;
using Quartz.Prism;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Debug-only in-game self-test, driven by the file UserData/Quartz/&lt;name&gt; (default facet-selftest).
/// Writes PASS/FAIL lines to &lt;name&gt;.log, ends with "DONE pass=N fail=M", then quits the game.
/// Runs <c>body(check, info)</c>, or the Facet checks when there is none; with featuresOn, the body runs with the features the
/// checks need on, as the Facet checks do. Names no Facet type;
/// Facet-dependent checks live in FacetBridge.
/// </summary>
public sealed class FacetSelfTest(string name = "facet-selftest", Func<Action<string, bool, string>, Action<string>, IEnumerator> body = null,
    bool featuresOn = false) : IRuntimeTick {
    private const float StartAfterSeconds = 25f;
    // Every runner's marker. Two armed in one launch would run interleaved, and the first DONE quits before the other's
    // finally puts things back (1-1's attempt flushed into the player's PlayCount.json).
    private static readonly string[] Markers = ["facet-selftest", "prism-selftest", "facet-level-selftest", "facet-pages-selftest"];
    private bool done;
    private int pass, fail;
    private StringBuilder log;
    private readonly Stack<IEnumerator> steps = new();
    public void Tick() {
        if(done || Time.realtimeSinceStartup < StartAfterSeconds) return;
        if(steps.Count == 0) {
            string marker = Path.Combine(MainCore.Paths.RootPath, name);
            if(!File.Exists(marker)) { done = true; return; }
            string[] others = [.. Markers.Where(m => m != name && File.Exists(Path.Combine(MainCore.Paths.RootPath, m)))];
            if(others.Length > 0) {
                // The marker stays: every armed runner must see the conflict and refuse, not just the first to tick.
                done = true;
                File.WriteAllText(Path.Combine(MainCore.Paths.RootPath, name + ".log"),
                    $"FAIL one marker per launch - also armed: {string.Join(", ", others)}\nDONE pass=0 fail=1\n");
                Application.Quit();
                return;
            }
            log = new StringBuilder();
            steps.Push(Doored(Unmade(Powered(body == null ? FeaturesOn(Run()) : featuresOn ? FeaturesOn(body(Check, Info)) : body(Check, Info)))));
        }
        if(!Step(steps, Check)) return;
        done = true;
        End();
        log.AppendLine($"DONE pass={pass} fail={fail}");
        File.WriteAllText(Path.Combine(MainCore.Paths.RootPath, name + ".log"), log.ToString());
        Application.Quit();
    }
    // Runs the stack until a check waits a frame (false) or none is left (true). Checks that span frames are iterators; a
    // yielded IEnumerator runs to completion first.
    internal static bool Step(Stack<IEnumerator> steps, Action<string, bool, string> check) {
        while(steps.Count > 0) {
            IEnumerator top = steps.Peek();
            bool more;
            try {
                more = top.MoveNext();
            } catch(Exception e) {
                // Only the iterator that threw ends (its finally restores what it changed); the one that yielded it goes on
                // with its next check, so one throw costs one section, not the rest of the run.
                check("runner", false, e.ToString());
                try { (steps.Pop() as IDisposable)?.Dispose(); } catch(Exception d) { Diag.Ignore(d); }
                continue;
            }
            if(!more) { steps.Pop(); continue; }
            if(top.Current is IEnumerator inner) { steps.Push(inner); continue; }
            return false; // wait a frame
        }
        return true;
    }
    private static bool RunnerSurvivesThrow(out string detail) {
        List<string> seen = [];
        var stack = new Stack<IEnumerator>();
        stack.Push(ThrowThenCheck(seen));
        bool ended = Step(stack, (c, ok, _) => seen.Add($"{c}:{ok}"));
        detail = string.Join(" ", seen);
        return ended && detail == "runner:False after:True";
    }
    private static IEnumerator ThrowThenCheck(List<string> seen) {
        yield return Throws(seen);
        seen.Add("after:True");
    }
    private static IEnumerator Throws(List<string> seen) {
        if(seen != null) throw new InvalidOperationException("self-test throw");
        yield break;
    }
    // A run ends by removing its marker, so a later launch does not run it again (or arm it beside another runner).
    private void End() {
        try { File.Delete(Path.Combine(MainCore.Paths.RootPath, name)); } catch(Exception e) { Diag.Ignore(e); }
    }
    // The self-test door stands in for the network for the whole run, from before the first check to after the last.
    private IEnumerator Doored(IEnumerator run) {
        SelfTestDoor.Install();
        try {
            yield return run;
        } finally {
            Check("self-test run: no request left the machine", SelfTestDoor.Network == 0, $"network={SelfTestDoor.Network}");
            SelfTestDoor.Reset();
        }
    }
    // A feature the run turns on loads its settings file, and quitting saves every loaded one: a file the run made (its feature
    // never set by the player) goes, and its handle leaves the registry, or that save writes it again.
    private IEnumerator Unmade(IEnumerator run) {
        var files = new HashSet<string>(Directory.GetFiles(MainCore.Paths.RootPath, "*.json"));
        try {
            yield return run;
        } finally {
            var gone = new List<string>();
            foreach(ISettingsHandle h in SettingsRegistry.Snapshot()) {
                if(files.Contains(h.Path) || Path.GetDirectoryName(h.Path) != MainCore.Paths.RootPath.TrimEnd('/', '\\')) continue;
                SettingsRegistry.Unregister(h);
                try {
                    if(File.Exists(h.Path)) File.Delete(h.Path);
                } catch(Exception e) {
                    Info($"could not remove {h.Path}: {e.Message}");
                    Diag.Ignore(e);
                }
                gone.Add(Path.GetFileName(h.Path));
            }
            if(gone.Count > 0) Info($"settings files the run made, removed: {string.Join(", ", gone)}");
        }
    }
    // Powered off there are no overlays or layout targets to check, and the rig may have the power saved off. Power on
    // for the run only, never writing the player's stored switch, and put the runtime back after.
    private IEnumerator Powered(IEnumerator run) {
        bool wasOn = MainCore.IsModEnabled, wasActive = MainCore.Conf.Active;
        try {
            if(!wasOn) {
                MainCore.Runtime.SetModEnabled(true, false, save: false);
                Info("power: was off, powered on for the run");
                for(int f = Time.frameCount + 3; Time.frameCount < f;) yield return null;
            }
            Check("Quartz is powered on for the run", MainCore.IsModEnabled, $"wasOn={wasOn}");
            yield return run;
        } finally {
            if(!wasOn) {
                MainCore.Runtime.SetModEnabled(false, false, save: false);
                Info($"power: back off after the run (on={MainCore.IsModEnabled})");
            }
            Check("the run puts Quartz's power back and leaves the stored switch alone",
                MainCore.IsModEnabled == wasOn && MainCore.Conf.Active == wasActive,
                $"on={MainCore.IsModEnabled} wasOn={wasOn} active={MainCore.Conf.Active} wasActive={wasActive}");
        }
    }
    // The player may have switched off features the Facet checks open: Effect Remover's page, Otto Icon's tile switch, and
    // Visual Tweaks beside it on the Features page, and Combo, whose overlay the preview checks borrow. Turn them on for the
    // run only, in memory, and put every switch back after: Features.json is the player's and keeps its bytes.
    private static readonly string[] Needed = ["effectremover", "ottoicon", "visualtweaks", "combo"];
    private IEnumerator FeaturesOn(IEnumerator run) {
        string path = FeatureState.Path;
        byte[] file = File.Exists(path) ? File.ReadAllBytes(path) : null;
        Dictionary<string, bool> was = FeatureService.Features.ToDictionary(h => h.Id, h => h.Enabled);
        try {
            foreach(string id in Needed)
                if(!FeatureService.IsEnabled(id)) FeatureService.SetEnabledForRun(id, true);
            string on = string.Join(", ", FeatureService.Features.Where(h => h.Enabled && !was[h.Id]).Select(h => h.Id));
            Info($"features: turned on for the run: {(on == "" ? "none" : on)}");
            if(on != "") for(int f = Time.frameCount + 3; Time.frameCount < f;) yield return null;
            yield return run;
        } finally {
            foreach(FeatureService.Handle h in FeatureService.Features)
                if(h.Enabled != was[h.Id]) FeatureService.SetEnabledForRun(h.Id, was[h.Id]);
            string off = string.Join(", ", FeatureService.Features.Where(h => h.Enabled != was[h.Id]).Select(h => h.Id));
            byte[] now = File.Exists(path) ? File.ReadAllBytes(path) : null;
            Check("the run puts every feature switch back and leaves Features.json's bytes alone",
                off == "" && (file == null ? now == null : now != null && now.SequenceEqual(file)),
                $"not back={(off == "" ? "none" : off)} bytes={now?.Length ?? -1}/{file?.Length ?? -1}");
        }
    }
    // Logged, never counted: facts for a reader, not assertions.
    private void Info(string line) => log.AppendLine("INFO " + line);
    private void Check(string check, bool ok, string detail = null) {
        if(ok) pass++; else fail++;
        log.AppendLine($"{(ok ? "PASS" : "FAIL")} {check}{(detail == null ? "" : " - " + detail)}");
    }
    private IEnumerator Run() {
        bool menu = FacetBridge.MenuAvailable();
        // Why the new menu is absent: the first of Facet, PrismLib.UI and its suite shell that is missing.
        string reason = !FacetBridge.Available() ? "facet" : !PrismBridge.UiAvailable ? "prismlib-ui" : "shell";
        log.AppendLine(menu ? "MODE menu-present" : $"MODE menu-absent reason={reason}");
        // Check 1: the page is registered, and its builder runs twice here, outside
        // PageFactory.EnsureBuilt (which swallows build exceptions), so a failure reaches FAIL.
        try {
            int state = Quartz.UI.Nav.NavRegistry.StateFor("visuals.effectremover");
            Check("effectremover page is registered", state >= 0);
            for(int i = 0; i < 2; i++) {
                var host = new GameObject("FacetSelfTestPage", typeof(RectTransform)).GetComponent<RectTransform>();
                host.SetParent(UICore.Panel, false);
                try { Quartz.Features.EffectRemover.PageEffectRemover.Create(host); }
                finally { UnityEngine.Object.Destroy(host.gameObject); }
            }
            Check("effectremover page builds", true);
        } catch(Exception e) {
            Check("effectremover page builds", false, e.Message);
        }
        Check("unity 6 gate", FacetBridge.IsUnity6("6000.3.2f1") && !FacetBridge.IsUnity6("2022.3.62f1")
            && !FacetBridge.IsUnity6("") && !FacetBridge.IsUnity6(null) && FacetBridge.IsUnity6(Application.unityVersion),
            $"unity={Application.unityVersion}");
        Check("a throwing section logs one runner FAIL and the run goes on with the next check", RunnerSurvivesThrow(out string runner), runner);
        string newLog = ReadQuartzLog();
        int unavailable = CountOccurrences(newLog, "Facet unavailable");
        if(menu) {
            // Check 2: Facet loaded, exactly one assembly of that name.
            int loaded = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Facet.0.2");
            Check("facet available", true);
            Check("exactly one Facet.0.2 assembly loaded", loaded == 1, $"loaded={loaded}");
            Check("no 'Facet unavailable' logged", unavailable == 0, $"count={unavailable}");
            // Facet-naming checks live in their own class so this one never names a Facet type.
            yield return FacetSelfTestChecks.Run(Check, Info);
        } else {
            // Check 3: absent, reported exactly once, by the first missing part.
            if(reason == "facet") {
                Check("facet reported unavailable", !FacetBridge.Available());
                Check("exactly one 'Facet unavailable' log line", unavailable == 1, $"count={unavailable}");
            } else if(reason == "prismlib-ui") {
                int ui = CountOccurrences(newLog, "PrismLib.UI unavailable");
                Check("facet available", FacetBridge.Available());
                Check("PrismLib.UI reported unavailable", !PrismBridge.UiAvailable);
                Check("exactly one 'PrismLib.UI unavailable' log line", ui == 1, $"count={ui}");
                Check("no 'Facet unavailable' logged", unavailable == 0, $"count={unavailable}");
            } else {
                int noShell = CountOccurrences(newLog, "PrismLib.UI has no suite shell");
                Check("facet available", FacetBridge.Available());
                Check("the suite shell reported unavailable", PrismBridge.UiAvailable && !PrismBridge.ShellAvailable,
                    $"ui={PrismBridge.UiAvailable} shell={PrismBridge.ShellAvailable}");
                Check("exactly one 'PrismLib.UI has no suite shell' log line", noShell == 1, $"count={noShell}");
                Check("no 'Facet unavailable' logged", unavailable == 0, $"count={unavailable}");
            }
            yield return ClassicFallback();
        }
    }
    // Without the new menu the switch on changes nothing: the hotkey keeps the classic menu, and nothing offers the new one.
    private IEnumerator ClassicFallback() {
        bool was = MainCore.Conf.ExperimentalNewMenu, wasOpen = UICore.IsOpen;
        MainCore.Conf.ExperimentalNewMenu = true;
        try {
            if(wasOpen) UICore.Close(true);
            UICore.Toggle();
            for(int f = Time.frameCount + 2; Time.frameCount < f;) yield return null;
            Check("switch on without the new menu: the hotkey opens the classic menu", UICore.IsOpen && !FacetBridge.NewMenuVisible(), $"open={UICore.IsOpen}");
            Check("no new-menu switch on the Settings page", !SwitchRowOnSettingsPage());
            Check("no preview button on the menu page", !PreviewButtonOnPage());
            // Shows nothing because the gate refused, not because a caught throw turned Facet off for the session.
            bool open = UICore.IsOpen, facet = FacetBridge.Available();
            int logged = CountOccurrences(ReadQuartzLog(), "Facet unavailable");
            FacetBridge.OpenNewMenu();
            for(int f = Time.frameCount + 2; Time.frameCount < f;) yield return null;
            int loggedAfter = CountOccurrences(ReadQuartzLog(), "Facet unavailable");
            Check("OpenNewMenu shows nothing",
                !FacetBridge.NewMenuVisible() && UICore.IsOpen == open && FacetBridge.Available() == facet && loggedAfter == logged,
                $"open={open}->{UICore.IsOpen} facet={facet}->{FacetBridge.Available()} 'Facet unavailable'={logged}->{loggedAfter}");
#if !QUARTZ_KEYVIEWER
            // A PrismLib.UI with the layout switcher but no suite shell: the switcher's Edit keeps to the classic menu.
            if(FacetBridge.KvSwitcherUsable()) yield return FacetSelfTestChecks.KvSwitcherEditClassic(Check);
#endif
        } finally {
            MainCore.Conf.ExperimentalNewMenu = was;
            MainCore.ConfMgr.RequestSave();   // a save while it was on would have written it
            if(wasOpen && !UICore.IsOpen) UICore.Open(true);
            else if(!wasOpen && UICore.IsOpen) UICore.Close(true);
        }
    }
    /// <summary>The run under the marker facet-level-selftest: checks made inside a level, which it enters itself.</summary>
    internal static IEnumerator Level(Action<string, bool, string> check, Action<string> info) {
        if(FacetBridge.MenuAvailable()) yield return FacetSelfTestChecks.InLevel(check, info);
        else check("new menu available", false, null);
    }
    /// <summary>The run under the marker facet-pages-selftest: the form pages moved into the new menu.</summary>
    internal static IEnumerator Pages(Action<string, bool, string> check, Action<string> info) {
        if(FacetBridge.MenuAvailable()) yield return FacetSelfTestChecks.FormPages(check, info);
        else check("the new menu is available", false, null);
    }
    // The menu page's "open in the new menu" button, found by its label on a freshly built page.
    internal static bool PreviewButtonOnPage() {
        var host = new GameObject("FacetSelfTestButton", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(UICore.Panel, false);
        try {
            Quartz.Features.EffectRemover.PageEffectRemover.Create(host);
            return HasPreviewButton(host);
        } finally { UnityEngine.Object.Destroy(host.gameObject); }
    }
    internal static bool HasPreviewButton(Transform page) =>
        HasLabel(page, MainCore.Tr.Get("FXRM_OPEN_NEW_MENU", "Open in the new menu (preview)"));
    // The classic Settings page's new-menu switch, found by its label on a page built into a throwaway host. Here, not
    // with the Facet checks: the menu-absent run asks it too.
    internal static bool SwitchRowOnSettingsPage() {
        var host = new GameObject("FacetSelfTestSettings", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(UICore.Panel, false);
        try {
            Quartz.UI.Factory.Page.PageSettings.Create(host);
            return HasLabel(host, MainCore.Tr.Get("PRISM_NEW_MENU", "Experimental: new menu"));
        } finally {
            UnityEngine.Object.Destroy(host.gameObject);
            UICore.Rebuild();   // the build pointed the Settings page's static references at this host
        }
    }
    private static bool HasLabel(Transform page, string text) =>
        page.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Any(t => t.text == text);
    // MelonLoader's log; null-safe empty string elsewhere (UMM). The file is held open
    // by the loader, so open it shared.
    private static string ReadQuartzLog() {
        try {
            string game = Path.GetFullPath(Path.Combine(MainCore.Paths.RootPath, "..", ".."));
            string path = Path.Combine(game, "MelonLoader", "Latest.log");
            if(!File.Exists(path)) return "";
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        } catch(Exception e) { Diag.Ignore(e); return ""; }
    }
    private static int CountOccurrences(string text, string needle) {
        int n = 0, i = 0;
        while((i = text.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
#endif
