#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.Tuf;
using Quartz.Game.Stats;
using Quartz.UI.FacetUi.TufPages;
using UnityEngine;
using UnityEngine.UIElements;
using UImage = UnityEngine.UIElements.Image;
namespace Quartz.UI.FacetUi;
/// <summary>
/// TUF Levels' cards and thumbnails: the action labels by state, the installed level's badge, delete and rollback, the chart
/// rows, and the thumbnails decoding, settling past the cache and coming back after eviction.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static VisualElement TufCard(int n) => PageContent().Q(name: "tuf-card-" + TufId(n));

    private static UImage TufImageOf(VisualElement body) {
        for(VisualElement e = body; e != null; e = e.parent)
            if(e.name == "image-cards-card") return e.Q<UImage>("image-cards-image");
        return null;
    }

    // A texture drawn after its owner destroyed it: the reference is there, Unity's null check says gone.
    private static bool Destroyed(UImage img) => img?.image is not null && img.image == null;

    private static IEnumerator TufThumbnails(Action<string, bool, string> check, Action<string> info) {
        TufPumpWorst = 0.0;
        TufCountPump();
        // Timed from a cleared cache, the decode path already run once as the page opened: the window asks again.
        TufPreviewCache.Clear();
        TufTimePump = true;
        (typeof(TufPreviewCache).GetField(nameof(TufPreviewCache.Changed), BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Action)?.Invoke();
        UImage img = TufImageOf(TufCard(1));
        yield return TufUntil(() => img?.image != null, 180);
        yield return Frames(30);   // the fade
        TufTimePump = false;
        info($"[perf] tuf thumbnails worst pump frame: {TufPumpWorst:0.00} ms");
        check($"{TufLevelsKey}: thumbnails decode and fade in", img?.image != null && img.resolvedStyle.opacity > 0.9f && TufPumpWorst <= 4.0,
            $"image={img?.image != null} opacity={img?.resolvedStyle.opacity:0.00} worst pump={TufPumpWorst:0.00} ms (budget 4)");
    }

    private static double TufPumpWorst;
    private static bool TufTimePump, tufPumpPatched;
    private static long tufPumpStart;

    private static void TufCountPump() {
        if(tufPumpPatched) return;
        tufPumpPatched = true;
        new HarmonyLib.Harmony("Quartz.FacetSelfTest.TufPump").Patch(typeof(TufPreviewCache).GetMethod("Pump", BindingFlags.Static | BindingFlags.NonPublic),
            prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(TufPumpIn), Static)),
            postfix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(TufPumpOut), Static)));
    }

    private static void TufPumpIn() => tufPumpStart = System.Diagnostics.Stopwatch.GetTimestamp();

    private static void TufPumpOut() {
        if(!TufTimePump) return;
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - tufPumpStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        if(ms > TufPumpWorst) TufPumpWorst = ms;
    }

    private static IEnumerator TufCardChecks(Action<string, bool, string> check, Action<string> info, TufService s) {
        TufLevel one = s.Levels[0], two = s.Levels[1];
        string T(string key, string en) => QuartzHost.Text(key, en);
        var states = new (TufItemState State, float Progress, string Label, bool On)[] {
            (TufItemState.Downloading, -1f, T("TUF_DOWNLOADING", "Downloading…"), false),
            (TufItemState.Downloading, 0.5f, string.Format(T("TUF_DOWNLOADING_PROGRESS", "Downloading {0}%"), 50), false),
            (TufItemState.Extracting, 0f, T("TUF_EXTRACTING", "Extracting…"), false),
            (TufItemState.Loading, 0f, T("TUF_LOADING_LEVEL", "Loading…"), false),
            (TufItemState.Load, 0f, T("TUF_LOAD", "Load"), true),
            (TufItemState.Retry, 0f, T("TUF_RETRY", "Retry"), true),
            (TufItemState.Unavailable, 0f, T("TUF_UNAVAILABLE", "Unavailable"), false),
            (TufItemState.ChooseChart, 0f, T("TUF_CANCEL", "Cancel"), true),
            (TufItemState.Queued, 0f, string.Format(T("TUF_QUEUED", "Queued #{0}"), s.QueuePosition(one.Id)), true),
            (TufItemState.Download, 0f, T("TUF_DOWNLOAD", "Download"), true),
        };
        foreach(var st in states) {
            one.State = st.State;
            one.Progress = st.Progress;
            s.NotifyForTest();
            yield return Frames(1);
            Button act = TufCard(1)?.Q<Button>("tuf-action");
            check($"{TufLevelsKey}: the action reads {st.Label} and is {(st.On ? "enabled" : "disabled")}",
                act != null && act.text == st.Label && act.enabledSelf == st.On, $"shown='{act?.text}' enabled={act?.enabledSelf}");
        }
        one.State = TufItemState.Queued;
        two.State = TufItemState.Extracting;
        s.NotifyForTest();
        yield return Frames(1);
        string a = TufCard(1)?.Q<Button>("tuf-action")?.text, b = TufCard(2)?.Q<Button>("tuf-action")?.text;
        check($"{TufLevelsKey}: one Changed relabels every card", a == states[8].Label && b == states[2].Label, $"one='{a}' two='{b}'");
        one.State = two.State = TufItemState.Download;
        s.NotifyForTest();
        yield return Frames(1);
        yield return Recorded(check, $"{TufLevelsKey}: the action acts only through Do", () => Submit(TufCard(1)?.Q<Button>("tuf-action")), "tuf.act:" + one.Id);

        // Previews off: every card's image goes and every card is drawn again.
        VisualElement drawn = TufCard(1);
        s.SetShowPreviews(false);
        yield return Frames(2);
        bool none = TufCardEls().All(e => e.Q<UImage>("image-cards-image")?.image == null);
        check($"{TufLevelsKey}: turning ShowPreviews off drops every card's image and redraws every card",
            none && TufCard(1) != null && !ReferenceEquals(drawn, TufCard(1)), $"no images={none} redrawn={!ReferenceEquals(drawn, TufCard(1))}");
        s.SetShowPreviews(true);
        yield return Frames(2);

        // An installed level: its folder and state set directly, two rollback copies from the door.
        one.InstallFolder = Path.Combine(MainCore.Paths.TufLevelsPath, "selftest-not-there");
        one.State = TufItemState.Load;
        long stamp = 1700000000L;
        tufSnapshots[one.Id] = [new TufSnapshot { Stamp = stamp, Id = one.Id, SizeBytes = 2048 }, new TufSnapshot { Stamp = stamp + 60, Id = one.Id }];
        s.NotifyForTest();
        yield return Frames(2);
        Button Part(string name) => TufCard(1)?.Q<Button>(name);
        check($"{TufLevelsKey}: an installed level shows the badge, delete and rollback", Part("tuf-badge") != null && Part("tuf-delete") != null
            && Part("tuf-rollback") != null, $"badge={Part("tuf-badge") != null} delete={Part("tuf-delete") != null} rollback={Part("tuf-rollback") != null}");

        Button del = Part("tuf-delete");
        if(del != null) Submit(del);
        yield return Frames(1);
        bool armed = TufCards.Armed == one.Id;
        float t0 = Time.realtimeSinceStartup;
        yield return TufUntil(() => TufCards.Armed == 0 || Time.realtimeSinceStartup - t0 > 5f, 600);
        float lapse = Time.realtimeSinceStartup - t0;
        check($"{TufLevelsKey}: delete arms for 4 s, then lapses", del != null && armed && TufCards.Armed == 0 && lapse >= 3.7f && lapse <= 4.6f,
            $"armed={armed} lapsed after {lapse:0.00} s");
        yield return Recorded(check, $"{TufLevelsKey}: the second click of delete reaches Delete", () => {
            Submit(Part("tuf-delete"));
            Submit(Part("tuf-delete"));
        }, "tuf.delete:" + one.Id);

        Button back = Part("tuf-rollback");
        if(back != null) Submit(back);
        yield return Frames(2);
        VisualElement list = TufPopup("tuf-rollback-list");
        Button restore = list?.Q(name: "tuf-snapshot-1")?.Q<Button>("tuf-restore");
        bool two2 = list?.Q(name: "tuf-snapshot-2") != null && list.Q(name: "tuf-snapshot-3") == null;
        if(restore != null)
            yield return Recorded(check, $"{TufLevelsKey}: rollback lists the door's two snapshots and Restore reaches RestoreSnapshot",
                () => Submit(restore), two2 ? "tuf.restore:" + one.Id + ":" + stamp : "missing");
        else check($"{TufLevelsKey}: rollback lists the door's two snapshots and Restore reaches RestoreSnapshot", false, $"list={list != null}");
        Popup.CloseAll();

        yield return Recorded(check, $"{TufLevelsKey}: the badge acts only through Do", () => Submit(Part("tuf-badge")), "tuf.check:" + one.Id);
        yield return Recorded(check, $"{TufLevelsKey}: Updates acts only through Do", () => PressRow("TUF_CHECK_UPDATES"),
            s.UpdatesAvailable > 0 ? "tuf.updateall" : "tuf.checkupdates");
        yield return Recorded(check, $"{TufLevelsKey}: the Installed switch acts only through Do", () => ClickRow("TUF_INSTALLED"), "tuf.installed");

        // Two charts on level 2: two rows under its card, and in grid mode under its row.
        two.ChartsRoot = MainCore.Paths.TufLevelsPath;
        two.Charts = [Path.Combine(two.ChartsRoot, "a", "one.adofai"), Path.Combine(two.ChartsRoot, "b", "two.adofai")];
        two.State = TufItemState.ChooseChart;
        s.NotifyForTest();
        yield return Frames(2);
        bool rows = PageContent().Q(name: "tuf-chart-1") != null && PageContent().Q(name: "tuf-chart-2") != null && PageContent().Q(name: "tuf-chart-3") == null;
        bool grid = s.GridView;
        s.SetGridView(true);
        yield return Frames(4);
        VisualElement chart = PageContent().Q(name: "tuf-chart-1");
        float below = TufCardEls().Take(2).Select(e => e.worldBound.yMax).DefaultIfEmpty(float.MaxValue).Max();
        bool under = chart != null && chart.worldBound.yMin >= below - 1f;
        s.SetGridView(grid);
        yield return Frames(2);
        check($"{TufLevelsKey}: two charts show two rows under the card, in grid mode under its row", rows && under,
            $"rows={rows} chart y={chart?.worldBound.yMin:0} row bottom={below:0}");
        yield return Recorded(check, $"{TufLevelsKey}: a chart row acts only through Do", () => Submit(PageContent().Q<Button>("tuf-chart-1")),
            "tuf.chart:" + two.Id + ":1");

        one.InstallFolder = null;
        one.State = two.State = TufItemState.Download;
        two.Charts = null;
        tufSnapshots.Clear();
        s.NotifyForTest();
        yield return Frames(2);

        // A refresh brings the same levels back as new objects, each card unchanged: a click still reaches the service's level.
        int had = s.Levels.Count;
        s.Refresh();
        yield return TufUntil(() => s.State == TufListState.Ready && s.Levels.Count > 0 && !ReferenceEquals(s.Levels[0], one), 120);
        yield return Frames(2);
        TufCountAct();
        tufActed = null;
        tufActCapture = true;
        Func<string, bool> doing = QuartzShell.Doing;
        QuartzShell.Doing = _ => false;
        try {
            Submit(TufCard(1)?.Q<Button>("tuf-action"));
            yield return Frames(1);
        } finally {
            QuartzShell.Doing = doing;
            tufActCapture = false;
        }
        TufLevel live = s.Levels.Count > 0 ? s.Levels[0] : null;
        check($"{TufLevelsKey}: after a refresh the action acts on the service's level, not the one drawn before",
            live != null && !ReferenceEquals(live, one) && ReferenceEquals(tufActed, live),
            $"acted={(tufActed == null ? "none" : ReferenceEquals(tufActed, live) ? "live" : ReferenceEquals(tufActed, one) ? "stale" : "other")}");
        while(s.Levels.Count < had && s.HasMore) {
            int want = s.Levels.Count + TufPageSize;
            s.LoadMore();
            yield return TufUntil(() => s.Levels.Count >= want, 60);
        }
    }

    private static TufLevel tufActed;
    private static TufLevelActionRunner tufActRunner;
    private static bool tufActCapture, tufActPatched;

    // While capturing, the level a click hands the action runner, and that runner, are kept and the action itself skipped.
    private static void TufCountAct() {
        if(tufActPatched) return;
        tufActPatched = true;
        new HarmonyLib.Harmony("Quartz.FacetSelfTest.TufAct").Patch(typeof(TufLevelActionRunner).GetMethod(nameof(TufLevelActionRunner.Act)),
            prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(TufActIn), Static)));
    }

    private static bool TufActIn(TufLevel level, TufLevelActionRunner __instance) {
        if(!tufActCapture) return true;
        tufActed = level;
        tufActRunner = __instance;
        return false;
    }

    // An element of an open popup, which lives on Facet's popup surface, a document of its own.
    private static VisualElement TufPopup(string name) {
        foreach(UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if(d.rootVisualElement?.Q(name: name) is { } e && e.panel != null) return e;
        return null;
    }

    private static IEnumerator TufSettle(Action<string, bool, string> check, Action<string> info, TufService s) {
        ScrollView sv = TufScroll();
        for(int i = 0; i < 4 && s.HasMore; i++) {
            yield return Frames(2);   // the last page laid out, so the scroll reaches its end
            TufScrollToEnd();
            int want = s.Levels.Count + TufPageSize;
            yield return TufUntil(() => s.Levels.Count >= want, 60);
        }
        int changes = 0;
        Action heard = () => changes++;
        TufPreviewCache.Changed += heard;
        try {
            // Top to bottom a viewport at a time, each window's thumbnails decoded before the next.
            sv.scrollOffset = Vector2.zero;
            yield return Frames(2);
            float end = sv.contentContainer.layout.height - sv.contentViewport.layout.height;
            for(float y = 0f; y <= end + 1f; y += Mathf.Max(100f, sv.contentViewport.layout.height)) {
                sv.scrollOffset = new Vector2(0f, Mathf.Min(y, end));
                yield return TufUntil(() => TufInView(sv).All(img => img.image != null), 90);
            }
            sv.scrollOffset = new Vector2(0f, end);
            yield return TufUntil(() => TufInView(sv).All(img => img.image != null), 180);
            // The decodes the scroll queued on its way down land first: settled is 30 frames without one, then 120 are watched.
            int quiet = 0, last = changes;
            for(int i = 0; i < 900 && quiet < 30; i++) {
                yield return Frames(1);
                quiet = changes == last ? quiet + 1 : 0;
                last = changes;
            }
            int previews = SelfTestDoor.Previews, heardBefore = changes;
            yield return Frames(120);
            List<UImage> shown = TufInView(sv);
            bool live = shown.Count > 0 && shown.All(img => img.image != null);
            check($"{TufLevelsKey}: past 300 cards, thumbnails settle", s.Levels.Count == TufFixtureCount && live
                && SelfTestDoor.Previews == previews && changes == heardBefore,
                $"levels={s.Levels.Count} in view={shown.Count} live={live} asks={SelfTestDoor.Previews - previews} changes={changes - heardBefore}");

            previews = SelfTestDoor.Previews;
            sv.scrollOffset = Vector2.zero;
            yield return Frames(2);
            yield return TufUntil(() => TufInView(sv).All(img => img.image != null), 180);
            shown = TufInView(sv);
            bool back = shown.Count > 0 && shown.All(img => img.image != null);
            bool clean = !TufCardEls().Any(e => Destroyed(e.Q<UImage>("image-cards-image")));
            check($"{TufLevelsKey}: a card scrolled back after its thumbnail was evicted asks again",
                SelfTestDoor.Previews > previews && back && clean, $"asks={SelfTestDoor.Previews - previews} live={back} no destroyed={clean}");
        } finally {
            TufPreviewCache.Changed -= heard;
        }
    }

    // The images of the cards inside the scroll view's viewport.
    private static List<UImage> TufInView(ScrollView sv) {
        Rect view = sv.contentViewport.worldBound;
        return [.. TufCardEls().Where(e => e.worldBound.yMax > view.yMin + 1f && e.worldBound.yMin < view.yMax - 1f)
            .Select(e => e.Q<UImage>("image-cards-image")).Where(i => i != null)];
    }

    /// <summary>Inside a level, with the new menu open and the classic menu shut, a thumbnail still decodes.</summary>
    internal static IEnumerator TufThumbnailInLevel(Action<string, bool, string> check, Action<string> info) {
        const string name = "tuf.levels: thumbnails decode with only the new menu open in a level";
        bool wasOn = FeatureService.IsEnabled("tuf");
        bool wasOpen = UICore.IsOpen;
        try {
            if(!wasOn) {
                FeatureService.SetEnabledForRun("tuf", true);
                yield return Frames(3);
            }
            if(wasOpen) UICore.Close(true);
            FacetBridge.OpenNewMenu("tuf.settings");
            yield return Frames(3);
            tufJpeg ??= TufJpeg();
            SelfTestDoor.Preview = _ => tufJpeg;
            string key = "selftest-in-level";
            bool setUp = GameStats.InGame && FacetBridge.NewMenuVisible() && !UICore.IsOpen;
            TufPreviewCache.Request(key, TufPreviewSource.Video(null));
            int frames = 0;
            for(; frames < 30 && !TufPreviewCache.TryGet(key, out _); frames++) yield return Frames(1);
            bool ready = TufPreviewCache.TryGet(key, out _);
            if(!setUp) info($"{name}: not in a level with only the new menu open (in game={GameStats.InGame})");
            check(name, setUp && ready, $"in game={GameStats.InGame} new menu={FacetBridge.NewMenuVisible()} classic={UICore.IsOpen} ready={ready} frames={frames}");
        } finally {
            TufPreviewCache.Clear();
            SelfTestDoor.Defaults();
            QuartzShell.Hide();
            if(wasOpen) UICore.Open(true);
            if(!wasOn) FeatureService.SetEnabledForRun("tuf", false);
        }
        yield return Frames(2);
    }
}
#endif
