#if !IL2CPP
using Facet;
using Quartz.Features.Judgement;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>Judgement choices as coloured tiles over the bits of a mask (spec "Decided" item 5): each tile wears its
/// judgement's colour from the judgement overlay.</summary>
internal static class JudgementTiles {
    /// <summary>A tile: its Lang key (its Id), the classic toggle's id (whose hotkey it shares) and its bit. A plain struct:
    /// a record's init accessors need IsExternalInit, which netstandard2.1 lacks.</summary>
    internal readonly struct Row(string key, string classicId, int bit) {
        internal string Key { get; } = key;
        internal string ClassicId { get; } = classicId;
        internal int Bit { get; } = bit;
    }

    /// <summary>The bit's slot colour, X Perfect's for 12 and Early Perfect's for + and − Perfect; none for Multipress and
    /// Overload (Fail), which have no slot.</summary>
    internal static Color? Colour(int bit) => bit switch {
        7 or 11 => null,
        12 => JudgementOverlay.XColor(),
        13 or 14 => Judgement.SlotColors[3],
        9 => Judgement.SlotColors[0],
        10 => Judgement.SlotColors[4],
        >= 0 and <= 8 => Judgement.SlotColors[bit < 7 ? bit + 1 : bit],
        _ => null,
    };

    /// <summary>A titled card of tiles over mask, four to a line, with All and None in its head. A write saves; defaultMask, from a fresh settings object, gives each tile the default a middle-click restores.</summary>
    internal static MaskTiles Grid(VisualElement page, string titleKey, Bind<int> mask, int defaultMask, IReadOnlyList<Row> rows,
        Action save, Action<CardOptions> more = null) {
        MaskTiles grid = null;
        VisualElement card = Card(page, titleKey, on => grid?.SetAll(on), more);
        return grid = Tiles(card, mask, defaultMask, rows, save, 4, Colour);
    }

    /// <summary>A titled card with All and None in its head (a card's actions are read once, at build, and only a titled card has
    /// them).</summary>
    internal static VisualElement Card(VisualElement page, string titleKey, Action<bool> setAll, Action<CardOptions> more) =>
        Controls.Card(page, titleKey, o => {
            o.Id = titleKey;
            if(titleKey != null) o.Actions = [new("MENU_ALL", () => setAll(true)), new("MENU_NONE", () => setAll(false))];   // an untitled card has no head for them
            more?.Invoke(o);
        });

    /// <summary>One line of tiles over rows' bits, columns to a line, each tile tinted colour(bit).</summary>
    internal static MaskTiles Tiles(VisualElement card, Bind<int> mask, int defaultMask, IReadOnlyList<Row> rows, Action save, int columns,
        Func<int, Color?> colour) {
        MaskTiles grid = null;
        var tiles = new List<MaskTile>(rows.Count);
        for(int i = 0; i < rows.Count; i++) {
            Row row = rows[i];
            int at = i;
            tiles.Add(new MaskTile(row.Key, row.Bit, o => {
                o.Id = row.Key;
                o.Colour = colour(row.Bit);
                o.Default = (defaultMask >> row.Bit & 1) != 0;
                ShellRows.Hotkeys(o, row.ClassicId, () => grid!.Tiles[at].Root);
            }));
        }
        Bind<int> written = QuartzShell.Writes(mask.Get, v => {
            mask.Set(v);
            save?.Invoke();
        });
        foreach(Row row in rows) {
            int bit = 1 << row.Bit;
            ShellRows.Flips(row.ClassicId, Bind.To(() => (written.Get() & bit) != 0, on => written.Set(on ? written.Get() | bit : written.Get() & ~bit)));
        }
        grid = Controls.MaskTiles(card, written, tiles, o => o.Columns = columns);
        return grid;
    }
}
#endif
