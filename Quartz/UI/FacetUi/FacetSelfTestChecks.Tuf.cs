#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.Tuf;
using Quartz.IO;
using Quartz.UI.FacetUi.TufPages;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// TUF Levels in the new menu against the classic browser, over a fixture of 350 levels the self-test door serves: the
/// list, paging, search, sort, filters, states, cards and thumbnails. Every card button is checked through Do; the only real
/// writes are TUF's sort, filters, grid and previews, each put back. Fixture ids are int.MaxValue - n, which no TUF level has.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string TufLevelsKey = "tuf.levels";
    private const int TufFixtureCount = 350, TufPageSize = 50;

    // The fixture's state for one section: the door's failure, if any, and the JPEG every thumbnail decodes.
    private static Func<int, Exception> tufFail;
    private static byte[] tufJpeg;
    private static readonly Dictionary<int, IReadOnlyList<TufSnapshot>> tufSnapshots = [];

    private static int TufId(int n) => int.MaxValue - n;

    private static TufLevel TufFixture(int n) => new(TufId(n), "Selftest Song " + n, "Selftest Artist", "Selftest Creator", "G5", "#33AAFF",
        n * 3, n, new Uri("https://selftest.invalid/" + n));

    private static Task<TufPage> TufFetch(string query, int offset) {
        if(tufFail?.Invoke(offset) is { } e) return Task.FromException<TufPage>(e);
        var matched = new List<TufLevel>();
        for(int n = 1; n <= TufFixtureCount; n++)
            if(string.IsNullOrEmpty(query) || ("Selftest Song " + n).EndsWith(" " + query, StringComparison.OrdinalIgnoreCase)
                || ("Selftest Song " + n).Equals(query, StringComparison.OrdinalIgnoreCase))
                matched.Add(TufFixture(n));
        var page = matched.Skip(offset).Take(TufPageSize).ToList();
        return Task.FromResult(new TufPage(page, offset + TufPageSize < matched.Count));
    }

    private static IEnumerator TufLevels(Action<string, bool, string> check, Action<string> info) {
        int network = SelfTestDoor.Network;
        TufService s = null;
        SettingsFile<TufSettings> file = null;
        (TufSort sort, bool asc, int min, int max, string[] special, bool quantum, int qmin, int qmax, bool grid, bool previews) was = default;
        Newtonsoft.Json.Linq.JToken kept = null;
        yield return FormPage("tuf", TufLevelsKey, () => {
            s = TufService.Instance;
            if(s == null) return;
            file = SettingsOf(s);
            kept = file?.Data.Serialize();
            was = (s.Sort, s.Ascending, s.MinDifficultyIndex, s.MaxDifficultyIndex, [.. TufDifficultyFilter.SpecialNames.Where(s.DifficultyFilter.IsSelected)],
                s.QuantumEnabled, s.QuantumMinIndex, s.QuantumMaxIndex, s.GridView, s.ShowPreviews);
            s.ForgetForTest();
            TufPreviewCache.Clear();
            tufFail = null;
            tufJpeg ??= TufJpeg();
            tufSnapshots.Clear();
            SelfTestDoor.Fetch = (q, _, _, offset, _, _) => TufFetch(q, offset);
            SelfTestDoor.Preview = _ => tufJpeg;
            SelfTestDoor.SnapshotCount = id => tufSnapshots.TryGetValue(id, out var l) ? l.Count : 0;
            SelfTestDoor.Snapshots = id => tufSnapshots.TryGetValue(id, out var l) ? l : [];
        }, () => {
            TufService now = TufService.Instance;
            if(now != null && s == now) {
                if(now.Sort != was.sort) now.SetSort(was.sort);
                if(now.Ascending != was.asc) now.ToggleAscending();
                now.SetQuantumRange(was.qmin, was.qmax);
                if(!was.quantum) now.ClearQuantum();
                now.SetDifficultyRange(was.min, was.max);
                foreach(string n in TufDifficultyFilter.SpecialNames)
                    if(now.DifficultyFilter.IsSelected(n) != was.special.Contains(n)) now.ToggleSpecialDifficulty(n);
                now.SetGridView(was.grid);
                now.SetShowPreviews(was.previews);
                if(file != null && kept != null) {
                    file.Data.Deserialize(kept);
                    file.Save();
                }
            }
            TufService.Instance?.ForgetForTest();
            TufPreviewCache.Clear();
            TufCards.Forget();
            tufFail = null;
            tufSnapshots.Clear();
            SelfTestDoor.Defaults();
        }, () => Checks(), "others");
        TufService after = TufService.Instance;
        check($"{TufLevelsKey}: after the section the service holds no fixture data",
            after == null || (after.Levels.Count == 0 && after.State == TufListState.Idle && after.Query.Length == 0),
            $"levels={after?.Levels.Count} state={after?.State} query='{after?.Query}'");
        check($"{TufLevelsKey}: no request left the machine", SelfTestDoor.Network == network, $"network={SelfTestDoor.Network - network}");

        IEnumerator Checks() {
            if(s == null) {
                check($"{TufLevelsKey}: the TUF service is up", false, "TufService.Instance");
                yield break;
            }
            yield return TufUntil(() => s.State == TufListState.Ready && s.Levels.Count > 0, 120);
            if(s.Levels.Any(l => l.InstallFolder != null)) {
                info($"{TufLevelsKey}: a fixture id is in the installed index or the chart cache, so the section is skipped");
                yield break;
            }
            check($"{TufLevelsKey}: the service lists the fixture first", s.Levels.Count > 0 && s.Levels[0].Id == TufId(1),
                $"levels={s.Levels.Count} first={(s.Levels.Count > 0 ? s.Levels[0].Id : 0)}");
            yield return Frames(3);
            Searched(check, TufLevelsKey, ["TUF_REFRESH"]);
            Translated(check, TufLevelsKey, ["TUF_SPECIAL"]);

            int[] shownIds = TufCardIds();
            check($"{TufLevelsKey}: the first page fills the cards from the fixture",
                shownIds.SequenceEqual(Enumerable.Range(1, TufPageSize).Select(TufId)), $"cards={shownIds.Length} first={shownIds.FirstOrDefault()}");

            yield return TufThumbnails(check, info);
            yield return TufPaging(check, info, s);
            yield return TufQuery(check, s);
            yield return TufSorts(check, s, file);
            yield return TufFilterChecks(check, info, s);
            yield return TufStates(check, s);
            if(s.Levels.Count >= 2) yield return TufCardChecks(check, info, s);
            else check($"{TufLevelsKey}: the card checks have two fixture levels", false, $"levels={s.Levels.Count}");
            yield return TufSettle(check, info, s);
        }
    }

    // Each fixture card's level number, in the order the page shows them.
    private static int[] TufCardIds() => [.. PageContent().Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith("tuf-card-")).ToList()
        .Select(e => int.TryParse(e.name["tuf-card-".Length..], out int id) ? id : 0)];

    private static List<VisualElement> TufCardEls() => PageContent().Query(name: "image-cards-card").ToList();

    private static IEnumerator TufUntil(Func<bool> done, int frames) {
        for(int i = 0; i < frames && !done(); i++) yield return Frames(1);
    }

    // Each element from e up to the scroll view's content: its name, height and shrink, for a list that ends past its scroll.
    private static string TufChain(VisualElement e, ScrollView sv) {
        var parts = new List<string>();
        for(; e != null && sv != null && e != sv.contentContainer; e = e.parent)
            parts.Add($"{e.name}:{e.layout.height:0}/{e.resolvedStyle.flexShrink:0}/{e.resolvedStyle.maxHeight}");
        return string.Join(" < ", parts);
    }

    // The scroll view the cards page in: the page's own.
    private static ScrollView TufScroll() => PageContent().Q(name: "image-cards")?.GetFirstAncestorOfType<ScrollView>() ?? ContentScroll;

    private static void TufScrollToEnd() {
        ScrollView sv = TufScroll();
        sv.scrollOffset = new Vector2(0f, Mathf.Max(0f, sv.contentContainer.layout.height - sv.contentViewport.layout.height));
    }

    private static IEnumerator TufPaging(Action<string, bool, string> check, Action<string> info, TufService s) {
        int fetches = SelfTestDoor.Fetches;
        TufScrollToEnd();
        yield return TufUntil(() => s.Levels.Count >= 2 * TufPageSize, 60);
        yield return Frames(10);
        ScrollView sv = TufScroll();
        VisualElement body = PageContent().Q(name: "search-list-body");
        check($"{TufLevelsKey}: scrolling to the end loads the next page once", s.Levels.Count == 2 * TufPageSize && SelfTestDoor.Fetches == fetches + 1,
            $"levels={s.Levels.Count} fetches={SelfTestDoor.Fetches - fetches} scroll={sv?.name}/{sv?.scrollOffset.y:0}/{sv?.verticalScroller.highValue:0}"
            + $" content={sv?.contentContainer.layout.height:0} view={sv?.contentViewport.layout.height:0}"
            + $" body end={(body == null || sv == null ? float.NaN : body.worldBound.yMax - sv.contentContainer.worldBound.y):0} more={s.HasMore}/{s.LoadingMore}/{s.State} chain={TufChain(body, sv)}");

        // Up to 250, then the page that makes 300 is timed with the cards it lands beside.
        for(int i = 0; i < 6 && s.Levels.Count < 5 * TufPageSize; i++) {
            TufScrollToEnd();
            int want = s.Levels.Count + TufPageSize;
            yield return TufUntil(() => s.Levels.Count >= want, 60);
            yield return Frames(2);
        }
        List<VisualElement> before = TufCardEls();
        TufScrollToEnd();
        yield return TufUntil(() => s.Levels.Count >= 6 * TufPageSize, 60);
        double ms = TufLevelsPage.LastChangedMs;
        yield return Frames(2);
        List<VisualElement> afterEls = TufCardEls();
        bool kept = before.Count == 5 * TufPageSize && afterEls.Count == 6 * TufPageSize && before.Select((e, i) => ReferenceEquals(e, afterEls[i])).All(b => b);
        info($"[perf] tuf.levels append 50 cards: {ms:0.0} ms");
        check($"{TufLevelsKey}: appending a page keeps the existing cards", kept && ms <= 12.0,
            $"before={before.Count} after={afterEls.Count} append={ms:0.0} ms (budget 12)");

        // 300 cards: every card in the window asked again, timed.
        FieldInfo changed = typeof(TufPreviewCache).GetField(nameof(TufPreviewCache.Changed), BindingFlags.Static | BindingFlags.NonPublic);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        (changed?.GetValue(null) as Action)?.Invoke();
        double images = clock.Elapsed.TotalMilliseconds;
        info($"[perf] tuf.levels ImagesChanged 300 cards: {images:0.00} ms");
        check($"{TufLevelsKey}: ImagesChanged over 300 cards stays within 2 ms", changed != null && images <= 2.0, $"{images:0.00} ms");

        yield return TufScrollPerf(check, info);

        TufScrollToEnd();
        yield return TufUntil(() => s.Levels.Count >= TufFixtureCount, 60);
        yield return Frames(3);
        int last = SelfTestDoor.Fetches;
        TufScrollToEnd();
        yield return Frames(10);
        string end = QuartzHost.Text("TUF_END", "End of results");
        bool shown = PageContent().Q(name: "search-list-footer")?.Query<Label>().ToList().Any(l => l.text == end && Displayed(l)) == true;
        check($"{TufLevelsKey}: the last page shows TUF_END and stops paging", s.Levels.Count == TufFixtureCount && !s.HasMore && shown
            && SelfTestDoor.Fetches == last, $"levels={s.Levels.Count} more={s.HasMore} end={shown} fetches after={SelfTestDoor.Fetches - last}");
    }

    private static IEnumerator TufScrollPerf(Action<string, bool, string> check, Action<string> info) {
        ScrollView sv = TufScroll();
        float worst = 0f;
        sv.scrollOffset = Vector2.zero;
        yield return Frames(2);
        float step = Mathf.Max(20f, (sv.contentContainer.layout.height - sv.contentViewport.layout.height) / 60f);
        for(int i = 0; i < 60; i++) {
            sv.scrollOffset += new Vector2(0f, step);
            yield return Frames(1);
            worst = Mathf.Max(worst, Time.unscaledDeltaTime * 1000f);
        }
        info($"[perf] tuf.levels scroll 300 cards worst frame: {worst:0.0} ms");
        check($"{TufLevelsKey}: scrolling 300 cards keeps each frame within 33 ms", worst <= 33f, $"worst={worst:0.0} ms");
    }

    private static IEnumerator TufQuery(Action<string, bool, string> check, TufService s) {
        TextField field = PageContent().Q<TextField>("search-list-field");
        float took = 0f;
        IEnumerator Type(string text, Func<bool> done) {
            if(field != null) field.value = text;
            float t0 = Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup - t0 < 1.3f && !(s.State != TufListState.Loading && done())) yield return null;
            took = Time.realtimeSinceStartup - t0;
            yield return Frames(2);
        }
        yield return Type("123", () => s.Query == "123");
        int[] ids = TufCardIds();
        // The fixture answers at once, so the list leaves Loading only once the 300 ms debounce has run.
        check($"{TufLevelsKey}: typing a query refetches after the service's debounce and shows only matches",
            field != null && s.Query == "123" && took >= 0.3f && ids.SequenceEqual([TufId(123)]),
            $"field={field != null} query='{s.Query}' after {took:0.00} s cards={string.Join(",", ids.Select(i => int.MaxValue - i))}");
        yield return Type("zz-none", () => s.Query == "zz-none");
        string empty = QuartzHost.Text("TUF_EMPTY", "No levels matched your search.");
        bool said = PageContent().Q(name: "search-list-status")?.Query<Label>().ToList().Any(l => l.text == empty) == true;
        check($"{TufLevelsKey}: an unmatched query shows TUF_EMPTY", s.State == TufListState.Empty && said, $"state={s.State} said={said}");
        yield return Type("", () => s.Query.Length == 0 && s.Levels.Count > 0);
    }

    private static IEnumerator TufSorts(Action<string, bool, string> check, TufService s, SettingsFile<TufSettings> file) {
        var bad = new List<string>();
        TufSort first = s.Sort;
        foreach(TufSort sort in new[] { TufSort.Recent, TufSort.Difficulty, TufSort.Clears, TufSort.Likes }.Where(x => x != first).Append(first)) {
            string key = TufFilters.SortKey(sort);
            byte[] before = Bytes(file?.Path);
            VisualElement chip = PageContent().Q(name: key);
            if(chip is Button b) Submit(b);
            yield return TufSaved(file, before);
            if(chip == null || s.Sort != sort || file?.Data.GetSort() != sort || SameBytes(Bytes(file?.Path), before)) bad.Add(key);
        }
        bool asc = s.Ascending;
        for(int i = 0; i < 2; i++) {
            byte[] before = Bytes(file?.Path);
            Button dir = PageContent().Q<Button>("tuf-direction");
            if(dir != null) Submit(dir);
            yield return TufSaved(file, before);
            bool want = i == 0 ? !asc : asc;
            if(dir == null || s.Ascending != want || file?.Data.Ascending != want || dir.text != (want ? "↑" : "↓") || SameBytes(Bytes(file?.Path), before))
                bad.Add("direction" + i);
        }
        check($"{TufLevelsKey}: each sort chip and the direction save to Settings.json and the classic page sees them", file != null && bad.Count == 0,
            $"failed={string.Join(",", bad)}");
    }

    private static IEnumerator TufSaved(SettingsFile<TufSettings> file, byte[] before) {
        for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 2f && SameBytes(Bytes(file?.Path), before);) yield return null;
        yield return Frames(1);
    }

    private static IEnumerator TufFilterChecks(Action<string, bool, string> check, Action<string> info, TufService s) {
        // The filter card in four rows: measured with Quantum off (put back after) and Special shut, as the page builds it.
        bool quantumWas = s.QuantumEnabled;
        if(quantumWas) ClickRow("TUF_QUANTUM");
        yield return Frames(3);
        VisualElement filters = PageContent().Q(name: "tuf-sort")?.parent;
        bool specialShut = !Displayed(PageContent().Q(name: "TUF_SPECIAL_" + TufDifficultyFilter.SpecialNames[0].ToUpperInvariant() + "-input"));
        float rowH = filters != null ? Theme.For(filters).Sizes.RowHeight : 0f;
        float pad = filters != null ? filters.resolvedStyle.paddingTop + filters.resolvedStyle.paddingBottom : 0f;
        float h = filters?.layout.height ?? float.NaN;
        check($"{TufLevelsKey}: the levels filter card is at most 4.5 rows tall with Special shut and Quantum off",
            filters != null && specialShut && !s.QuantumEnabled && h <= 4.5f * rowH + pad,
            $"card={filters != null} h={h} limit={4.5f * rowH + pad} special shut={specialShut} quantum={s.QuantumEnabled}");
        if(quantumWas) {
            ClickRow("TUF_QUANTUM");
            yield return Frames(3);
        }

        // A drag on the range's track: down near the low end, three moves, up.
        var calls = new List<(int, int)>();
        TufRangeCalls = calls;
        int min = s.MinDifficultyIndex, max = s.MaxDifficultyIndex;
        VisualElement track = PageContent().Q(name: "TUF_DIFFICULTY_FILTER-track");
        if(track != null) {
            TufCountRange();
            Rect r = track.worldBound;
            float y = r.center.y, x0 = Mathf.Lerp(r.xMin, r.xMax, min / 59f);
            Pointer<PointerDownEvent>(track, new Vector2(x0, y));
            for(int i = 1; i <= 3; i++) {
                Pointer<PointerMoveEvent>(track, new Vector2(Mathf.Lerp(x0, Mathf.Lerp(r.xMin, r.xMax, 0.3f), i / 3f), y));
                yield return Frames(1);
            }
            Pointer<PointerUpEvent>(track, new Vector2(Mathf.Lerp(r.xMin, r.xMax, 0.3f), y));
            yield return Frames(2);
        }
        TufRangeCalls = null;
        check($"{TufLevelsKey}: the difficulty range commits once per drag", track != null && calls.Count == 1
            && calls[0] == (s.MinDifficultyIndex, s.MaxDifficultyIndex) && s.MinDifficultyIndex != min,
            $"track={track != null} calls={string.Join(";", calls)} now={s.MinDifficultyIndex}-{s.MaxDifficultyIndex} was={min}-{max}");
        s.SetDifficultyRange(min, max);
        Texture gradient = PageContent().Q(name: "tuf-gradient")?.style.backgroundImage.value.texture;
        check($"{TufLevelsKey}: the range's gradient texture is never saved or shown in the hierarchy",
            gradient != null && gradient.hideFlags == HideFlags.HideAndDontSave, $"texture={gradient != null} flags={gradient?.hideFlags}");

        bool q0 = s.QuantumEnabled;
        ClickRow("TUF_QUANTUM");
        yield return Frames(3);
        bool on = s.QuantumEnabled != q0 && Displayed(PageContent().Q(name: "tuf-quantum-range-track")) == s.QuantumEnabled;
        ClickRow("TUF_QUANTUM");
        yield return Frames(3);
        bool off = s.QuantumEnabled == q0 && Displayed(PageContent().Q(name: "tuf-quantum-range-track")) == q0;
        check($"{TufLevelsKey}: Quantum on shows its range, off clears it", on && off, $"on={on} off={off} was={q0}");

        var flipped = new List<string>();
        foreach(string n in TufDifficultyFilter.SpecialNames) {
            bool before = s.DifficultyFilter.IsSelected(n);
            ClickRow("TUF_SPECIAL_" + n.ToUpperInvariant());
            yield return Frames(1);
            bool flip = s.DifficultyFilter.IsSelected(n) != before;
            ClickRow("TUF_SPECIAL_" + n.ToUpperInvariant());
            yield return Frames(1);
            if(flip && s.DifficultyFilter.IsSelected(n) == before) flipped.Add(n);
        }
        check($"{TufLevelsKey}: each Special toggle flips its difficulty", flipped.Count == 3, $"flipped={string.Join(",", flipped)}");
        yield return TufUntil(() => s.State == TufListState.Ready, 60);

        bool grid = s.GridView;
        s.SetGridView(true);
        yield return Frames(4);
        VisualElement root = PageContent().Q(name: "image-cards");
        List<VisualElement> els = TufCardEls();
        float w = root?.resolvedStyle.width ?? 0f;
        int want = Mathf.Clamp(Mathf.FloorToInt((w + 8f) / 276f), 1, 6);
        int row = els.Count == 0 ? 0 : els.TakeWhile(e => Mathf.Abs(e.worldBound.y - els[0].worldBound.y) < 1f).Count();
        if(w < 552f) info($"{TufLevelsKey}: the cards are {w:0} px wide, so two columns are not reached on this window");
        check($"{TufLevelsKey}: Grid regroups the cards into columns by width", root != null && row == want && (w < 552f || row >= 2),
            $"width={w:0} columns={row} want={want}");
        s.SetGridView(grid);
        yield return Frames(2);
    }

    private static List<(int, int)> TufRangeCalls;
    private static bool tufRangePatched;

    private static void TufCountRange() {
        if(tufRangePatched) return;
        tufRangePatched = true;
        new HarmonyLib.Harmony("Quartz.FacetSelfTest.TufRange").Patch(typeof(TufService).GetMethod(nameof(TufService.SetDifficultyRange)),
            prefix: new HarmonyLib.HarmonyMethod(typeof(FacetSelfTestChecks).GetMethod(nameof(TufRangeCall), Static)));
    }

    private static void TufRangeCall(int minIndex, int maxIndex) => TufRangeCalls?.Add((minIndex, maxIndex));

    private static IEnumerator TufStates(Action<string, bool, string> check, TufService s) {
        string Status() => string.Join("\n", PageContent().Q(name: "search-list-status")?.Query<Label>().ToList().Where(Displayed).Select(l => l.text) ?? []);
        Button Retry() => PageContent().Query<Button>().ToList().FirstOrDefault(b => b.text == QuartzHost.Text("TUF_RETRY", "Retry") && Displayed(b));

        tufFail = _ => new OperationCanceledException();
        s.ForgetForTest();
        s.Refresh();
        yield return TufUntil(() => s.State == TufListState.Error, 60);
        yield return Frames(2);
        Button go = PageContent().Q<Button>("tuf-offline-switch");
        string offline = QuartzHost.Text("TUF_OFFLINE", "TUF could not be reached — you may be offline.");
        check($"{TufLevelsKey}: offline shows TUF_OFFLINE with the switch and Retry", Status().StartsWith(offline) && go != null && Retry() != null,
            $"status='{Status()}' switch={go != null} retry={Retry() != null}");
        if(go != null) yield return Recorded(check, $"{TufLevelsKey}: the offline switch acts only through Do", () => Submit(go), "tuf.offline");

        tufFail = _ => new InvalidOperationException("selftest api error");
        s.Refresh();
        yield return TufUntil(() => s.State == TufListState.Error && !s.OfflineError, 60);
        yield return Frames(2);
        bool api = Status().StartsWith(QuartzHost.Text("TUF_API_ERROR", "Could not load TUF levels."));
        tufFail = null;
        Button retry = Retry();
        if(retry != null) Submit(retry);
        yield return TufUntil(() => s.State == TufListState.Ready, 60);
        check($"{TufLevelsKey}: an API error shows TUF_API_ERROR and retries on click", api && retry != null && s.State == TufListState.Ready,
            $"api={api} retry={retry != null} state={s.State}");

        tufFail = offset => offset > 0 ? new InvalidOperationException("selftest page error") : null;
        yield return Frames(2);
        TufScrollToEnd();
        yield return TufUntil(() => s.State == TufListState.Error, 60);
        yield return Frames(2);
        Button more = PageContent().Q(name: "search-list-footer")?.Query<Button>().ToList().FirstOrDefault(b => Displayed(b));
        check($"{TufLevelsKey}: a failed page shows TUF_RETRY in the footer", s.Levels.Count == TufPageSize && more?.text == QuartzHost.Text("TUF_RETRY", "Retry"),
            $"levels={s.Levels.Count} footer='{more?.text}'");
        tufFail = null;
        if(more != null) Submit(more);
        yield return TufUntil(() => s.State == TufListState.Ready && s.Levels.Count > TufPageSize, 60);
        TufScroll().scrollOffset = Vector2.zero;
        yield return Frames(2);
    }

    /// <summary>With TUF switched off for the run (its services disposed, each Instance null), every TUF page builds detached
    /// without throwing; TUF goes back after.</summary>
    private static IEnumerator TufOffBuilds(Action<string, bool, string> check) {
        bool wasOn = FeatureService.IsEnabled("tuf");
        var failed = new List<string>();
        string[] keys = [.. FormPageKeys.Where(k => k.StartsWith("tuf."))];
        bool off = false;
        try {
            if(wasOn) {
                FeatureService.SetEnabledForRun("tuf", false);
                yield return Frames(3);
            }
            off = TufService.Instance == null && TufPackService.Instance == null;
            foreach(string key in keys) {
                try {
                    if(QuartzShell.Moved.TryGetValue(key, out Action<VisualElement> build)) build(new VisualElement());
                    else failed.Add(key + " (no builder)");
                } catch(Exception e) {
                    failed.Add(key + ": " + e.GetType().Name);
                }
            }
        } finally {
            if(wasOn) FeatureService.SetEnabledForRun("tuf", true);
        }
        yield return Frames(3);
        check("ThrowawayBuilds: tuf pages build with TUF off", off && keys.Length > 0 && failed.Count == 0 && FeatureService.IsEnabled("tuf") == wasOn,
            $"services gone={off} pages={string.Join(",", keys)} failed={string.Join(",", failed)} back={FeatureService.IsEnabled("tuf") == wasOn}");
    }

    private static byte[] TufJpeg() {
        var image = new Texture2D(320, 180, TextureFormat.RGB24, false);
        try {
            var pixels = new Color32[320 * 180];
            for(int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i % 320 * 255 / 320), (byte)(i / 320 * 255 / 180), 96, 255);
            image.SetPixels32(pixels);
            image.Apply(false);
            return image.EncodeToJPG();
        } finally {
            UnityEngine.Object.Destroy(image);
        }
    }
}
#endif
