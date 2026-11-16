#if !IL2CPP
using Facet;
using Quartz.Features.Tuf;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TufPages;
/// <summary>
/// TUF Levels' filters in the new menu, in four rows: sort, direction and Refresh; Installed, Grid View and Quantum tiles with
/// Updates; the Special difficulties; the difficulty range (and the Quantum range while Quantum is on). Each control reads
/// the service on refresh, so the classic page and this one always agree; the service's setters save, as classic's do.
/// </summary>
internal static class TufFilters {
    private static readonly TufSort[] Sorts = [TufSort.Recent, TufSort.Difficulty, TufSort.Clears, TufSort.Likes];

    internal static void Build(VisualElement card, TufService s) {
        // Row 1: sort chips, direction, and Refresh at the right end. A search finds Refresh: rows are what the index notes,
        // so Refresh stays its own unlabeled row, moved into the chips' bar.
        Control<TufSort> sort = Controls.Chips(card, Bind.To(() => s.Sort, s.SetSort), Sorts, SortKey, o => o.Id = "tuf-sort");
        for(int i = 0; i < Sorts.Length && i < sort.Root.childCount; i++) sort.Root[i].name = SortKey(Sorts[i]);
        TufPacksPage.Arrow(sort.Root, "tuf-direction", () => s.Ascending, s.ToggleAscending);
        Button refresh = Controls.Action(card, "", "TUF_REFRESH", s.Refresh, o => o.Id = "TUF_REFRESH");
        VisualElement refreshRow = ShellRows.Join(sort.Root, refresh);
        refreshRow.style.marginLeft = StyleKeyword.Auto;

        // Row 2: Installed, Grid View and Quantum as one tile line over their three switches, Updates at its right.
        const int Installed = 0, Grid = 1, Quantum = 2;
        MaskTiles views = Controls.MaskTiles(card, Bind.To(
            () => (s.ShowInstalled ? 1 << Installed : 0) | (s.GridView ? 1 << Grid : 0) | (s.QuantumEnabled ? 1 << Quantum : 0),
            v => {
                if(((v >> Installed & 1) != 0) != s.ShowInstalled) QuartzShell.Do("tuf.installed", () => {
                    TufCards.Disarm();
                    s.ToggleInstalled();
                });
                if(((v >> Grid & 1) != 0) != s.GridView) s.SetGridView(!s.GridView);
                bool q = (v >> Quantum & 1) != 0;
                if(q == s.QuantumEnabled) return;
                if(q) s.SetQuantumRange(s.QuantumMinIndex, s.QuantumMaxIndex);
                else s.ClearQuantum();
            }), [
                new MaskTile("TUF_INSTALLED", Installed, o => { o.Id = "TUF_INSTALLED"; o.Tooltip = "DESC_TUF_INSTALLED"; }),
                new MaskTile("TUF_GRID_VIEW", Grid, o => { o.Id = "TUF_GRID_VIEW"; o.Tooltip = "DESC_TUF_GRID_VIEW"; }),
                new MaskTile("TUF_QUANTUM", Quantum, o => o.Id = "TUF_QUANTUM"),
            ], o => {
                o.Columns = 3;
                o.Undoable = false;   // view switches, not settings a player undoes
            });
        VisualElement grid = views.Tiles[0].Root.parent;

        Button updates = Controls.Action(card, "", "TUF_CHECK_UPDATES", () => {
            TufCards.Disarm();
            if(s.UpdatesAvailable > 0) QuartzShell.Do("tuf.updateall", s.UpdateAll);
            else QuartzShell.Do("tuf.checkupdates", s.CheckUpdates);
        }, o => {
            o.Id = "TUF_CHECK_UPDATES";
            o.Tooltip = "DESC_TUF_CHECK_UPDATES";
        });
        Words.Label(updates, "");
        // Read on every refresh pass: the text is made again only when the counts it shows change.
        (bool, int, int, int) updatesFor = (false, -1, -1, -1);
        Controls.Hook(updates, o => o.VisibleWhen = () => {
            var key = (s.CheckingUpdates, s.UpdateCheckDone, s.UpdateCheckTotal, s.UpdatesAvailable);
            if(!key.Equals(updatesFor)) {
                updatesFor = key;
                string text = UpdatesText(s);
                if(updates.text != text) updates.text = text;
            }
            Paint.Select(updates, s.UpdatesAvailable > 0);
            return true;
        });

        TileLine(grid, updates);

        VisualElement special = ShellRows.Section(card, "TUF_SPECIAL", o => {
            o.Id = "TUF_SPECIAL";
            o.Open = false;
        });
        foreach(string name in TufDifficultyFilter.SpecialNames) {
            string n = name;
            View(special, "TUF_SPECIAL_" + n.ToUpperInvariant(), n, () => s.DifficultyFilter.IsSelected(n), _ => s.ToggleSpecialDifficulty(n));
        }

        Vector2? held = null;
        Control<Vector2> range = Controls.Range(card, "TUF_DIFFICULTY_FILTER", "",
            Bind.To(() => held ?? new Vector2(s.MinDifficultyIndex, s.MaxDifficultyIndex), v => held = v), o => {
                o.HighId = "TUF_DIFFICULTY_FILTER-high";
                o.Names = TufDifficultyFilter.RankedNames;
                o.Track = t => Gradient(t, TufDifficultyRangeBar.PguStops);
            });
        range.Committed += v => {
            held = null;
            s.SetDifficultyRange(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
        };

        VisualElement quantum = Controls.When(card, () => s.QuantumEnabled);
        Vector2? heldQ = null;
        Control<Vector2> qrange = Controls.Range(quantum, "tuf-quantum-range", "",
            Bind.To(() => heldQ ?? new Vector2(s.QuantumMinIndex, s.QuantumMaxIndex), v => heldQ = v), o => {
                o.HighId = "tuf-quantum-range-high";
                o.Names = TufDifficultyRangeBar.CompactNames(TufDifficultyFilter.QuantumNames);
                o.Track = t => Gradient(t, TufDifficultyRangeBar.QuantumStops);
            });
        qrange.Committed += v => {
            heldQ = null;
            s.SetQuantumRange(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
        };
    }

    internal static string SortKey(TufSort s) => s switch {
        TufSort.Difficulty => "TUF_SORT_DIFFICULTY",
        TufSort.Clears => "TUF_SORT_CLEARS",
        TufSort.Likes => "TUF_SORT_LIKES",
        _ => "TUF_SORT_RECENT",
    };

    private static string UpdatesText(TufService s) =>
        s.CheckingUpdates ? string.Format(QuartzHost.Text("TUF_CHECKING_UPDATES", "{0}/{1}"), s.UpdateCheckDone, s.UpdateCheckTotal)
        : s.UpdatesAvailable > 0 ? string.Format(QuartzHost.Text("TUF_UPDATE_ALL", "Update all ({0})"), s.UpdatesAvailable)
        : QuartzHost.Text("TUF_CHECK_UPDATES", "Check Level Updates");

    // Facet workaround: a tile grid fills its card row. The grid and b's row go into one line that takes the grid's card-row
    // inset and rule; the grid shrinks to what b leaves (its tiles size from its own width).
    private static void TileLine(VisualElement grid, Button b) {
        var line = new VisualElement();
        line.style.flexDirection = FlexDirection.Row;
        line.style.alignItems = Align.Center;
        grid.parent.Insert(grid.parent.IndexOf(grid), line);
        line.style.paddingLeft = grid.style.paddingLeft;
        line.style.paddingRight = grid.style.paddingRight;
        line.style.borderTopWidth = grid.style.borderTopWidth;
        // ponytail: the rule's colour is copied once; a theme switch shows it on the next page build.
        line.style.borderTopColor = grid.style.borderTopColor;
        line.Add(grid);
        grid.style.paddingLeft = grid.style.paddingRight = 0f;
        grid.style.borderTopWidth = 0f;
        grid.style.flexBasis = 0f;
        grid.style.flexGrow = 1f;
        grid.style.flexShrink = 1f;
        grid.style.minWidth = 0f;
        ShellRows.Join(line, b);
    }

    // A view switch, not a setting a player undoes: the Special difficulties.
    private static void View(VisualElement parent, string key, string tip, Func<bool> get, Action<bool> set) =>
        Controls.Toggle(parent, key, Bind.To(get, set), o => {
            o.Id = key;
            o.Undoable = false;
            if(tip != null) o.Tooltip = tip;
        });

    // The range's gradient under its span and thumbs: the texture is made only while the track is on a panel, so a detached
    // build makes none, and destroyed when it leaves.
    private static void Gradient(VisualElement track, Color[] stops) {
        var fill = new VisualElement { name = "tuf-gradient", pickingMode = PickingMode.Ignore };
        fill.style.position = Position.Absolute;
        fill.style.left = fill.style.right = fill.style.top = fill.style.bottom = 0f;
        track.Add(fill);
        Texture2D texture = null;
        Ui.WhileAttached(fill, () => {
            texture ??= TufDifficultyRangeBar.BuildGradientTexture(256, stops);
            fill.style.backgroundImage = texture;
        }, () => {
            fill.style.backgroundImage = StyleKeyword.Null;
            if(texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
        });
    }
}
#endif
