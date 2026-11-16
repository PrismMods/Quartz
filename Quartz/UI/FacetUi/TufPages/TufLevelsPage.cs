#if !IL2CPP
using Facet;
using Quartz.Features.Tuf;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TufPages;
/// <summary>
/// TUF ▸ Levels in the new menu: the filters, then the search-bound list of level cards over their thumbnails, paged as
/// the list nears its end. With no TUF service the page is its header alone. The service is loaded, followed and its
/// thumbnails asked for only while the page is shown: the search index builds it detached.
/// </summary>
internal static class TufLevelsPage {
#if DEBUG
    /// <summary>Self-tests only: how long the last Changed took to bring the page up to date, in ms.</summary>
    internal static double LastChangedMs;
#endif

    internal static void Build(VisualElement p) =>
        ShellPage.Feature(p, "tuf", "TUF_LEVELS", "TUF_TAGLINE", null, Cards);

    private static void Cards(VisualElement p) {
        TufService s = TufService.Instance;
        if(s == null) return;
        TufFilters.Build(Controls.Card(p, null), s);

        VisualElement results = Controls.Card(p, null);
        Texts texts = new(s);
        SearchList list = Controls.SearchList(results, o => {
            o.Query = Bind.To(() => s.Query, s.SetQuery);
            o.Placeholder = "TUF_SEARCH_PLACEHOLDER";
            o.State = () => State(s);
            o.Message = texts.Message;
            o.RetryLabel = "TUF_RETRY";
            o.Retry = s.Refresh;
            o.ErrorExtra = box => Offline(box, s);
            o.More = () => More(s);
            o.EndText = texts.End;
            o.LoadMore = s.LoadMore;
        });
        // The list grows with its cards: a shrunk card would leave the cards past its end, where the scroll never reaches.
        results.style.flexShrink = list.Root.style.flexShrink = list.Body.style.flexShrink = 0f;
        ImageCards<TufLevel> cards = Controls.ImageCards<TufLevel>(list.Body, o => {
            o.Items = () => s.Levels;
            o.Key = TufCards.Key;
            o.Grid = () => s.GridView;
            o.Image = l => TufCards.Image(l, s);
            o.Body = (c, l) => TufCards.Body(c, l, new TufCardOptions { Service = s, Runner = s.Runner, Grid = s.GridView });
            o.Below = l => TufCards.Below(l, s.LaunchChart);
        });

        // ponytail: Facet's own scroll-end test mixes panel and local units, so on a scaled menu it never fires; this one
        // measures in the scroll content's own units. Drop it once Facet's ScrollEnd converts its units.
        // The cards change in the frame a page lands and are laid out by the end of the next one; read before that, the
        // old height puts the end in view and asks for another page.
        ScrollView scroll = null;
        int drawnCount = -1, drawnFrame = -2;
        Action nearEnd = () => {
            if(scroll == null || list.Body.panel == null || State(s) != ListState.Ready || More(s) != MoreState.None
                || Time.frameCount - drawnFrame <= 1) return;
            float end = list.Body.ChangeCoordinatesTo(scroll.contentContainer, new Vector2(0f, list.Body.layout.height)).y;
            if(scroll.scrollOffset.y + scroll.contentViewport.layout.height >= end - 400f) s.LoadMore();
        };
        // Read after a scroll and on the page's quarter-second tick (a page landed, the window resized), never from a
        // layout not yet made.
        Action<float> scrolled = _ => nearEnd();
        (bool, bool, bool, bool, int) shown = default;
        // Per level id: the object last drawn and what its card showed. The service makes new objects for the same ids (a
        // refresh, the Installed list, a sort), and a card's buttons act on the object it was drawn with.
        var parts = new Dictionary<int, (TufLevel, (TufItemState, TufUpdateState, int, bool, bool, int))>();
        Action changed = () => {
#if DEBUG
            var clock = System.Diagnostics.Stopwatch.StartNew();
#endif
            list.Refresh();
            cards.Refresh();
            if(s.Levels.Count != drawnCount) {
                drawnCount = s.Levels.Count;
                drawnFrame = Time.frameCount;
            }
            var now = Global(s);
            bool all = !now.Equals(shown);
            if(all) {
                foreach(TufLevel l in s.Levels) cards.Redraw(l);
                if(now.Item3 != shown.Item3) cards.ImagesChanged();
                shown = now;
            }
            if(s.Levels.Count == 0) parts.Clear();   // a new query or view: the next cards are drawn fresh
            foreach(TufLevel l in s.Levels) {
                var part = Part(l, s);
                if(parts.TryGetValue(l.Id, out var was) && !all && (!ReferenceEquals(was.Item1, l) || !was.Item2.Equals(part))) cards.Redraw(l);
                parts[l.Id] = (l, part);
            }
            TufCards.Relabel(s.Levels, s.Runner);
#if DEBUG
            LastChangedMs = clock.Elapsed.TotalMilliseconds;
#endif
        };
        Action images = cards.ImagesChanged;
        Ui.WhileAttached(results, () => {
            shown = Global(s);
            parts.Clear();
            s.Changed += changed;
            TufCards.Stale += changed;
            TufPreviewCache.Changed += images;
            scroll = results.GetFirstAncestorOfType<ScrollView>();
            if(scroll != null) scroll.verticalScroller.valueChanged += scrolled;
            s.EnsureLoaded();
        }, () => {
            if(scroll != null) scroll.verticalScroller.valueChanged -= scrolled;
            scroll = null;
            s.Changed -= changed;
            TufCards.Stale -= changed;
            parts.Clear();
            TufPreviewCache.Changed -= images;
            TufCards.Forget();
        });
        // An armed delete lapses after 4 s; a list that ends inside the view asks for the next page.
        Ui.Every(results, 250, () => {
            if(TufCards.Armed != 0 && Time.unscaledTime >= TufCards.ArmedUntil) TufCards.Disarm();
            nearEnd();
        });
    }

    private static ListState State(TufService s) => s.State switch {
        TufListState.Loading => ListState.Loading,
        TufListState.Error when s.Levels.Count == 0 => ListState.Error,
        TufListState.Empty => ListState.Empty,
        TufListState.Idle => ListState.Idle,
        _ => ListState.Ready,
    };

    private static MoreState More(TufService s) {
        if(s.HasMore) return s.LoadingMore ? MoreState.Loading : s.State == TufListState.Error ? MoreState.Error : MoreState.None;
        return s.Levels.Count > 0 ? MoreState.End : MoreState.None;
    }

    // Offline, outside Installed: switch to the levels on disk, or try again.
    private static void Offline(VisualElement box, TufService s) {
        if(!s.OfflineError || s.ShowInstalled) return;
        int installed = s.InstalledCount;
        Button go = Ui.Btn(box, "", () => QuartzShell.Do("tuf.offline", () => {
            TufCards.Disarm();
            s.ShowInstalledLevels();
        }));
        go.name = "tuf-offline-switch";
        Words.Label(go, "");
        go.text = installed > 0
            ? string.Format(QuartzHost.Text("TUF_OFFLINE_SWITCH", "Switch to Installed ({0})"), installed)
            : QuartzHost.Text("TUF_OFFLINE_SWITCH_EMPTY", "Switch to Installed");
        if(installed > 0) Paint.Look(go, Theme.For(go).Looks.AccentButton);
    }

    /// <summary>The status and footer texts, made again only when what they show changes: the list reads them on every
    /// refresh pass.</summary>
    private sealed class Texts(TufService s) {
        private (TufListState, bool, bool, string, string) messageFor;
        private string message = "";
        private (bool, int, long, int) endFor = (false, -1, -1, -1);
        private string end = "";

        internal string Message() {
            var key = (s.State, s.OfflineError, s.ShowInstalled, s.Error, s.Query);
            if(key.Equals(messageFor)) return message;
            messageFor = key;
            return message = s.State switch {
                TufListState.Loading => QuartzHost.Text("TUF_LOADING", "Loading levels…"),
                TufListState.Error when s.OfflineError && !s.ShowInstalled =>
                    QuartzHost.Text("TUF_OFFLINE", "TUF could not be reached — you may be offline.") + "\n" + s.Error,
                TufListState.Error => QuartzHost.Text("TUF_API_ERROR", "Could not load TUF levels.") + "\n" + s.Error,
                TufListState.Empty when !s.ShowInstalled => QuartzHost.Text("TUF_EMPTY", "No levels matched your search."),
                TufListState.Empty when string.IsNullOrEmpty(s.Query) => QuartzHost.Text("TUF_INSTALLED_EMPTY", "You have not downloaded any levels yet."),
                TufListState.Empty => QuartzHost.Text("TUF_INSTALLED_NO_MATCH", "No downloaded level matched your search."),
                _ => "",
            };
        }

        internal string End() {
            var key = (s.ShowInstalled, s.Levels.Count, s.LibraryBytes, s.UpdatesAvailable);
            if(key.Equals(endFor)) return end;
            endFor = key;
            if(!s.ShowInstalled) return end = QuartzHost.Text("TUF_END", "End of results");
            string text = string.Format(QuartzHost.Text("TUF_INSTALLED_COUNT", "{0} level(s) in your library"), s.Levels.Count);
            if(s.LibraryBytes > 0) text += "  ·  " + TufDiskSpace.Describe(s.LibraryBytes);
            if(s.UpdatesAvailable > 0) text += "  ·  " + string.Format(QuartzHost.Text("TUF_UPDATES_READY", "{0} update(s) available"), s.UpdatesAvailable);
            return end = text;
        }
    }

    // What every card shows of the service (busy, launching, previews, grid, info revision): a change redraws every card.
    // Classic's list signature also holds the list's own state (loading, paging, offline); none of it shows on a card, so a
    // page landing keeps every card as it is.
    private static (bool, bool, bool, bool, int) Global(TufService s) => (s.IsBusy, s.IsLaunching, s.ShowPreviews, s.GridView, s.InfoRevision);

    // What one card shows of its level: a change redraws that card.
    private static (TufItemState, TufUpdateState, int, bool, bool, int) Part(TufLevel l, TufService s) =>
        (l.State, l.UpdateState, s.SnapshotCount(l.Id), l.InstallFolder != null, !string.IsNullOrEmpty(l.Error), l.Charts?.Count ?? 0);
}
#endif
