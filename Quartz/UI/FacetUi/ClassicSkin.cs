#if !IL2CPP
using Facet;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Quartz's own design system on the new menu: the classic menu's palette (<see cref="UIColors"/>, read when worn, so it
/// follows the accent), its filled rounded rows, round dot switches and larger type. The layout is the new menu's.
/// </summary>
internal static class ClassicSkin {
    internal static Theme Wear(Theme t) {
        t[Role.Panel] = UIColors.MenuBG; t[Role.TitleBar] = UIColors.TopBar; t[Role.Rail] = UIColors.MenuBG;
        t[Role.Row] = UIColors.ObjectBG; t[Role.RowHover] = UIColors.MenuHighlight; t[Role.RowAlt] = UIColors.MenuHighlight;
        t[Role.Line] = UIColors.Border; t[Role.PanelBorder] = UIColors.Border; t[Role.Off] = UIColors.ObjectInactive;
        t[Role.Text] = Color.white; t[Role.TextMuted] = UIColors.TextSecondary; t[Role.Danger] = UIColors.SoftRed;
        Shape(t);
        return t;
    }
    /// <summary>Touches the classic skin needs on parts other assemblies build: a gap before a page header's switch,
    /// and a filled Hand/Foot strip in the key viewer editor so it reads as a tab rail.</summary>
    internal static void Polish(VisualElement page) {
        var sz = Theme.For(page).Sizes;
        page.Query("menu-header").ForEach(h => {
            if(h.childCount > 1) h[h.childCount - 1].style.marginLeft = sz.Pad;
        });
        VisualElement strip = page.Q("kle-strip");
        if(strip == null) return;
        Paint.Bg(strip, Role.Row);
        Ui.SetRadius(strip, sz.Radius);
        strip.style.paddingLeft = strip.style.paddingRight = sz.ControlPad;
    }
    private static void Shape(Theme t) {
        var s = t.Sizes;
        s.Radius = 5f; s.Line = 2f;
        s.Pad = 18f; s.Gap = 6f;
        s.ControlPad = 6f; s.FieldPad = 6f;
        s.RowHeight = 44f; s.ListRowHeight = 30f; s.NavRowHeight = 36f; s.TitleBarHeight = 44f; s.ItemHeight = 44f;
        s.Title = 22f; s.Body = 16f; s.Small = 13f; s.Caps = 13f; s.CapsSpacing = 0.5f;
        // The classic toggle: a ring when off, a filled dot when on. A round track with no knob drawn.
        s.SwitchWidth = 16f; s.SwitchHeight = 16f; s.SwitchRadius = 8f; s.KnobRadius = 6f;
        s.Track = 6f; s.ThumbWidth = 16f; s.ThumbHeight = 16f;
        s.SliderWidth = 180f; s.NumberWidth = 64f; s.DropdownWidth = 160f;
        s.Indicator = 3f; s.Icon = 18f;
        s.RowPadY = 6f;
        s.TabGap = s.Gap + s.Pad;
        s.CanvasHandle = 10f; s.CanvasEdgeLong = 18f; s.CanvasEdgeShort = 8f; s.CanvasHit = 18f; s.CanvasOutline = 3f;
        s.DialogMinWidth = 280f; s.DialogMaxWidth = 420f;
        s.CardWidth = 168f;
        var k = t.Looks;
        k.Button = new Look { Bg = Role.RowHover, Text = Role.Text, Border = Role.Line, HoverBg = Role.Off, PressedBg = Role.Panel,
            SelectedBg = Role.Accent, SelectedText = Role.AccentText, FocusBorder = Role.Focus };
        k.DangerButton = new Look { Bg = Role.RowHover, Text = Role.Danger, Border = Role.Line, HoverBg = Role.Off, PressedBg = Role.Panel,
            SelectedBg = Role.Danger, SelectedText = Role.AccentText, FocusBorder = Role.Focus };
        k.FlatButton = new Look { Text = Role.Text, FocusBorder = Role.Focus };
        k.AccentButton = new Look { Bg = Role.Accent, Text = Role.AccentText, FocusBorder = Role.Focus };
        // The card carries the row fill, so its corners round; a row adds only its hover.
        k.Row = new Look { HoverBg = Role.RowHover };
        // The selected page: an accent outline, as the classic sidebar draws it.
        k.RailItem = new Look { Text = Role.Text, HoverBg = Role.Row, SelectedBg = Role.Row, SelectedText = Role.Text,
            SelectedBorder = Role.Accent };
        k.Field = new Look { Bg = Role.Row, Text = Role.Text, HoverBg = Role.RowHover, FocusBorder = Role.Focus };
        k.Ring = new Look { FocusBorder = Role.Focus };
        k.Chrome = new Look { Bg = Role.TitleBar };
        k.Card = new Look { Bg = Role.Panel, Border = Role.PanelBorder };
        k.Group = new Look { Bg = Role.Row, Border = Role.Line };
        k.Stage = new Look { Bg = Role.Rail, Border = Role.Line };
        k.Switch = new Look { Border = Role.Off, SelectedBg = Role.Accent, SelectedBorder = Role.Accent, FocusBorder = Role.Focus };
        k.Knob = new Look();
        k.Track = new Look { Bg = Role.Off };
        k.Fill = new Look { Bg = Role.Accent };
        k.Thumb = new Look { Bg = Role.Accent };
        k.Tab = new Look { Text = Role.TextMuted, HoverText = Role.Text, SelectedText = Role.Text, FocusBorder = Role.Focus };
        k.Indicator = new Look { SelectedBg = Role.Accent };
        k.Tile = new Look { Bg = Role.Row, Text = Role.TextMuted, HoverBg = Role.RowHover, SelectedText = Role.Text,
            SelectedBorder = Role.Accent, FocusBorder = Role.Focus };
        k.Link = new Look { Text = Role.Accent, HoverText = Role.Text, FocusBorder = Role.Focus };
        k.Rule = new Look { Bg = Role.Line };
        k.Caps = new Look { Text = Role.TextMuted };
    }
}
#endif
