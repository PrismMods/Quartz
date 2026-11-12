#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Features.KeyViewer;
using Quartz.Overlay;
using Quartz.UI.Nav;
using Quartz.UI.Search;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Key Viewer in the new menu: its build time, its rows against the classic page's settings, its two columns' edges,
/// the note rain switch leaving the classic page stale, the editor card's way to the classic menu and back, search keeping the
/// editor's rows as classic hits, and the preview pane, which borrows the live key viewer.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string KeyViewerPage = "overlay.keyviewer";
    private static KeyViewerSettings KvConf => KeyViewerOverlay.Conf;

    // The classic page's settings rows, with its three status lines: the Key Limiter sync only while the Key Limiter is loaded,
    // and a switch per plugin.
    private static string[] KeyViewerRows() => [.. new[] {
        "KEYVIEWER_SHOWOUTSIDE", "KEYVIEWER_SYNCLIMITER", "FONT_OVERLAY",
        "KEYVIEWER_DM_NOTE", "KEYVIEWER_DM_REVERSE", "KEYVIEWER_DM_COUNTER", "KEYVIEWER_DM_SCALE", "KEYVIEWER_DM_FOOT_SCALE", "KEYVIEWER_DM_SPEED",
        "KEYVIEWER_DM_TRACK", "KEYVIEWER_DM_FADE", "KEYVIEWER_DM_DELAY_ENABLED", "KEYVIEWER_DM_SHORT_THRESHOLD", "KEYVIEWER_DM_KEY_DELAY",
        "KEYVIEWER_DM_INDEPENDENT_INPUT", "KEYVIEWER_DM_MIN_LIT",
        "KEYVIEWER_GHOSTRAINDOTTED", "KEYVIEWER_GHOSTRAINDOTLENGTH", "KEYVIEWER_GHOSTRAINGAPLENGTH",
        "KEYVIEWER_TABUB", "KEYVIEWER_TABUB_PERCENT", "KEYVIEWER_TABUB_SCALE", "KEYVIEWER_TABUB_IMAGE_STATUS", "KEYVIEWER_TABUB_IMAGE",
        "KEYVIEWER_TABUB_IMAGE_CLEAR", "KEYVIEWER_TABUB_RESETPOS",
        "KEYVIEWER_DM_CSS_STATUS", "KEYVIEWER_DM_CSS_ENABLED", "KEYVIEWER_DM_CSS_IMPORT", "KEYVIEWER_DM_CSS_CLEAR",
        "KEYVIEWER_JS_STATUS", "KEYVIEWER_JS_ENABLED", "KEYVIEWER_JS_IMPORT", "KEYVIEWER_JS_RELOAD", "KEYVIEWER_JS_CLEAR",
        "KEYVIEWER_ANCHOR_LABEL", "KEYVIEWER_RESETPOS", "KEYVIEWER_RESETCOUNTS",
    }.Where(k => k != "KEYVIEWER_SYNCLIMITER" || KvKeyLimiterBridge.Available), .. KvConf.JsPlugins.Select(p => Quartz.UI.Generator.GenerateUI.LocaleKeyFromId("keyviewer_js_plugin_" + p.PluginId))];

    private static IEnumerator OverlayKeyViewer(Action<string, bool, string> check, Action<string> info) {
        KeyViewerOverlay.EnsureConf();
        KeyViewerSettings c = KvConf;
        (bool tabub, bool dotted, float scale) = (c.TabubEnabled, c.GhostRainDotted, c.DmScale);
        // A plugin whose id has a "-", off and never loaded: its switch must carry the classic toggle's key, where it is "_".
        var plugin = new Quartz.Features.KeyViewer.Js.KvJsPluginRecord { Name = "Self Test Plugin", Enabled = false };
        yield return OnPage("keyviewer", KeyViewerPage, () => {
            (c.TabubEnabled, c.GhostRainDotted) = (true, true);   // the rows they hide show for the parity check
            c.JsPlugins.Add(plugin);
            KeyViewerOverlay.Apply();
        }, () => {
            c.JsPlugins.Remove(plugin);
            (c.TabubEnabled, c.GhostRainDotted, c.DmScale) = (tabub, dotted, scale);
            KeyViewerOverlay.Apply();
            KeyViewerOverlay.Save();
        }, () => KeyViewerChecks(check, info, scale));
    }

    private static IEnumerator KeyViewerChecks(Action<string, bool, string> check, Action<string> info, float scale) {
        yield return KeyViewerBuild(info);
        yield return Parity(check, KeyViewerPage, KeyViewerRows());
        yield return KeyViewerOffRows(check);
        yield return ColumnEdges(check, KeyViewerPage, KeyViewerOverlay.FontKey);
        yield return ClassicStale(check, KeyViewerPage, "KEYVIEWER_DM_NOTE", Bind.To(() => KvConf.DmNoteEffect, v => {
            KvConf.DmNoteEffect = v;
            KeyViewerOverlay.Rebuild();
            KeyViewerOverlay.Save();
        }));
        yield return EditorLink(check, KeyViewerPage, "MENU_KV_EDITOR");
        KeyViewerSearch(check);
        SearchButtonRow(check, KeyViewerPage, "KEYVIEWER_RESETCOUNTS");
        yield return KeyViewerLights(check);
        // Through the row's own slider: the scale half a step up (down near the top), read off the hand's root.
        float want = scale + 0.5f <= 4f ? scale + 0.5f : scale - 0.5f;
        bool SetScale() {
            if(Body.Q<Slider>("KEYVIEWER_DM_SCALE-input") is not { } s) return false;
            s.value = (want - 0.2f) / 3.8f;   // the track runs 0 to 1 over the row's 0.2 to 4
            return true;
        }
        yield return PreviewChecks(check, info, KeyViewerPage, KeyViewerOverlay.FontKey, SetScale,
            () => OverlayPreview.ShownOf(KeyViewerOverlay.FontKey) is { } root && Mathf.Abs(root.transform.localScale.x - want) < 0.005f);
    }

    // Tabub and dotted rain switched off through their own switches hide the rows they gate, and only those; on again after.
    private static IEnumerator KeyViewerOffRows(Action<string, bool, string> check) {
        string[] switches = ["KEYVIEWER_TABUB", "KEYVIEWER_GHOSTRAINDOTTED"];
        string[] gated = ["KEYVIEWER_TABUB_PERCENT", "KEYVIEWER_TABUB_SCALE", "KEYVIEWER_TABUB_IMAGE_STATUS", "KEYVIEWER_TABUB_IMAGE",
            "KEYVIEWER_TABUB_IMAGE_CLEAR", "KEYVIEWER_TABUB_RESETPOS", "KEYVIEWER_GHOSTRAINDOTLENGTH", "KEYVIEWER_GHOSTRAINGAPLENGTH"];
        void Flip() {
            foreach(string s in switches) if(Body.Q(name: s + "-input") is { } track) Click(track);
            QuartzShell.Surface?.Refresh();   // VisibleWhen is asked now, not on the next refresh
        }
        Flip();
        try {
            yield return Parity(check, KeyViewerPage, [.. KeyViewerRows().Except(gated)], gated);
        } finally {
            Flip();
        }
        yield return Frames(1);
    }

    // The page's build from another category, as a first open landing on it pays: logged, not held to the first-open budget,
    // which is read on Home.
    private static IEnumerator KeyViewerBuild(Action<string> info) {
        QuartzShell.Go(CorePages.HomePageKey);
        yield return Frames(2);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        QuartzShell.Go(KeyViewerPage);
        info($"[perf] Key Viewer page build {clock.Elapsed.TotalMilliseconds:0.0} ms (page={QuartzShell.CurrentPage})");
        yield return Frames(3);
    }

    // A key the Key Viewer polls (the async hook off) lights its box in the pane while the new menu holds keys from the game.
    // Show Outside Gameplay is off, so outside a level the Key Viewer shows for the pane alone: the press counts nowhere.
    // The press is synthetic: Input.GetKey reads it as held, and the gate still has its say. The counts go back after anyway.
    private static IEnumerator KeyViewerLights(Action<string, bool, string> check) {
        for(int i = 0; i < 10 && !OverlayPreview.Is(KeyViewerOverlay.FontKey); i++) yield return null;   // back from the search
        Type t = typeof(KeyViewerOverlay), box = t.GetNestedType("Box", BindingFlags.NonPublic);
        var boxes = (IList)t.GetField("boxes", Static).GetValue(null);
        FieldInfo count = box.GetField("Count"), boxKey = box.GetField("Key"), isFoot = box.GetField("IsFoot");
        PropertyInfo isStat = box.GetProperty("IsStat");
        FieldInfo[] stats = [.. new[] { "totalCount", "countsDirty", "nextCountsSave" }.Select(n => t.GetField(n, Static))];
        object[] counts = [.. boxes.Cast<object>().Select(count.GetValue)], saved = [.. stats.Select(f => f.GetValue(null))];
        KeyCode key = boxes.Cast<object>().Where(b => !(bool)isStat.GetValue(b) && !(bool)isFoot.GetValue(b)).Select(b => (KeyCode)boxKey.GetValue(b))
            .FirstOrDefault(k => k != KeyCode.None && !Quartz.Game.HookInput.TryMacPhysicalKeyHeld(k, out _));   // not read off the window server
        bool independent = KvConf.IndependentInput, outside = KvConf.ShowOutsideGame, lit = false, blocking = FacetBridge.BlocksGameInput();
        bool shown = OverlayPreview.Is(KeyViewerOverlay.FontKey), level = Quartz.Game.Stats.GameStats.InGame;
        void Lit(KeyViewerOverlay.KeyPressChangedEventArgs e) => lit |= e.Key == key && e.Pressed;
        int frames = 0;
        (KvConf.IndependentInput, KvConf.ShowOutsideGame) = (false, false);
        KeyViewerOverlay.OnKeyPressChanged += Lit;
        try {
            HoldKey(key);
            for(; frames < 30 && !lit; frames++) yield return null;
        } finally {
            HoldKey(KeyCode.None);
            KeyViewerOverlay.OnKeyPressChanged -= Lit;
            (KvConf.IndependentInput, KvConf.ShowOutsideGame) = (independent, outside);
        }
        yield return Frames(3);   // released
        int added = boxes.Cast<object>().Select((b, i) => (int)count.GetValue(b) - (i < counts.Length ? (int)counts[i] : 0)).Sum();
        int total = (int)stats[0].GetValue(null) - (int)saved[0];
        bool flushed = (added != 0 || total != 0) && !(bool)stats[1].GetValue(null);   // saved with a press counted: save again once put back
        for(int i = 0; i < counts.Length && i < boxes.Count; i++) count.SetValue(boxes[i], counts[i]);
        for(int i = 0; i < stats.Length; i++) stats[i].SetValue(null, saved[i]);
        if(flushed) stats[1].SetValue(null, true);
        check($"{KeyViewerPage}: a key the Key Viewer polls lights its box in the pane while the new menu holds keys from the game, and with Show "
            + "Outside Gameplay off adds to no stored count",
            key != KeyCode.None && blocking && shown && !level && lit && added == 0 && total == 0,
            $"key={key} blocking={blocking} shown={shown} level={level} lit={lit} frames={frames} added={added} total={total} focused={Application.isFocused} "
            + $"flushed={flushed}");
    }

    // Input.GetKey(heldKey) reads as held until it is set back to None. Patched on first use, inert after.
    private static KeyCode heldKey;
    private static HarmonyLib.Harmony heldProbe;

    private static void HoldKey(KeyCode key) {
        if(heldProbe == null) {
            heldProbe = new HarmonyLib.Harmony("Quartz.FacetSelfTest.Held");
            heldProbe.Patch(typeof(UnityEngine.Input).GetMethod(nameof(UnityEngine.Input.GetKey), [typeof(KeyCode)]),
                prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(ProbeKeyHeld), Static)));
        }
        heldKey = key;
    }

    private static bool ProbeKeyHeld(KeyCode __0, ref bool __result) {
        if(__0 == KeyCode.None || __0 != heldKey) return true;
        __result = true;
        return false;
    }

    // The page is moved in part: a row of the classic page this page has no row for (its editor's) is still a classic hit, and a
    // row both have, or the classic "Use this feature" (the header switch here), is found on this page only.
    private static void KeyViewerSearch(Action<string, bool, string> check) {
        int state = NavRegistry.StateFor(KeyViewerPage);
        NavPage page = NavRegistry.ByKey(KeyViewerPage);
        List<(string Key, string English, int State)> editor = [.. SettingsSearch.Rows().Where(r => r.State == state && r.Key != null
            && r.Key != page?.LocaleKey && r.Key != "FEATURE_PAGE_ENABLE" && !string.IsNullOrEmpty(r.English)
            && Body.Q(name: r.Key) == null && Body.Q(name: r.Key + "-input") == null)];
        var (key, english, _) = editor.FirstOrDefault();
        bool Classic(List<SearchIndex.Hit> hits, string k, string en) => hits.Any(h => h.PageRef == null && h.Label == QuartzHost.Host.Translate(QuartzShell.Label(k, en)));
        try {
            List<SearchIndex.Hit> left = key == null ? [] : Find(english);
            bool classic = key != null && Classic(left, key, english);
            List<SearchIndex.Hit> moved = Find(QuartzHost.Host.Translate("KEYVIEWER_DM_NOTE")), feature = Find("use this feature");
            bool own = moved.Any(h => h.PageRef != null && h.Key == "KEYVIEWER_DM_NOTE"), twice = Classic(moved, "KEYVIEWER_DM_NOTE", "Note Rain");
            bool switchTwice = feature.Any(h => h.PageRef == null && h.Page == QuartzShell.Label(page?.LocaleKey, page?.Title));
            check($"{KeyViewerPage}: search keeps a row left to the classic editor as a classic hit, and finds a moved row and the feature switch on this page only",
                classic && own && !twice && !switchTwice,
                $"editor rows={editor.Count} left={key}/'{english}' classic={classic} {Hits(left)}; moved own={own} classic={twice} {Hits(moved)}; "
                + $"feature switch classic={switchTwice}");
        } finally {
            QuartzShell.Nav?.Filter("");
        }
    }
}
#endif
