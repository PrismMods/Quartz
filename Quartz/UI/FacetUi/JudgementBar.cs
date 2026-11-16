#if !IL2CPP
using Facet;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.Judgement;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>The judgement bar: the timings early to late as equal segments butted into one bar, each filled with the game's own
/// colour for it, label above its switch; then the judgements that are not timings as an ordinary tile line.</summary>
internal static class JudgementBar {
    internal const string FillName = "judgement-fill";
    private static readonly Refl.Member Scheme = new(typeof(RDC), "hitMarginColoursBySettings");

    /// <summary>The live scheme's colour for a bit (ColourSchemeHitMargin.SelectByHitMargin's choice), else JudgementTiles.Colour.</summary>
    internal static Color SlotColour(int bit) =>
        Live(bit switch {
            0 => "colourTooEarly", 1 => "colourVeryEarly", 2 => "colourLittleEarly", 3 or 13 or 14 => "colourPerfect", 12 => "colourXPerfect",
            4 => "colourLittleLate", 5 => "colourVeryLate", 6 => "colourTooLate", 7 => "colourMultipress", 8 => "colourFail", _ => null,
        }) ?? JudgementTiles.Colour(bit) ?? Color.gray;

    /// <summary>The death colour (the live scheme's colourFail), for a segment a restriction blocks.</summary>
    internal static Color Death => Live("colourFail") ?? Judgement.SlotColors[0];

    private static Color? Live(string field) {
        if(field == null) return null;
        try {
            object scheme = Scheme.Get(null);
            return scheme != null && new Refl.Member(scheme.GetType(), field).Get(scheme) is Color c ? c : null;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }

    /// <summary>A titled card (All and None in its head, one undo step each) with bar as one butted line and extra as a tile line
    /// under it. Returns the bar's tiles. With blocked, a blocked segment is filled with Death, re-asked every 100 ms; locked tiles
    /// show labels and fills only and take no clicks (an untitled card has no All and None either).</summary>
    internal static MaskTiles Build(VisualElement page, string titleKey, Bind<int> mask, int defaultMask, IReadOnlyList<JudgementTiles.Row> bar,
        IReadOnlyList<JudgementTiles.Row> extra, Action save, Func<int, bool> blocked = null, Action<CardOptions> more = null, bool locked = false) {
        MaskTiles line = null, rest = null;
        VisualElement card = JudgementTiles.Card(page, titleKey, on => {
            using(History.Step(on ? "MENU_ALL" : "MENU_NONE", null, null, QuartzHost.Translate)) {
                line?.SetAll(on);
                rest?.SetAll(on);
            }
        }, more);
        line = JudgementTiles.Tiles(card, mask, defaultMask, bar, save, bar.Count, _ => null);
        Func<int, Color> fill = b => blocked?.Invoke(b) == true ? Death : SlotColour(b);
        VisualElement[] fills = Butt(line);
        void Recolour() {
            for(int i = 0; i < fills.Length; i++) {
                Color c = fill(bar[i].Bit);
                c.a = 0.45f;
                fills[i].style.backgroundColor = c;
            }
        }
        Recolour();
        if(blocked != null) Ui.Every(line.Root, 100, Recolour);   // a custom mask or a mode changes what is blocked
        if(extra.Count > 0) rest = JudgementTiles.Tiles(card, mask, defaultMask, extra, save, extra.Count, JudgementTiles.Colour);
        if(locked) {
            Labels(line);
            if(rest != null) Labels(rest);
        }
        return line;
    }

    // A preset's bar: labels and fills only. No switch, no changed dot, and a tile takes no click (not dimmed: the fills are the point).
    private static void Labels(MaskTiles tiles) {
        foreach(Control<bool> t in tiles.Tiles) {
            t.Root.pickingMode = PickingMode.Ignore;
            t.Root.focusable = false;
            if(t.Input != null) t.Input.style.display = DisplayStyle.None;
            if(t.Root.Q(name: "changed") is { } dot) dot.style.visibility = Visibility.Hidden;
        }
    }

    // Facet workaround: Facet's tile grid keeps a gap after every tile and lays a tile out as one row; the bar wants no gaps and two rows.
    // Returns each tile's fill box, for the caller to colour.
    private static VisualElement[] Butt(MaskTiles line) {
        VisualElement grid = line.Root;
        Sizes sz = Theme.For(grid).Sizes;
        int n = line.Tiles.Count;
        var fills = new VisualElement[n];
        grid.style.marginRight = 0f;
        for(int i = 0; i < n; i++) {
            VisualElement tile = line.Tiles[i].Root;
            tile.style.marginRight = 0f;
            tile.style.flexGrow = 1f;   // Facet sizes each tile for a gap after it; growing takes those gaps back
            tile.style.height = sz.RowHeight * 2f;
            tile.style.flexDirection = FlexDirection.Column;
            tile.style.justifyContent = Justify.Center;
            tile.style.alignItems = Align.Center;
            if(i > 0) tile.style.borderLeftWidth = 0f;
            if(i > 0) tile.style.borderTopLeftRadius = tile.style.borderBottomLeftRadius = 0f;
            if(i < n - 1) tile.style.borderTopRightRadius = tile.style.borderBottomRightRadius = 0f;
            VisualElement dot = tile.Q(name: "changed");
            foreach(VisualElement child in tile.Children().ToList()) {
                if(child is TextElement label) {
                    label.style.flexGrow = 0f;
                    label.style.marginLeft = label.style.marginRight = 0f;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    // The changed dot hangs left of the label, out of the flow: label and switch stay put when it shows.
                    if(dot == null) continue;
                    var head = new VisualElement { pickingMode = PickingMode.Ignore };
                    tile.Insert(tile.IndexOf(label), head);
                    head.Add(label);
                    head.Add(dot);
                    dot.style.position = Position.Absolute;
                    dot.style.left = -10f;
                    dot.style.top = Length.Percent(50);
                    dot.style.marginTop = -3f;
                    dot.style.marginRight = 0f;
                } else if(child.style.position.value == Position.Absolute)
                    child.style.display = DisplayStyle.None;   // the selection edge: the fill and the switch already say on
                else if(child != dot) {
                    child.style.marginLeft = child.style.marginRight = 0f;   // the switch, under its label
                    child.style.marginTop = sz.Gap / 2f;
                }
            }
            var colour = new VisualElement { name = FillName, pickingMode = PickingMode.Ignore };
            colour.style.position = Position.Absolute;
            colour.style.left = colour.style.right = colour.style.top = colour.style.bottom = 0f;
            Paint.Forget(colour);
            tile.Insert(0, fills[i] = colour);
        }
        return fills;
    }
}
#endif
