#if DEBUG && !IL2CPP
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using Quartz.Compat.Interface;
using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Debug-only in-game self-test, driven by the file UserData/Quartz/facet-selftest.
/// Writes PASS/FAIL lines to facet-selftest.log, ends with "DONE pass=N fail=M", then
/// quits the game. Names no Facet type; Facet-dependent checks live in FacetBridge.
/// </summary>
public sealed class FacetSelfTest : IRuntimeTick {
    private const float StartAfterSeconds = 25f;
    private bool done;
    private int pass, fail;
    private StringBuilder log;
    // Checks that span frames are iterators; a yielded IEnumerator runs to completion first.
    private readonly Stack<IEnumerator> steps = new();
    public void Tick() {
        if(done || Time.realtimeSinceStartup < StartAfterSeconds) return;
        if(steps.Count == 0) {
            string marker = Path.Combine(MainCore.Paths.RootPath, "facet-selftest");
            if(!File.Exists(marker)) { done = true; return; }
            log = new StringBuilder();
            steps.Push(Run());
        }
        while(steps.Count > 0) {
            IEnumerator top = steps.Peek();
            bool more;
            try {
                more = top.MoveNext();
            } catch(Exception e) {
                Check("runner", false, e.ToString());
                // Dispose top first so each iterator's finally (state restores) runs.
                while(steps.Count > 0) {
                    try { (steps.Pop() as IDisposable)?.Dispose(); } catch(Exception d) { Diag.Ignore(d); }
                }
                break;
            }
            if(!more) { steps.Pop(); continue; }
            if(top.Current is IEnumerator inner) { steps.Push(inner); continue; }
            return; // wait a frame
        }
        done = true;
        log.AppendLine($"DONE pass={pass} fail={fail}");
        File.WriteAllText(Path.Combine(MainCore.Paths.RootPath, "facet-selftest.log"), log.ToString());
        Application.Quit();
    }
    // Logged, never counted: facts for a reader, not assertions.
    private void Info(string line) => log.AppendLine("INFO " + line);
    private void Check(string name, bool ok, string detail = null) {
        if(ok) pass++; else fail++;
        log.AppendLine($"{(ok ? "PASS" : "FAIL")} {name}{(detail == null ? "" : " - " + detail)}");
    }
    private IEnumerator Run() {
        bool facet = FacetBridge.Available();
        log.AppendLine($"MODE {(facet ? "facet-present" : "facet-absent")}");
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
        string newLog = ReadQuartzLog();
        int unavailable = CountOccurrences(newLog, "Facet unavailable");
        if(facet) {
            // Check 2: Facet loaded, exactly one assembly of that name.
            int loaded = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Facet.0.1");
            Check("facet available", true);
            Check("exactly one Facet.0.1 assembly loaded", loaded == 1, $"loaded={loaded}");
            Check("no 'Facet unavailable' logged", unavailable == 0, $"count={unavailable}");
            // Facet-naming checks live in their own class so this one never names a Facet type.
            yield return FacetSelfTestChecks.Run(Check, Info);
        } else {
            // Check 3: absent, reported exactly once after two page builds.
            Check("facet reported unavailable", !FacetBridge.Available());
            Check("exactly one 'Facet unavailable' log line", unavailable == 1, $"count={unavailable}");
            Check("no preview button on the menu page", !PreviewButtonOnPage());
        }
    }
    // The menu page's "open in the new menu" button, found by its label on a freshly built page.
    internal static bool PreviewButtonOnPage() {
        var host = new GameObject("FacetSelfTestButton", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(UICore.Panel, false);
        try {
            Quartz.Features.EffectRemover.PageEffectRemover.Create(host);
            string label = MainCore.Tr.Get("FXRM_OPEN_NEW_MENU", "Open in the new menu (preview)");
            return host.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Any(t => t.text == label);
        } finally { UnityEngine.Object.Destroy(host.gameObject); }
    }
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
