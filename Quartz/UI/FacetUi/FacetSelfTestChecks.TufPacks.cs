#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.IO;
using Quartz.UI.FacetUi.TufPages;
using UnityEngine;
using UnityEngine.UIElements;
using PackPage = Quartz.Features.Tuf.TufPacksPage;
using UImage = UnityEngine.UIElements.Image;
namespace Quartz.UI.FacetUi;
/// <summary>
/// TUF Packs in the new menu against the classic pack browser, over 60 fixture packs and one fixture tree the self-test
/// door serves: paging, the session's sort, opening a pack and coming back, folders, level sort, thumbnails, states and
/// the level cards' actions. Every action is checked through Do; nothing here is saved.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string TufPacksKey = "tuf.packs";
    private const int TufPackCount = 60, TufPackPageSize = 25;

    // The door's failure for a page of packs (by offset) and for a pack's levels.
    private static Func<int, Exception> tufPackFail;
    private static Exception tufPackLevelsFail;

    private static Task<PackPage> TufPackFetch(int offset) {
        if(tufPackFail?.Invoke(offset) is { } e) return Task.FromException<PackPage>(e);
        var page = Enumerable.Range(1, TufPackCount).Skip(offset).Take(TufPackPageSize)
            .Select(n => new TufPack("selftest-" + n, "Selftest Pack " + n, "Selftest Owner", 9, n, []) { IconUrl = null!, FirstLevelId = 0 }).ToList();
        return Task.FromResult(new PackPage(page, TufPackCount));
    }

    // Level 1, folder 100 (levels 2 and 3, folder 101 with levels 4 and 5), levels 6 to 9: two folders, one nested.
    private static Task<IReadOnlyList<TufPackItem>> TufPackTree() {
        if(tufPackLevelsFail != null) return Task.FromException<IReadOnlyList<TufPackItem>>(tufPackLevelsFail);
        static TufPackItem L(int n) => new(n, TufFixture(n));
        IReadOnlyList<TufPackItem> tree = [L(1), new TufPackItem(100, "Selftest Folder A", [L(2), L(3), new TufPackItem(101, "Selftest Folder B", [L(4), L(5)])]),
            L(6), L(7), L(8), new(9, TufPackBlank(9))];
        return Task.FromResult(tree);
    }

    // A pack level the API gave no artist or creator: classic's pack card still shows its empty meta line with clears and likes.
    private static TufLevel TufPackBlank(int n) => new(TufId(n), "Selftest Song " + n, "", "", "G5", "#33AAFF", n * 3, n, new Uri("https://selftest.invalid/" + n));

    private static IEnumerator TufPacks(Action<string, bool, string> check, Action<string> info) {
        int network = SelfTestDoor.Network;
        TufPackService s = null;
        (TufPackSort sort, bool asc, TufPackLevelSort levelSort, bool levelAsc) was = default;
        yield return FormPage("tuf", TufPacksKey, () => {
            s = TufPackService.Instance;
            if(s == null) return;
            was = (s.Sort, s.Ascending, s.LevelSort, s.LevelAscending);
            s.ForgetForTest();
            TufPreviewCache.Clear();
            tufPackFail = null;
            tufPackLevelsFail = null;
            tufJpeg ??= TufJpeg();
            SelfTestDoor.FetchPacks = (_, _, _, offset, _, _) => TufPackFetch(offset);
            SelfTestDoor.FetchPackLevels = (_, _) => TufPackTree();
            SelfTestDoor.Preview = _ => tufJpeg;
        }, () => {
            TufPackService now = TufPackService.Instance;
            if(now != null && now == s) {
                if(now.Sort != was.sort) now.SetSort(was.sort);
                if(now.Ascending != was.asc) now.ToggleAscending();
                if(now.LevelSort != was.levelSort) now.SetLevelSort(was.levelSort);
                if(now.LevelAscending != was.levelAsc) now.ToggleLevelAscending();
            }
            TufPackService.Instance?.ForgetForTest();
            TufService.Instance?.ForgetForTest();
            TufPreviewCache.Clear();
            TufCards.Forget();
            tufPackFail = null;
            tufPackLevelsFail = null;
            SelfTestDoor.Defaults();
        }, () => Checks());
        TufPackService after = TufPackService.Instance;
        check($"{TufPacksKey}: after the section the service holds no fixture data",
            after == null || (after.Packs.Count == 0 && after.ListState == TufPackListState.Idle && after.SelectedPack == null && after.PackLevels.Count == 0
                && after.Query.Length == 0),
            $"packs={after?.Packs.Count} state={after?.ListState} open={after?.SelectedPack?.Id} levels={after?.PackLevels.Count} query='{after?.Query}'");
        check($"{TufPacksKey}: no request left the machine", SelfTestDoor.Network == network, $"network={SelfTestDoor.Network - network}");

        IEnumerator Checks() {
            if(s == null) {
                check($"{TufPacksKey}: the TUF pack service is up", false, "TufPackService.Instance");
                yield break;
            }
            yield return TufUntil(() => s.ListState == TufPackListState.Ready && s.Packs.Count > 0, 120);
            yield return TufPass();
            // As on the Levels page: the search field's placeholder names no row, the tagline is the header's description,
            // and a chip is no search row, so search finds Refresh.
            Covers(check, TufPacksKey, omitted: ["TUF_PACK_SEARCH_PLACEHOLDER", "TUF_PACKS_TAGLINE"], classic: TufPackClassicKeys());
            Searched(check, TufPacksKey, ["TUF_REFRESH"]);
            Translated(check, TufPacksKey, ["TUF_PACKS_TAGLINE"]);
            check($"{TufPacksKey}: the first page fills the pack cards from the fixture",
                TufPackCardIds().SequenceEqual(Enumerable.Range(1, TufPackPageSize).Select(n => "selftest-" + n)),
                $"cards={TufPackCardIds().Count} first={TufPackCardIds().FirstOrDefault()}");

            int fetches = SelfTestDoor.Fetches;
            TufScrollToEnd();
            yield return TufUntil(() => s.Packs.Count >= 2 * TufPackPageSize, 60);
            yield return Frames(10);
            check($"{TufPacksKey}: the list pages on scroll", s.Packs.Count == 2 * TufPackPageSize && SelfTestDoor.Fetches == fetches + 1
                && TufPackCardIds().Count == 2 * TufPackPageSize, $"packs={s.Packs.Count} cards={TufPackCardIds().Count} fetches={SelfTestDoor.Fetches - fetches}");

            yield return TufPackSorts(check, s);
            yield return TufPackOpen(check, s);
            yield return TufPackTreeChecks(check, s);
            yield return TufPackStates(check, s);
        }
    }

    // The classic pack browser's label keys, from a build under the classic menu's panel that is destroyed at once.
    private static string[] TufPackClassicKeys() {
        var host = new GameObject("FacetSelfTestTufPacks", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(UICore.Panel, false);
        try {
            TufPacksUI.Create(host);
            return [.. host.GetComponentsInChildren<Quartz.Localization.TextLocalization>(true).Select(l => l.Key).Where(k => !string.IsNullOrEmpty(k))];
        } finally {
            UnityEngine.Object.Destroy(host.gameObject);
        }
    }

    // Two of the window's refresh passes (every 100 ms), in real time: a When, a hook or a footer has caught up.
    private static IEnumerator TufPass() {
        for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 0.25f;) yield return null;
    }

    // The shown pack cards' ids, in order.
    private static List<string> TufPackCardIds() => [.. PageContent().Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith("tuf-pack-") && Displayed(e))
        .ToList().Select(e => e.name["tuf-pack-".Length..])];

    private static VisualElement TufPackCard(int n) {
        for(VisualElement e = PageContent().Q(name: "tuf-pack-selftest-" + n); e != null; e = e.parent)
            if(e.name == "image-cards-card") return e;
        return null;
    }

    // An opened pack's shown rows, in order: "F<key>" for a folder, the level number for a level.
    private static string TufPackRows() => string.Join(",", PageContent().Query<VisualElement>()
        .Where(e => e.name != null && Displayed(e) && (e.name.StartsWith("tuf-folder-") || e.name.StartsWith("tuf-card-"))).ToList()
        .Select(e => e.name.StartsWith("tuf-folder-") ? "F" + e.name["tuf-folder-".Length..]
            : int.TryParse(e.name["tuf-card-".Length..], out int id) ? (int.MaxValue - id).ToString() : "?"));

    private static IEnumerator TufPackSorts(Action<string, bool, string> check, TufPackService s) {
        string path = TufService.Instance == null ? null : SettingsOf(TufService.Instance)?.Path;
        byte[] before = Bytes(path);
        var bad = new List<string>();
        TufPackSort first = s.Sort;
        foreach(TufPackSort sort in new[] { TufPackSort.Recent, TufPackSort.Name, TufPackSort.Levels }.Where(x => x != first).Append(first)) {
            int fetches = SelfTestDoor.Fetches;
            if(PageContent().Q<Button>(TufPages.TufPacksPage.SortKey(sort)) is { } chip) Submit(chip);
            yield return TufUntil(() => s.ListState == TufPackListState.Ready, 60);
            if(s.Sort != sort || SelfTestDoor.Fetches != fetches + 1) bad.Add(TufPages.TufPacksPage.SortKey(sort));
        }
        bool asc = s.Ascending;
        for(int i = 0; i < 2; i++) {
            int fetches = SelfTestDoor.Fetches;
            Button dir = PageContent().Q<Button>("tuf-packs-direction");
            if(dir != null) Submit(dir);
            yield return TufUntil(() => s.ListState == TufPackListState.Ready, 60);
            yield return TufPass();
            bool want = i == 0 ? !asc : asc;
            if(dir == null || s.Ascending != want || dir.text != (want ? "↑" : "↓") || SelfTestDoor.Fetches != fetches + 1)
                bad.Add($"direction{i}({dir != null}/{s.Ascending}/{dir?.text}/{SelfTestDoor.Fetches - fetches})");
        }
        yield return Frames(30);
        check($"{TufPacksKey}: sort and direction refetch and are not saved", bad.Count == 0 && path != null && SameBytes(Bytes(path), before),
            $"failed={string.Join(",", bad)} settings unchanged={path != null && SameBytes(Bytes(path), before)}");
    }

    private static IEnumerator TufPackOpen(Action<string, bool, string> check, TufPackService s) {
        ScrollView sv = TufScroll();
        yield return TufPass();
        // Farther down than the opened tree reaches, short of the paging distance (400) from the list's end: back has to wait
        // for the list to be laid out again before its offset fits.
        float max = Mathf.Max(0f, sv.contentContainer.layout.height - sv.contentViewport.layout.height);
        sv.scrollOffset = new Vector2(0f, max > 750f ? max - 450f : Mathf.Min(300f, max));
        yield return TufPass();
        float listY = sv.scrollOffset.y;
        VisualElement card = TufPackCard(1);
        if(card != null) Click(card);
        yield return TufUntil(() => s.DetailState == TufPackListState.Ready, 60);
        yield return TufPass();
        string rows = TufPackRows();
        float top = sv.scrollOffset.y;
        bool backShown = Displayed(PageContent().Q(name: "tuf-back"));
        int listed = TufPackCardIds().Count;
        bool opened = s.SelectedPack?.Id == "selftest-1" && backShown && rows == "1,F100,6,7,8,9" && listed == 0;
        float treeMax = Mathf.Max(0f, sv.contentContainer.layout.height - sv.contentViewport.layout.height);
        Button back = PageContent().Q<Button>("tuf-back");
        if(back != null) Submit(back);
        yield return TufUntil(() => s.SelectedPack == null && Mathf.Abs(sv.scrollOffset.y - listY) < 1f, 60);
        yield return TufPass();
        check($"{TufPacksKey}: opening a pack shows its tree and scrolls to the top; back restores the list scroll",
            card != null && listY > treeMax + 1f && opened && top < 1f && s.SelectedPack == null && Mathf.Abs(sv.scrollOffset.y - listY) < 1f && TufPackCardIds().Count > 0,
            $"card={card != null} opened={opened} back={backShown} pack cards={listed} rows={rows} list y={listY:0} tree max={treeMax:0} top={top:0} after back={sv.scrollOffset.y:0} open={s.SelectedPack?.Id}");
    }

    private static IEnumerator TufPackTreeChecks(Action<string, bool, string> check, TufPackService s) {
        IEnumerator Open(int n) {
            VisualElement card = TufPackCard(n);
            if(card != null) Click(card);
            yield return TufUntil(() => s.SelectedPack?.Id == "selftest-" + n && s.DetailState == TufPackListState.Ready, 60);
            yield return TufPass();
        }
        IEnumerator Fold(long key) {
            if(PageContent().Q<Button>("tuf-folder-" + key) is { } b) Submit(b);
            yield return TufPass();
        }
        TufScroll().scrollOffset = Vector2.zero;
        yield return TufPass();
        yield return Open(1);
        yield return Fold(100);
        string Arrow() => PageContent().Q<Button>("tuf-folder-100")?.text?[..1];
        string one = TufPackRows() + Arrow();
        yield return Fold(101);
        string two = TufPackRows() + Arrow();
        yield return Fold(100);
        string shut = TufPackRows() + Arrow();
        yield return Fold(100);
        s.ClosePack();
        yield return TufPass();
        yield return Open(2);
        string other = TufPackRows();
        check($"{TufPacksKey}: a folder expands and collapses; opening another pack collapses all",
            one == "1,F100,2,3,F101,6,7,8,9▾" && two == "1,F100,2,3,F101,4,5,6,7,8,9▾" && shut == "1,F100,6,7,8,9▸" && other == "1,F100,6,7,8,9",
            $"open={one} nested={two} shut={shut} other pack={other}");

        // By clears (n * 3), high first: the levels move, the folder keeps its slot.
        if(PageContent().Q<Button>("TUF_SORT_CLEARS") is { } clears) Submit(clears);
        yield return TufPass();
        if(s.LevelAscending && PageContent().Q<Button>("tuf-packs-level-direction") is { } dir) Submit(dir);
        yield return TufPass();
        string sorted = TufPackRows();
        if(PageContent().Q<Button>("TUF_PACK_SORT_ORDER") is { } order) Submit(order);
        yield return TufPass();
        string back = TufPackRows();
        check($"{TufPacksKey}: level sort reorders levels and keeps folder slots", sorted == "9,F100,8,7,6,1" && back == "1,F100,6,7,8,9",
            $"by clears={sorted} pack order={back}");

        string meta = TufCard(9)?.Query<Label>().ToList().Select(l => l.text).FirstOrDefault(t => t.Contains("✓"));
        check($"{TufPacksKey}: a pack level with no artist or creator shows classic's meta line", meta == "  ·    ·  ✓ 27  ♥ 9", $"meta='{meta}'");

        TufLevel first = s.PackLevels.FirstOrDefault(l => l.Id == TufId(1));
        UImage img = TufImageOf(TufCard(1));
        yield return TufUntil(() => img?.image != null, 180);
        bool cached = TufPreviewCache.TryGet(TufId(1).ToString(), out Texture2D tex) && tex != null;
        check($"{TufPacksKey}: pack level cards show thumbnails", img?.image != null && cached && ReferenceEquals(img.image, tex),
            $"image={img?.image != null} cached by level id={cached}");

        if(first != null) {
            yield return Recorded(check, $"{TufPacksKey}: a pack level's action acts only through Do", () => Submit(TufCard(1)?.Q<Button>("tuf-action")),
                "tuf.act:" + first.Id);
            TufCountAct();
            tufActed = null;
            tufActRunner = null;
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
            check($"{TufPacksKey}: a pack level's action goes to the pack service's runner", ReferenceEquals(tufActed, first) && tufActRunner != null
                && ReferenceEquals(tufActRunner, s.Runner), $"level={(ReferenceEquals(tufActed, first) ? "pack's" : tufActed == null ? "none" : "other")}"
                + $" runner={(tufActRunner == null ? "none" : ReferenceEquals(tufActRunner, s.Runner) ? "pack service's" : "other")}");

            first.ChartsRoot = MainCore.Paths.TufLevelsPath;
            first.Charts = [Path.Combine(first.ChartsRoot, "a", "one.adofai"), Path.Combine(first.ChartsRoot, "b", "two.adofai")];
            first.State = TufItemState.ChooseChart;
            s.NotifyForTest();
            yield return TufPass();
            bool rows = PageContent().Q(name: "tuf-chart-1") != null && PageContent().Q(name: "tuf-chart-2") != null;
            yield return Recorded((n, ok, d) => check(n, ok && rows, $"{d} rows={rows}"), $"{TufPacksKey}: a chart row acts only through Do",
                () => Submit(PageContent().Q<Button>("tuf-chart-1")), "tuf.chart:" + first.Id + ":1");
            first.Charts = null;
            first.State = TufItemState.Download;
            s.NotifyForTest();
            yield return TufPass();
        } else {
            check($"{TufPacksKey}: the opened pack has fixture level 1", false, $"levels={s.PackLevels.Count}");
        }
        s.ClosePack();
        yield return TufPass();
    }

    private static IEnumerator TufPackStates(Action<string, bool, string> check, TufPackService s) {
        string Status() => string.Join("\n", PageContent().Query(name: "search-list-status").ToList().Where(Displayed)
            .SelectMany(e => e.Query<Label>().ToList()).Where(Displayed).Select(l => l.text));
        Button Retry() => PageContent().Query<Button>().ToList().FirstOrDefault(b => b.text == QuartzHost.Text("TUF_RETRY", "Retry") && Displayed(b));

        // A failed refresh keeps the packs already listed, as classic's: the states show from an empty list.
        tufPackFail = _ => new OperationCanceledException();
        s.ForgetForTest();
        s.RefreshPacks();
        yield return TufUntil(() => s.ListState == TufPackListState.Error, 60);
        yield return TufPass();
        Button go = PageContent().Q<Button>("tuf-offline-switch");
        check($"{TufPacksKey}: offline shows TUF_OFFLINE with the switch and Retry",
            Status().StartsWith(QuartzHost.Text("TUF_OFFLINE", "TUF could not be reached — you may be offline.")) && go != null && Retry() != null,
            $"status='{Status()}' switch={go != null} retry={Retry() != null}");
        if(go != null) yield return Recorded(check, $"{TufPacksKey}: the offline switch acts only through Do", () => Submit(go), "tuf.packs.offline");

        tufPackFail = _ => new InvalidOperationException("selftest api error");
        s.RefreshPacks();
        yield return TufUntil(() => s.ListState == TufPackListState.Error && !s.OfflineError, 60);
        yield return TufPass();
        bool api = Status().StartsWith(QuartzHost.Text("TUF_PACK_API_ERROR", "Could not load TUF packs."));
        tufPackFail = null;
        Button retry = Retry();
        if(retry != null) Submit(retry);
        yield return TufUntil(() => s.ListState == TufPackListState.Ready, 60);
        check($"{TufPacksKey}: an API error shows TUF_PACK_API_ERROR and retries on click", api && retry != null && s.ListState == TufPackListState.Ready,
            $"api={api} retry={retry != null} state={s.ListState}");

        tufPackFail = offset => offset > 0 ? new InvalidOperationException("selftest page error") : null;
        yield return TufPass();
        TufScrollToEnd();
        yield return TufUntil(() => s.ListState == TufPackListState.Error, 60);
        yield return TufPass();
        Button more = PageContent().Query(name: "search-list-footer").ToList().Where(Displayed).SelectMany(f => f.Query<Button>().ToList()).FirstOrDefault(Displayed);
        check($"{TufPacksKey}: a failed page shows TUF_RETRY in the footer", s.Packs.Count == TufPackPageSize && more?.text == QuartzHost.Text("TUF_RETRY", "Retry"),
            $"packs={s.Packs.Count} footer='{more?.text}'");
        tufPackFail = null;
        if(more != null) Submit(more);
        yield return TufUntil(() => s.ListState == TufPackListState.Ready && s.Packs.Count > TufPackPageSize, 60);

        TufScroll().scrollOffset = Vector2.zero;
        yield return TufPass();
        // An opened pack offline: TUF_OFFLINE with the switch; Retry loads the pack again, not the list.
        tufPackLevelsFail = new OperationCanceledException();
        if(TufPackCard(1) is { } offCard) Click(offCard);
        yield return TufUntil(() => s.DetailState == TufPackListState.Error, 60);
        yield return TufPass();
        bool offline = Status().StartsWith(QuartzHost.Text("TUF_OFFLINE", "TUF could not be reached — you may be offline."));
        bool detailSwitch = PageContent().Q<Button>("tuf-offline-switch") != null;
        tufPackLevelsFail = null;
        Button reload = Retry();
        TufPackListState listWas = s.ListState;
        if(reload != null) Submit(reload);
        yield return TufUntil(() => s.DetailState == TufPackListState.Ready, 60);
        check($"{TufPacksKey}: an opened pack offline shows TUF_OFFLINE with the switch and retries the pack",
            offline && detailSwitch && reload != null && s.DetailState == TufPackListState.Ready && s.SelectedPack?.Id == "selftest-1" && s.ListState == listWas,
            $"offline={offline} switch={detailSwitch} retry={reload != null} detail={s.DetailState} open={s.SelectedPack?.Id} list={listWas}->{s.ListState}");
        s.ClosePack();
        yield return TufPass();

        tufPackLevelsFail = new InvalidOperationException("selftest pack error");
        if(TufPackCard(1) is { } card) Click(card);
        yield return TufUntil(() => s.DetailState == TufPackListState.Error, 60);
        yield return TufPass();
        bool failed = Status().StartsWith(QuartzHost.Text("TUF_PACK_LEVELS_ERROR", "Could not load this pack."));
        tufPackLevelsFail = null;
        Button again = Retry();
        if(again != null) Submit(again);
        yield return TufUntil(() => s.DetailState == TufPackListState.Ready, 60);
        check($"{TufPacksKey}: a pack that fails to load shows TUF_PACK_LEVELS_ERROR and retries on click",
            failed && again != null && s.DetailState == TufPackListState.Ready, $"error={failed} retry={again != null} state={s.DetailState} status='{Status()}'");
        s.ClosePack();
        yield return TufPass();
    }
}
#endif
