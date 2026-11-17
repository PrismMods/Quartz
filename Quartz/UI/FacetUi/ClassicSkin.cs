#if !IL2CPP
using Facet;
using UnityEngine;
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
    private static void Shape(Theme t) {
        var s = t.Sizes;
        s.Radius = 10f; s.Line = 2f;
        s.Pad = 14f; s.Gap = 6f;
        s.ControlPad = 6f; s.FieldPad = 6f;
        s.RowHeight = 44f; s.ListRowHeight = 30f; s.NavRowHeight = 36f; s.TitleBarHeight = 44f; s.ItemHeight = 44f;
        s.Title = 22f; s.Body = 16f; s.Small = 13f; s.Caps = 13f; s.CapsSpacing = 0.5f;
        // The classic toggle: a ring when off, a filled dot when on. A round track with no knob drawn.
        s.SwitchWidth = 22f; s.SwitchHeight = 22f; s.SwitchRadius = 11f; s.KnobRadius = 9f;
        s.Track = 6f; s.ThumbWidth = 16f; s.ThumbHeight = 16f;
        s.SliderWidth = 180f; s.NumberWidth = 64f; s.DropdownWidth = 160f;
        s.Indicator = 3f; s.Icon = 18f;
        s.RowPadY = 6f;
        s.TabGap = s.Gap + s.Pad;
        s.CanvasHandle = 10f; s.CanvasEdgeLong = 18f; s.CanvasEdgeShort = 8f; s.CanvasHit = 18f; s.CanvasOutline = 3f;
        s.DialogMinWidth = 280f; s.DialogMaxWidth = 420f;
        s.CardWidth = 168f;
        var k = t.Looks;
        k.Button = new Look { Bg = Role.Row, Text = Role.Text, HoverBg = Role.RowHover, PressedBg = Role.Panel,
            SelectedBg = Role.Accent, SelectedText = Role.AccentText, FocusBorder = Role.Focus };
        k.DangerButton = new Look { Bg = Role.Row, Text = Role.Danger, HoverBg = Role.RowHover, PressedBg = Role.Panel,
            SelectedBg = Role.Danger, SelectedText = Role.AccentText, FocusBorder = Role.Focus };
        k.FlatButton = new Look { Text = Role.Text, FocusBorder = Role.Focus };
        k.AccentButton = new Look { Bg = Role.Accent, Text = Role.AccentText, FocusBorder = Role.Focus };
        // Classic rows are each a filled rounded bar; the card around them stays bare.
        k.Row = new Look { Bg = Role.Row, HoverBg = Role.RowHover };
        // The selected page: an accent outline, as the classic sidebar draws it.
        k.RailItem = new Look { Text = Role.Text, HoverBg = Role.Row, SelectedBg = Role.Row, SelectedText = Role.Text,
            SelectedBorder = Role.Accent };
        k.Field = new Look { Bg = Role.Row, Text = Role.Text, HoverBg = Role.RowHover, FocusBorder = Role.Focus };
        k.Ring = new Look { FocusBorder = Role.Focus };
        k.Chrome = new Look { Bg = Role.TitleBar };
        k.Card = new Look { Bg = Role.Panel, Border = Role.PanelBorder };
        k.Group = new Look();
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
