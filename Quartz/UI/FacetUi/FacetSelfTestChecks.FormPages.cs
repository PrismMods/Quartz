#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.KeyLimiter;
using Quartz.Features.Tuf;
using Quartz.Game;
using Quartz.IO;
using Quartz.IO.Interface;
using Quartz.Resource;
using Quartz.UI.Nav;
using Quartz.UI.Search;
using Quartz.UI.Utility;
using Quartz.Update;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The facet-pages-selftest run: the form pages built for the search index do no work, the moved pages' header hotkey,
/// then one section per category. The helpers below check a page against the classic page it replaces.
/// </summary>
internal static partial class FacetSelfTestChecks {
    // The form pages moved so far, by page key; each page's section adds its own. FormPage fails for a page not listed here.
    private static readonly List<string> FormPageKeys = ["overlay.combo", "visuals.hidejudgements", "visuals.tilearc", "visuals.visualtweaks",
        "visuals.planetcolors", "visuals.ottoicon", "visuals.uihiding", "gameplay.chatter", "gameplay.countdown", "gameplay.death", "gameplay.autodeafen",
        "gameplay.keylimiter", "gameplay.judgement", "gameplay.practice", "tweaks.general", "tweaks.specialdays", "tweaks.optimizer", "tweaks.mainmenu",
        "editor.tilereadout", "editor.bga", "editor.fliprotate", "editor.decopreview", "tuf.settings", CorePages.SettingsPageKey];

    internal static IEnumerator FormPages(Action<string, bool, string> check, Action<string> info) {
        // Every window opened here writes its geometry into the live settings: put the map back.
        var windows = new Dictionary<string, string>(MainCore.Conf.FacetWindows);
        formInfo = info;
        formCheck = check;
        try {
            yield return ThrowawayBuilds(check, info);
            yield return FormPage("combo", "overlay.combo", () => { }, () => { },
                () => HeaderHotkeyCheck(check, "overlay.combo", "SECTION_COMBO", "combo"));
            yield return VisualsPages(check);
            yield return GameplayForms(check);
            yield return GameplayLists(check, info);
            yield return TweaksEditor(check);
            yield return TufSettings(check, info);
            yield return SettingsUpdatesFont(check, info);
            IndexPerf(info);
        } finally {
            MainCore.Conf.FacetWindows.Clear();
            foreach(var kv in windows) MainCore.Conf.FacetWindows[kv.Key] = kv.Value;
            MainCore.ConfMgr.RequestSave();
        }
    }

    /// <summary>OnPage, with the page's category on for the run when the player has it off (in memory, put back exactly and
    /// saved last), and the page's first build timed. category, when given, is the one that shows the page's own.</summary>
    private static IEnumerator FormPage(string featureId, string pageKey, Action show, Action restore, Func<IEnumerator> checks,
        string category = null) {
        category ??= NavRegistry.ByKey(pageKey)?.CategoryKey;
        bool was = false, turned = false, had = category != null && FeatureService.State.Categories.TryGetValue(category, out was);
        Action<VisualElement> build = QuartzShell.Moved.TryGetValue(pageKey, out Action<VisualElement> b) ? b : null;
        float ms = -1f;
        // The detached builds below guard what the search index does with this page's builder.
        formCheck($"{pageKey}: the detached builds cover it", FormPageKeys.Contains(pageKey), $"listed={string.Join(",", FormPageKeys)}");
        try {
            if(category != null && !FeatureCategories.IsEnabled(category)) {
                FeatureService.State.Categories[category] = true;
                turned = true;
                QuartzShell.Changed();
            }
            // The first build into the menu's window is the page's own as the menu opens on it; the search index's are detached.
            if(build != null) QuartzShell.Moved[pageKey] = p => {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                build(p);
                if(ms < 0f && QuartzShell.Window?.Body.Contains(p) == true) ms = (float)clock.Elapsed.TotalMilliseconds;
            };
            yield return OnPage(featureId, pageKey, show, restore, checks);
        } finally {
            if(build != null) QuartzShell.Moved[pageKey] = build;
            if(turned) {
                if(had) FeatureService.State.Categories[category] = was;
                else FeatureService.State.Categories.Remove(category);
                FeatureService.State.Save();
                QuartzShell.Changed();
            }
        }
        if(ms >= 0f) formInfo($"[perf] page {pageKey} first build: {ms:0.0} ms");
        else formCheck($"{pageKey}: the menu builds it from its moved builder", false, $"moved={build != null}");
    }

    /// <summary>FormPage for a feature's settings page: its file's values are kept before show and put back after, saved at
    /// once, then apply runs. The rows' keys are checked against the classic page; searched less its headers, cards and
    /// sections.</summary>
    private static IEnumerator FeatureFormPage<T>(Action<string, bool, string> check, string featureId, string pageKey, SettingsFile<T> file,
        Action show, Action apply, string[] keys, string[] searched, Func<IEnumerator> more, string[] hidden = null, string category = null)
        where T : class, ISettingsFile, new() {
        JToken was = null;
        yield return FormPage(featureId, pageKey, () => {
            was = file.Data.Serialize();
            show?.Invoke();
        }, () => {
            if(was != null) file.Data.Deserialize(was);
            file.Save();   // drops a pending save too
            apply?.Invoke();
        }, () => Checks(), category);
        IEnumerator Checks() {
            yield return Parity(check, pageKey, keys, hidden);
            Covers(check, pageKey);
            Searched(check, pageKey, searched);
            Translated(check, pageKey, keys);
            if(more != null) yield return more();
        }
    }

    // The Key row whose input is button, from its surface's refresher, which holds every row's Refresh.
    private static Control<string> KeyRow(VisualElement button) {
        if(button == null) return null;
        Type refresher = typeof(Controls).Assembly.GetType("Facet.Refresher");
        object of = refresher?.GetMethod("Of", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, [button]);
        var rows = refresher?.GetField("_controls", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(of) as IEnumerable<KeyValuePair<string, Action>>;
        return rows?.Select(r => r.Value?.Target).OfType<Control<string>>().FirstOrDefault(c => c.Input == button);
    }

    // A value set on a slider's track as a player's keyboard step sets it: a whole change, committed at once.
    private static bool SlideTo(string id, float min, float max, float v) {
        Slider s = PageContent().Q<Slider>(id + "-input");
        if(s != null) s.value = (v - min) / (max - min);
        return s != null;
    }

    // A whole value 10 from v, kept inside 0 to 100.
    private static float Away(float v) => Mathf.Round(v) >= 50f ? Mathf.Round(v) - 10f : Mathf.Round(v) + 10f;

    private static bool ClickRow(string id) {
        VisualElement track = PageContent().Q(name: id + "-input");
        if(track != null) Click(track);
        return track != null;
    }

    // The run's INFO and check lines, for FormPage.
    private static Action<string> formInfo;
    private static Action<string, bool, string> formCheck;

    /// <summary>Every row the classic page indexes names an element of the open page: its key, its -input, or key#n for the
    /// n-th of a key the classic page lists more than once. renamed maps a classic key to the new Id; omitted keys are skipped
    /// (each caller says why); only, when given, limits the keys.</summary>
    private static void Covers(Action<string, bool, string> check, string pageKey, IReadOnlyDictionary<string, string> renamed = null,
        IReadOnlyCollection<string> omitted = null, IReadOnlyCollection<string> only = null) {
        NavPage page = NavRegistry.ByKey(pageKey);
        VisualElement root = PageContent();
        var missing = new List<string>();
        int rows = 0;
        if(page != null)
            foreach(IGrouping<string, string> key in SettingsSearch.Rows()
                .Where(r => r.State == page.State && r.Key != null && r.Key != page.LocaleKey && (only == null || only.Contains(r.Key)))
                .Select(r => r.Key).GroupBy(k => k)) {
                if(omitted?.Contains(key.Key) == true) continue;
                // The classic page's "Use this feature" is the moved page's header switch: the input of the header named by the
                // page's title key, so a header without its switch is missing.
                string id = renamed != null && renamed.TryGetValue(key.Key, out string to) ? to
                    : key.Key == "FEATURE_PAGE_ENABLE" ? page.LocaleKey + "-input" : key.Key;
                int n = key.Count();
                rows += n;
                for(int i = 1; i <= n; i++)
                    if(!Names(root, id + "#" + i) && !(i == 1 && Names(root, id))) missing.Add(i == 1 ? id : id + "#" + i);
            }
        check($"{pageKey}: has a row for every row the classic page indexes", page != null && rows > 0 && missing.Count == 0,
            $"page={page != null} rows={rows} missing={string.Join(",", missing)}");
    }

    private static bool Names(VisualElement root, string name) => root.Q(name: name) != null || root.Q(name: name + "-input") != null;

    /// <summary>Each key's English text finds its row on this page in the new menu's search.</summary>
    private static void Searched(Action<string, bool, string> check, string pageKey, IEnumerable<string> keys) {
        NavPage page = NavRegistry.ByKey(pageKey);
        string title = page == null ? null : QuartzShell.Label(page.LocaleKey, page.Title);
        var missed = new List<string>();
        string first = "";
        try {
            foreach(string key in keys) {
                List<SearchIndex.Hit> hits = Find(MainCore.Tr.GetForLanguage(key, "en-US", key));
                // A page in a category with tabs is a tab: its title is the hit's Tab, its category the NavItem.
                if(!hits.Any(h => h.PageRef is NavItem item && (h.Tab ?? item.Title) == title && Unnumbered(h.Key) == key)) missed.Add(key);
                if(missed.Count == 1 && first.Length == 0) first = " first: " + Hits(hits);
            }
        } finally {
            QuartzShell.Nav?.Filter("");
        }
        check($"{pageKey}: search finds each of its rows on this page", title != null && missed.Count == 0,
            $"missed={string.Join(",", missed)}{first}");
    }

    private static string Unnumbered(string key) {
        int at = key?.LastIndexOf('#') ?? -1;
        return at < 0 ? key : key![..at];
    }

    private static void Translated(Action<string, bool, string> check, string pageKey, IEnumerable<string> keys) {
        string[] missing = [.. keys.Where(k => !MainCore.Tr.HasKeyForLanguage(k, "ko-KR"))];
        check($"{pageKey}: every row has Korean text", missing.Length == 0, $"missing={string.Join(",", missing)}");
    }

    /// <summary>write acts as a player would; within 2 s of real time the setting reads as written (wrote) and the file's
    /// bytes differ from before. The page's restore puts the value back and saves synchronously.</summary>
    private static IEnumerator Saved(Action<string, bool, string> check, string pageKey, string rowId, Func<bool> write, Func<bool> wrote, string file) {
        bool did = false;
        return Saved(check, pageKey, rowId, () => Act(), wrote, file, () => did);
        IEnumerator Act() {
            did = write();
            yield break;
        }
    }

    /// <summary>Saved for a write that takes frames, such as an item picked in a dropdown; did says it acted (default yes).</summary>
    private static IEnumerator Saved(Action<string, bool, string> check, string pageKey, string rowId, Func<IEnumerator> write, Func<bool> wrote,
        string file, Func<bool> did = null) {
        byte[] before = Bytes(file);
        yield return write();
        bool acted = did?.Invoke() ?? true, set = false, changed = false;
        for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 2f;) {
            yield return null;
            set = wrote();
            changed = !SameBytes(Bytes(file), before);
            if(set && changed) break;
        }
        check($"{pageKey} {rowId}: a write here reaches the setting and its file", acted && set && changed,
            $"write={acted} setting={set} file changed={changed} ({file})");
    }

    private static byte[] Bytes(string path) => path != null && File.Exists(path) ? File.ReadAllBytes(path) : null;

    private static bool SameBytes(byte[] a, byte[] b) => a == null ? b == null : b != null && a.SequenceEqual(b);

    /// <summary>A click on a tile flips its bit alone and undo puts it back; the card's All, then None, change only the shown
    /// tiles' bits, each one undo step. The stray bits show seeded stay.</summary>
    private static IEnumerator MaskFlip(Action<string, bool, string> check, string pageKey, string tileId, Func<int> mask, int bit, int[] shownBits) {
        VisualElement tile = PageContent().Q(name: tileId + "-input");
        int was = mask();
        if(tile != null) Click(tile);
        yield return Frames(2);
        int clicked = mask();
        bool undid = global::Facet.History.Undo();
        yield return Frames(2);
        check($"{pageKey} {tileId}: a click flips its bit alone, and undo puts it back", tile != null && (clicked ^ was) == 1 << bit && undid && mask() == was,
            $"tile={tile != null} was={was:x} clicked={clicked:x} undone={mask():x}");
        int shown = shownBits.Aggregate(0, (m, b) => m | 1 << b), start = mask();
        Button all = CardLink(tile, "MENU_ALL"), none = CardLink(tile, "MENU_NONE");
        if(all != null) Submit(all);
        yield return Frames(2);
        int afterAll = mask();
        if(none != null) Submit(none);
        yield return Frames(2);
        int afterNone = mask();
        bool back1 = global::Facet.History.Undo();
        yield return Frames(2);
        int undoNone = mask();
        bool back2 = global::Facet.History.Undo();
        yield return Frames(2);
        check($"{pageKey}: All and None set the shown tiles' bits only, one undo step each",
            all != null && none != null && afterAll == (start | shown) && afterNone == (start & ~shown) && back1 && undoNone == afterAll && back2 && mask() == start,
            $"links={all != null}/{none != null} start={start:x} all={afterAll:x} none={afterNone:x} undo={undoNone:x} undo2={mask():x} shown={shown:x}");
    }

    // A card link (an Actions entry) in the card around e, found by its text.
    private static Button CardLink(VisualElement e, string key) {
        string text = QuartzHost.Host.Translate(key);
        for(; e != null; e = e.parent)
            if(e.Query<Button>().Where(b => b.text == text).First() is { } link) return link;
        return null;
    }

    /// <summary>The header switch binds feature_featureId from its right-click menu: the key captured turns the feature off
    /// (the page is its off page) and on again (the moved page is back), and Clear removes the bind. The player's bind and the
    /// feature are put back.</summary>
    private static IEnumerator HeaderHotkeyCheck(Action<string, bool, string> check, string pageKey, string headerId, string featureId) {
        string id = "feature_" + featureId, tag = $"{pageKey}: the header switch's hotkey is feature_{featureId}'s, set and cleared from its right-click menu";
        KeyCode key = new[] { KeyCode.F13, KeyCode.F14, KeyCode.F15 }.FirstOrDefault(k => (int)k != MainCore.Conf.ToggleKey
            && !MainCore.Conf.ToggleKeybinds.Any(b => b.Key != id && b.Value.Key == (int)k));
        VisualElement head = Body.Q(name: headerId);
        if(head == null || key == KeyCode.None) {
            check(tag, false, $"header={head != null}{(key == KeyCode.None ? " F13-F15 all bound by the player" : "")}");
            yield break;
        }
        bool wasOn = FeatureService.IsEnabled(featureId), had = ToggleBinds.TryGet(id, out Keybind.KeyModifier hadMod, out KeyCode hadKey);
        if(had) ShellRows.ClearHotkey(id);
        string set = QuartzHost.Host.Translate("MENU_SET_HOTKEY"), clear = QuartzHost.Host.Translate("MENU_CLEAR_HOTKEY");
        try {
            Hover(head);
            RightClick(head);
            yield return Frames(2);
            VisualElement setRow = MenuRow(set);
            if(setRow != null) Pick(setRow);
            yield return Frames(1);
            bool capturing = KeyCaptureService.IsCapturing;
            typeof(KeyCaptureService).GetMethod("End", Static).Invoke(null, [key]);   // as the capture runner ends it
            yield return Frames(2);
            bool bound = ToggleBinds.TryGet(id, out Keybind.KeyModifier mod, out KeyCode got) && got == key && mod == Keybind.KeyModifier.None;
            bool off = false, on = false;
            PressNextFrame(key);
            for(float t0 = Time.realtimeSinceStartup; !off && Time.realtimeSinceStartup - t0 < 1f;) {
                yield return null;
                // Off, the feature's pages are its off page, registered under a key of its own.
                NavPage now = NavRegistry.ByKey(QuartzShell.CurrentPage);
                off = !FeatureService.IsEnabled(featureId) && FeaturePages.IsStub(now) && FeaturePages.FeatureOf(now) == featureId
                    && Body.Q(name: headerId) == null && Body.Q(name: "menu-header") != null;
            }
            string offPage = QuartzShell.CurrentPage;
            PressNextFrame(key);
            for(float t0 = Time.realtimeSinceStartup; !on && Time.realtimeSinceStartup - t0 < 1f;) {
                yield return null;
                // Back on, the shell shows the feature's first page: pageKey itself unless the feature has several. A moved
                // first page shows its header; a classic one (TUF's level browser) the classic card.
                NavPage now = NavRegistry.ByKey(QuartzShell.CurrentPage);
                on = FeatureService.IsEnabled(featureId) && !FeaturePages.IsStub(now) && FeaturePages.FeatureOf(now) == featureId
                    && Body.Q(name: QuartzShell.Moved.ContainsKey(now.Key) ? now.LocaleKey : "menu-classic") != null;
            }
            string onPage = QuartzShell.CurrentPage;
            if(on && onPage != pageKey) {
                QuartzShell.Go(pageKey);
                yield return Frames(2);
            }
            head = Body.Q(name: headerId);
            if(head != null) {
                Hover(head);
                RightClick(head);
            }
            yield return Frames(2);
            VisualElement clearRow = MenuRow(clear);
            if(clearRow != null) Pick(clearRow);
            yield return Frames(1);
            bool cleared = !ToggleBinds.TryGet(id, out _, out _);
            check(tag, setRow != null && capturing && bound && off && on && clearRow != null && cleared,
                $"set={setRow != null} capturing={capturing} bound={got}/{mod} off={off} offPage={offPage} on={on} onPage={onPage} clear={clearRow != null} cleared={cleared}");
        } finally {
            KeyCaptureService.Cancel();
            Popup.CloseAll();
            if(had) ShellRows.SetHotkey(id, hadMod, hadKey);
            else if(ToggleBinds.TryGet(id, out _, out _)) ShellRows.ClearHotkey(id);
            if(FeatureService.IsEnabled(featureId) != wasOn) FeatureService.SetEnabled(featureId, wasOn);
        }
        yield return Frames(2);
    }

    /// <summary>Each form page's builder, run twice into a detached element as the search index runs it, changes no setting in
    /// memory or on disk, subscribes to no event and never measures the TUF library. With no page listed, each check fails.</summary>
    private static IEnumerator ThrowawayBuilds(Action<string, bool, string> check, Action<string> info) {
        int count = FormPageKeys.Count;
        CountScans();
        Dictionary<string, (string Memory, byte[] Disk)> before = SettingsReadings();
        int[] listeners = Listeners(), effects = SideEffectReadings(), door = DoorReadings();
        int scansBefore = scans;
        var done = new List<string>();
        Func<string, bool> doing = QuartzShell.Doing;
        QuartzShell.Doing = n => { done.Add(n); return true; };
        try {
            foreach(string key in FormPageKeys)
                for(int i = 0; i < 2; i++)
                    if(QuartzShell.Moved.TryGetValue(key, out Action<VisualElement> build)) build(new VisualElement());
        } finally {
            QuartzShell.Doing = doing;
        }
        Dictionary<string, (string Memory, byte[] Disk)> built = SettingsReadings();
        int[] listenersAfter = Listeners(), effectsAfter = SideEffectReadings(), doorAfter = DoorReadings();
        int scanned = scans - scansBefore;
        for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1.5f;) yield return null;   // past the save debounce
        Dictionary<string, (string Memory, byte[] Disk)> settled = SettingsReadings();
        var changed = new List<string>();
        foreach(var (path, now) in built) {
            // A handle a first EnsureConf registered during the builds: its file is compared on disk, from then to now.
            bool known = before.TryGetValue(path, out var was);
            if(known && was.Memory != now.Memory) changed.Add(Path.GetFileName(path) + " (memory)");
            byte[] disk = settled.TryGetValue(path, out var later) ? later.Disk : null;
            if(!SameBytes(known ? was.Disk : now.Disk, disk)) changed.Add(Path.GetFileName(path) + " (disk)");
        }
        check($"every form page builds detached without changing a setting in memory or on disk ({count})", count > 0 && changed.Count == 0,
            $"changed={string.Join(",", changed)}");
        check($"every form page builds detached without subscribing to an event ({count})", count > 0 && listeners.SequenceEqual(listenersAfter),
            $"listeners={string.Join("/", listeners)} -> {string.Join("/", listenersAfter)}");
        check($"every form page builds detached without measuring the TUF library ({count})", count > 0 && scanned == 0, $"scans={scanned}");
        check($"every page builds detached without a thumbnail request or a side effect ({count})",
            count > 0 && effects[2] >= 0 && effects.SequenceEqual(effectsAfter) && done.Count == 0,
            $"packs/previews listeners, previews cached={string.Join("/", effects)} -> {string.Join("/", effectsAfter)} done={string.Join(",", done)}");
        check($"every page builds detached without a request through the self-test door ({count})", count > 0 && door.SequenceEqual(doorAfter),
            $"fetches/previews/update checks/network={string.Join("/", door)} -> {string.Join("/", doorAfter)}");
    }

    private static int[] DoorReadings() => [SelfTestDoor.Fetches, SelfTestDoor.Previews, SelfTestDoor.UpdateChecks, SelfTestDoor.Network];

    // Timed once the menu has opened: before that there is no index to build.
    private static void IndexPerf(Action<string> info) {
        if(QuartzShell.Nav == null) {
            info("[perf] index build with every form page moved: no menu");
            return;
        }
        var clock = System.Diagnostics.Stopwatch.StartNew();
        QuartzShell.Changed();
        Find("a");
        info($"[perf] index build with every form page moved: {clock.Elapsed.TotalMilliseconds:0.0} ms");
        QuartzShell.Nav.Filter("");
    }

    // Each settings file's in-memory JSON and its bytes on disk, by path.
    private static Dictionary<string, (string Memory, byte[] Disk)> SettingsReadings() {
        var readings = new Dictionary<string, (string, byte[])>();
        foreach(ISettingsHandle h in SettingsRegistry.Snapshot()) {
            var data = h.GetType().GetProperty("Data")?.GetValue(h) as ISettingsFile;
            readings[h.Path] = (data?.Serialize().ToString(), Bytes(h.Path));
        }
        return readings;
    }

    // The listeners on the events a page must not subscribe to at build, each read from its backing field.
    private static int[] Listeners() {
        const BindingFlags any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        static int Count(Type t, string field, object target = null) =>
            (t.GetField(field, any)?.GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        return [
            Count(typeof(KeyLimiter), nameof(KeyLimiter.Changed)),
            Count(typeof(KeyBindSync), nameof(KeyBindSync.Changed)),
            Count(typeof(UpdateService), nameof(UpdateService.OnChanged)),
            Count(typeof(FontManager), nameof(FontManager.OnFontCatalogChanged)),
            TufService.Instance == null ? -1 : Count(typeof(TufService), nameof(TufService.Changed), TufService.Instance),
        ];
    }

    // TufPackService's and TufPreviewCache's listeners, and the previews cached (no thumbnail asked for), each read by reflection.
    private static int[] SideEffectReadings() {
        const BindingFlags any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        static int Count(Type t, string field, object target = null) =>
            (t.GetField(field, any)?.GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        return [
            TufPackService.Instance == null ? -1 : Count(typeof(TufPackService), nameof(TufPackService.Changed), TufPackService.Instance),
            Count(typeof(TufPreviewCache), nameof(TufPreviewCache.Changed)),
            (typeof(TufPreviewCache).GetField("entries", any)?.GetValue(null) as System.Collections.ICollection)?.Count ?? -1,
        ];
    }

    // Calls of TufService's private ScanSizes, counted by a prefix patched on first use.
    private static int scans;
    private static bool scansPatched;

    private static void CountScans() {
        if(scansPatched) return;
        scansPatched = true;
        new HarmonyLib.Harmony("Quartz.FacetSelfTest.TufScans").Patch(
            typeof(TufService).GetMethod("ScanSizes", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(CountScan), Static)));
    }

    private static void CountScan() => scans++;
}
#endif
