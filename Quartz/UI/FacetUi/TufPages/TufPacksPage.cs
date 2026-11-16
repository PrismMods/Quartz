#if !IL2CPP
using Facet;
using Quartz.Features.Tuf;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TufPages;
/// <summary>
/// TUF ▸ Packs in the new menu, as the classic pack browser: the search-bound list of pack cards, paged as it nears its
/// end, and an opened pack's tree of folders and level cards. Pack levels act through the pack service's own runner. Sort
/// and direction are the session's, as classic's. With no pack service the page is its header alone; the service is
/// loaded and followed only while the page is shown, since the search index builds it detached.
/// </summary>
internal static class TufPacksPage {
    private static readonly TufPackSort[] Sorts = [TufPackSort.Recent, TufPackSort.Name, TufPackSort.Levels];
    private static readonly TufPackLevelSort[] LevelSorts = [TufPackLevelSort.PackOrder, TufPackLevelSort.Difficulty, TufPackLevelSort.Clears];
    private const float Indent = 26f;

    internal static void Build(VisualElement p) =>
        ShellPage.Feature(p, "tuf", "TUF_PACKS", "TUF_PACKS_TAGLINE", null, Cards);

    internal static string SortKey(TufPackSort sort) => sort switch {
        TufPackSort.Name => "TUF_PACK_SORT_NAME",
        TufPackSort.Levels => "TUF_PACK_SORT_LEVELS",
        _ => "TUF_SORT_RECENT",
    };

    internal static string LevelSortKey(TufPackLevelSort sort) => sort switch {
        TufPackLevelSort.Difficulty => "TUF_SORT_DIFFICULTY",
        TufPackLevelSort.Clears => "TUF_SORT_CLEARS",
        _ => "TUF_PACK_SORT_ORDER",
    };

    /// <summary>One shown row of an opened pack: a folder or a level, with its depth and its card key.</summary>
    private sealed class Row(TufPackItem item, int depth, string key) {
        internal readonly TufPackItem Item = item;
        internal readonly int Depth = depth;
        internal readonly string Key = key;
    }

    private static void Cards(VisualElement p) {
        TufPackService s = TufPackService.Instance;
        if(s == null) return;

        VisualElement list = Controls.When(p, () => s.SelectedPack == null);
        VisualElement tools = Controls.Card(list, null);
        // One row, as on Levels: sort chips, direction, and Refresh at the right end. A search finds Refresh: rows are what the
        // index notes, so Refresh stays its own unlabeled row, moved into the chips' bar.
        Button refresh = Controls.Action(tools, "", "TUF_REFRESH", s.RefreshPacks, o => o.Id = "TUF_REFRESH");
        Control<TufPackSort> sort = Controls.Chips(tools, Bind.To(() => s.Sort, s.SetSort), Sorts, SortKey, o => o.Id = "tuf-packs-sort");
        for(int i = 0; i < Sorts.Length && i < sort.Root.childCount; i++) sort.Root[i].name = SortKey(Sorts[i]);
        Arrow(sort.Root, "tuf-packs-direction", () => s.Ascending, s.ToggleAscending);
        ShellRows.Join(sort.Root, refresh).style.marginLeft = StyleKeyword.Auto;
        ShellRows.BarRow(sort.Root);

        VisualElement results = Controls.Card(list, null);
        Texts texts = new(s);
        SearchList packs = Controls.SearchList(results, o => {
            o.Query = Bind.To(() => s.Query, s.SetQuery);
            o.Placeholder = "TUF_PACK_SEARCH_PLACEHOLDER";
            o.State = () => s.ListState switch {
                TufPackListState.Loading => ListState.Loading,
                TufPackListState.Error when s.Packs.Count == 0 => ListState.Error,
                TufPackListState.Empty => ListState.Empty,
                TufPackListState.Idle => ListState.Idle,
                _ => ListState.Ready,
            };
            o.Message = texts.List;
            o.RetryLabel = "TUF_RETRY";
            o.Retry = s.RefreshPacks;
            o.ErrorExtra = box => Offline(box, s);
            o.More = () => More(s);
            o.EndText = texts.End;
            // The footer's Retry only: Facet's scroll-end test reads the list before a landed page is laid out and would ask for
            // one more; the page's own test below pages on scroll.
            o.LoadMore = () => {
                if(More(s) == MoreState.Error) s.LoadMore();
            };
        });
        // The list grows with its cards: a shrunk card would leave the cards past its end, where the scroll never reaches.
        results.style.flexShrink = packs.Root.style.flexShrink = packs.Body.style.flexShrink = 0f;
        var packKeys = new Dictionary<string, string>();
        ImageCards<TufPack> packCards = Controls.ImageCards<TufPack>(packs.Body, o => {
            o.Items = () => s.Packs;
            o.Key = k => packKeys.TryGetValue(k.Id, out string key) ? key : (packKeys[k.Id] = "pack-" + k.Id);
            o.Image = k => Image(o.Key(k), TufPreviewSource.ForPack(k.IconUrl, k.FirstLevelId));
            o.Body = PackBody;
            o.Click = s.OpenPack;
        });

        // An opened pack: its back row, its states, then the level sort and the tree. Built again for each pack.
        var rows = new List<Row>();
        var expanded = new HashSet<long>();
        string expandedFor = null;
        ImageCards<Row> tree = null;
        SearchList detail = null;
        VisualElement opened = Controls.Live(p, () => s.SelectedPack?.Id, box => {
            tree = null;
            detail = null;
            TufPack pack = s.SelectedPack;
            if(pack == null) return;
            if(expandedFor != pack.Id) {
                expandedFor = pack.Id;
                expanded.Clear();
            }
            VisualElement card = Controls.Card(box, null);
            Button back = Ui.Btn(card, "", s.ClosePack);
            back.name = "tuf-back";
            back.text = "←   " + pack.Name;
            back.style.unityTextAlign = TextAnchor.MiddleLeft;
            back.style.unityFontStyleAndWeight = FontStyle.Bold;
            Label count = TufCards.Literal(back, LevelCount(pack.LevelCount), Role.TextMuted);
            count.style.position = Position.Absolute;
            count.style.right = 12f;
            count.style.top = 0f;
            count.style.bottom = 0f;
            count.style.unityTextAlign = TextAnchor.MiddleRight;

            detail = Controls.SearchList(card, o => {
                o.Search = false;
                o.State = () => s.DetailState switch {
                    TufPackListState.Loading => ListState.Loading,
                    TufPackListState.Error when s.PackLevels.Count == 0 => ListState.Error,
                    TufPackListState.Empty => ListState.Empty,
                    TufPackListState.Idle => ListState.Idle,
                    _ => ListState.Ready,
                };
                o.Message = texts.Detail;
                o.RetryLabel = "TUF_RETRY";
                o.Retry = s.RetryPackLevels;
                o.ErrorExtra = b => Offline(b, s);
            });
            card.style.flexShrink = detail.Root.style.flexShrink = detail.Body.style.flexShrink = 0f;
            Control<TufPackLevelSort> levelSort = Controls.Chips(detail.Body, Bind.To(() => s.LevelSort, s.SetLevelSort), LevelSorts, LevelSortKey,
                o => o.Id = "tuf-packs-level-sort");
            for(int i = 0; i < LevelSorts.Length && i < levelSort.Root.childCount; i++) levelSort.Root[i].name = LevelSortKey(LevelSorts[i]);
            Arrow(levelSort.Root, "tuf-packs-level-direction", () => s.LevelAscending, s.ToggleLevelAscending);
            Rows(s, expanded, rows);
            tree = Controls.ImageCards<Row>(detail.Body, o => {
                o.Items = () => rows;
                o.Key = r => r.Key;
                o.Image = r => r.Item.Level is { } l ? TufCards.Image(l, TufService.Instance) : null;
                o.Body = (c, r) => {
                    if(r.Item.Level is { } l) {
                        VisualElement inner = Ui.Box(c);
                        inner.style.marginLeft = r.Depth * Indent;
                        TufCards.Body(inner, l, new TufCardOptions { Runner = s.Runner, Badge = false, Delete = false, Rollback = false, PackMeta = true });
                    } else {
                        Folder(c, r, expanded.Contains(r.Item.Key), () => {
                            if(!expanded.Add(r.Item.Key)) expanded.Remove(r.Item.Key);
                            Rows(s, expanded, rows);
                            tree?.Refresh();
                            tree?.Redraw(r);
                        });
                    }
                };
                o.Below = r => r.Item.Level is { } l ? TufCards.Below(l, s.LaunchChart) : null;
            });
        });

        ScrollView scroll = null;
        bool wasOpen = false;
        float listY = 0f;
        float? restore = null;
        float restoreUntil = 0f;
        (bool, bool, bool) shown = default;
        // What the shown rows are made from: a download's progress notifies often and changes none of it, so the rows (and
        // their sorted copies) are made again only when this changes. A folder's toggle makes them itself.
        (ImageCards<Row>, TufPack, TufPackListState, TufPackItem, int, TufPackLevelSort, bool) rowsFor = default;
        // Per level id: the object last drawn and what its card showed, as on the Levels page.
        var parts = new Dictionary<int, (TufLevel, (TufItemState, bool, int))>();
        Action changed = () => {
            bool open = s.SelectedPack != null;
            if(open != wasOpen) {
                wasOpen = open;
                // Opening a pack starts its tree at the top; going back finds the list where it was left.
                if(open) {
                    listY = scroll?.scrollOffset.y ?? 0f;
                    if(scroll != null) scroll.scrollOffset = Vector2.zero;
                } else {
                    restore = listY;
                    restoreUntil = Time.realtimeSinceStartup + 1f;
                }
                Anim.FadeIn(open ? opened : list, 200);   // the view switch fades in over 0.2 s
            }
            packs.Refresh();
            packCards.Refresh();
            if(tree == null) {
                parts.Clear();
                return;
            }
            detail?.Refresh();
            var from = (tree, s.SelectedPack, s.DetailState, s.PackItems.Count > 0 ? s.PackItems[0] : null, s.PackItems.Count, s.LevelSort, s.LevelAscending);
            if(!from.Equals(rowsFor)) {
                rowsFor = from;
                Rows(s, expanded, rows);
            }
            // Still a pass: a level that now asks for a chart gains its rows below the card here.
            tree.Refresh();
            var now = (s.IsBusy, s.IsLaunching, TufService.Instance?.ShowPreviews ?? true);
            bool all = !now.Equals(shown);
            shown = now;
            foreach(Row r in rows) {
                if(r.Item.Level is not { } l) continue;
                var part = (l.State, !string.IsNullOrEmpty(l.Error), l.Charts?.Count ?? 0);
                if(all || (parts.TryGetValue(l.Id, out var was) && (!ReferenceEquals(was.Item1, l) || !was.Item2.Equals(part)))) tree.Redraw(r);
                parts[l.Id] = (l, part);
            }
            TufCards.Relabel(s.PackLevels, s.Runner);
        };
        Action images = () => {
            packCards.ImagesChanged();
            tree?.ImagesChanged();
        };
        // ponytail: Facet's own scroll-end test mixes panel and local units, so on a scaled menu it never fires; this one
        // measures in the scroll content's own units, as the Levels page's. Drop it once Facet's ScrollEnd converts its units.
        // asked is the list's size and height when it last asked: a page that landed but is not laid out yet asks nothing more.
        (int count, float height) asked = (-1, -1f);
        Action nearEnd = () => {
            if(scroll == null || restore != null || s.SelectedPack != null || packs.Body.panel == null || !(packs.Body.layout.height > 0f)
                || s.ListState != TufPackListState.Ready || More(s) != MoreState.None) return;
            float height = packs.Body.layout.height;
            float end = packs.Body.ChangeCoordinatesTo(scroll.contentContainer, new Vector2(0f, height)).y;
            if(scroll.scrollOffset.y + scroll.contentViewport.layout.height < end - 400f
                || (s.Packs.Count > asked.count && Mathf.Approximately(height, asked.height))) return;
            asked = (s.Packs.Count, height);
            s.LoadMore();
        };
        Action<float> scrolled = _ => nearEnd();
        Ui.WhileAttached(results, () => {
            wasOpen = s.SelectedPack != null;
            shown = default;
            rowsFor = default;
            parts.Clear();
            s.Changed += changed;
            TufPreviewCache.Changed += images;
            scroll = p.GetFirstAncestorOfType<ScrollView>();
            if(scroll != null) scroll.verticalScroller.valueChanged += scrolled;
            s.EnsureLoaded();
        }, () => {
            if(scroll != null) scroll.verticalScroller.valueChanged -= scrolled;
            scroll = null;
            s.Changed -= changed;
            TufPreviewCache.Changed -= images;
            parts.Clear();
            TufCards.Forget();
        });
        // Every frame while shown: the list's saved scroll comes back once the list is laid out again (the list returns on a
        // later refresh pass; a shorter tree clamps the offset until then), giving up after a second of real time.
        Ui.Every(results, 0, () => {
            if(restore is not { } y || scroll == null) return;
            scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, y);
            if(Mathf.Abs(scroll.scrollOffset.y - y) < 1f || Time.realtimeSinceStartup > restoreUntil) restore = null;
        });
        // A list that ends inside the view asks for the next page, read on a quarter-second tick (a page landed, the window
        // resized), never from a layout not yet made.
        Ui.Every(results, 250, nearEnd);
    }

    // The arrow alone says the direction: no tint, unlike classic's chip.
    internal static void Arrow(VisualElement bar, string name, Func<bool> ascending, Action toggle) {
        Button b = Ui.Btn(bar, "", toggle);
        b.name = name;
        b.style.marginLeft = Theme.For(b).Sizes.Gap;
        Words.Label(b, "");
        Controls.Hook(b, o => o.VisibleWhen = () => {
            string arrow = ascending() ? "↑" : "↓";
            if(b.text != arrow) b.text = arrow;
            return true;
        });
    }

    // A pack or level thumbnail while previews are on (classic reads the Levels service's switch), asked for once.
    private static Texture Image(string key, TufPreviewSource source) {
        if(!(TufService.Instance?.ShowPreviews ?? true)) return null;
        if(TufPreviewCache.TryGet(key, out Texture2D tex) && tex != null) return tex;
        TufPreviewCache.Request(key, source);
        return null;
    }

    private static void PackBody(VisualElement c, TufPack pack) {
        VisualElement box = Ui.Box(c);
        box.name = "tuf-pack-" + pack.Id;
        box.style.paddingLeft = 10f;
        Label name = TufCards.Literal(box, pack.Name ?? "", Role.Text);
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        name.style.fontSize = Theme.For(c).Sizes.Body * 1.3f;
        string preview = pack.Preview.Count > 0 ? "  ·  " + string.Join(", ", pack.Preview) : "";
        TufCards.Literal(box, LevelCount(pack.LevelCount) + $"  ·  {pack.Owner}  ·  ♥ {pack.Favorites:N0}" + preview, Role.TextMuted);
    }

    private static void Folder(VisualElement c, Row r, bool open, Action toggle) {
        Button b = Ui.Btn(c, "", toggle);
        b.name = "tuf-folder-" + r.Item.Key;
        b.text = (open ? "▾   " : "▸   ") + r.Item.Name;
        b.style.marginLeft = r.Depth * Indent;
        b.style.marginRight = 0f;
        b.style.unityTextAlign = TextAnchor.MiddleLeft;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        Label count = TufCards.Literal(b, LevelCount(r.Item.LevelCount), Role.TextMuted);
        count.style.position = Position.Absolute;
        count.style.right = 12f;
        count.style.top = 0f;
        count.style.bottom = 0f;
        count.style.unityTextAlign = TextAnchor.MiddleRight;
    }

    // The opened pack's shown rows, in classic's order: sorting moves levels only, folders keep their slots, and an
    // expanded folder's rows follow it one level deeper.
    private static void Rows(TufPackService s, HashSet<long> expanded, List<Row> rows) {
        rows.Clear();
        if(s.SelectedPack != null && s.DetailState == TufPackListState.Ready) Add(s, expanded, rows, s.PackItems, 0);
    }

    private static void Add(TufPackService s, HashSet<long> expanded, List<Row> rows, IReadOnlyList<TufPackItem> items, int depth) {
        foreach(TufPackItem item in Sorted(s, items)) {
            if(item.IsFolder) {
                rows.Add(new Row(item, depth, "folder-" + item.Key));
                if(expanded.Contains(item.Key)) Add(s, expanded, rows, item.Children, depth + 1);
            } else {
                rows.Add(new Row(item, depth, TufCards.Key(item.Level)));
            }
        }
    }

    private static IReadOnlyList<TufPackItem> Sorted(TufPackService s, IReadOnlyList<TufPackItem> items) {
        if(s.LevelSort == TufPackLevelSort.PackOrder) return items;
        List<TufPackItem> result = [.. items];
        List<int> slots = [];
        List<TufPackItem> levels = [];
        for(int i = 0; i < result.Count; i++) {
            if(result[i].IsFolder) continue;
            slots.Add(i);
            levels.Add(result[i]);
        }
        IEnumerable<TufPackItem> sorted = (s.LevelSort, s.LevelAscending) switch {
            (TufPackLevelSort.Difficulty, true) => levels.OrderBy(i => i.Level?.DifficultyRank ?? 0),
            (TufPackLevelSort.Difficulty, false) => levels.OrderByDescending(i => i.Level?.DifficultyRank ?? 0),
            (TufPackLevelSort.Clears, true) => levels.OrderBy(i => i.Level?.Clears ?? 0),
            _ => levels.OrderByDescending(i => i.Level?.Clears ?? 0),
        };
        int slot = 0;
        foreach(TufPackItem item in sorted) result[slots[slot++]] = item;
        return result;
    }

    private static MoreState More(TufPackService s) {
        if(s.HasMore) return s.LoadingMore ? MoreState.Loading : s.ListState == TufPackListState.Error ? MoreState.Error : MoreState.None;
        return s.Packs.Count > 0 ? MoreState.End : MoreState.None;
    }

    // Offline: switch to the levels on disk on the Levels page, or try again.
    private static void Offline(VisualElement box, TufPackService s) {
        if(!s.OfflineError) return;
        int installed = TufService.Instance?.InstalledCount ?? 0;
        Button go = Ui.Btn(box, "", () => QuartzShell.Do("tuf.packs.offline", () => {
            TufService.Instance?.ShowInstalledLevels();
            QuartzShell.Go(TufModule.LevelsPageKey);
        }));
        go.name = "tuf-offline-switch";
        Words.Label(go, "");
        go.text = installed > 0
            ? string.Format(QuartzHost.Text("TUF_OFFLINE_SWITCH", "Switch to Installed ({0})"), installed)
            : QuartzHost.Text("TUF_OFFLINE_SWITCH_EMPTY", "Switch to Installed");
        if(installed > 0) Paint.Look(go, Theme.For(go).Looks.AccentButton);
    }

    private static string LevelCount(int n) => string.Format(QuartzHost.Text("TUF_PACK_LEVEL_COUNT", "{0} levels"), n);

    /// <summary>The status and footer texts, made again only when what they show changes: the lists read them on every
    /// refresh pass.</summary>
    private sealed class Texts(TufPackService s) {
        private (TufPackListState, bool, string) listFor = ((TufPackListState)(-1), false, null);
        private (TufPackListState, bool, string) detailFor = ((TufPackListState)(-1), false, null);
        private string list = "", detail = "";

        internal string List() {
            var key = (s.ListState, s.OfflineError, s.ListError);
            if(key.Equals(listFor)) return list;
            listFor = key;
            return list = s.ListState switch {
                TufPackListState.Loading => QuartzHost.Text("TUF_PACK_LOADING", "Loading packs…"),
                TufPackListState.Error when s.OfflineError => Offline(s.ListError),
                TufPackListState.Error => QuartzHost.Text("TUF_PACK_API_ERROR", "Could not load TUF packs.") + "\n" + s.ListError,
                TufPackListState.Empty => QuartzHost.Text("TUF_PACK_EMPTY", "No packs matched your search."),
                _ => "",
            };
        }

        internal string Detail() {
            var key = (s.DetailState, s.OfflineError, s.DetailError);
            if(key.Equals(detailFor)) return detail;
            detailFor = key;
            return detail = s.DetailState switch {
                TufPackListState.Loading => QuartzHost.Text("TUF_PACK_LOADING_LEVELS", "Loading pack levels…"),
                TufPackListState.Error when s.OfflineError => Offline(s.DetailError),
                TufPackListState.Error => QuartzHost.Text("TUF_PACK_LEVELS_ERROR", "Could not load this pack.") + "\n" + s.DetailError,
                TufPackListState.Empty => QuartzHost.Text("TUF_PACK_NO_LEVELS", "This pack has no playable levels."),
                _ => "",
            };
        }

        internal string End() => QuartzHost.Text("TUF_END", "End of results");

        private static string Offline(string error) => QuartzHost.Text("TUF_OFFLINE", "TUF could not be reached — you may be offline.") + "\n" + error;
    }
}
#endif
