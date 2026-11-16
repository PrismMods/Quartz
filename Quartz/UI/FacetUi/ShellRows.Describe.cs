#if !IL2CPP
using Facet;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// A row's description as an info icon after its label instead of a line under it: hovering the icon (or focusing it from the
/// keyboard) shows a floating card with the whole text, wrapped, near the icon and inside the window, above everything else.
/// Nothing runs while no one points at an icon: the card is made on enter and dropped on leave.
/// </summary>
internal static partial class ShellRows {
    private const float InfoSize = 15f, InfoMaxWidth = 360f, InfoGap = 6f;
    private static VisualElement infoCard;
    private static VisualElement infoFor;

    /// <summary>Puts key's description behind an info icon after row's label (row: a control's Root, the labelled row). The
    /// icon is named key, so the row's old description Id still names something on the page. visible, when given, shows
    /// the icon only while it holds (read on the menu's refresh). Returns the icon.</summary>
    internal static VisualElement Describe(VisualElement row, string key, Func<bool> visible = null) {
        Label label = null;
        for(int i = 0; i < row.childCount && label == null; i++) label = row[i] as Label;
        VisualElement host = row;
        int at = label == null ? 0 : row.IndexOf(label) + 1;
        if(visible != null) {
            // Facet's When shows its box by the predicate on every refresh; moved beside the label it carries the icon.
            host = Controls.When(row, visible);
            row.Insert(at, host);
            at = 0;
        }
        VisualElement icon = InfoIcon(key);
        host.Insert(at, icon);
        // The label stops growing so the icon sits right after its text; the free space goes after the icon instead.
        if(label != null) label.style.flexGrow = 0f;
        (visible != null ? host : icon).style.marginRight = StyleKeyword.Auto;
        return icon;
    }

    private static VisualElement InfoIcon(string key) {
        var icon = new VisualElement { name = key, focusable = true, tooltip = "" };
        Sizes sz = Theme.For(icon).Sizes;
        icon.style.width = icon.style.height = InfoSize;
        icon.style.flexShrink = 0f;
        icon.style.alignSelf = Align.Center;
        icon.style.marginLeft = InfoGap;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        Ui.SetRadius(icon, InfoSize / 2f);
        Ui.SetBorderWidth(icon, sz.Line);
        Paint.Look(icon, new Look { Border = Role.TextMuted, Text = Role.TextMuted, HoverText = Role.Text, FocusBorder = Role.Focus });
        var i = new Label("i") { pickingMode = PickingMode.Ignore };   // its colour is the icon's (inherited), hover included
        i.style.fontSize = sz.Caps;
        i.style.unityFontStyleAndWeight = FontStyle.Bold;
        i.style.paddingLeft = i.style.paddingRight = i.style.paddingTop = i.style.paddingBottom = 0f;
        i.style.marginLeft = i.style.marginRight = i.style.marginTop = i.style.marginBottom = 0f;
        i.style.unityTextAlign = TextAnchor.MiddleCenter;
        icon.Add(i);
        icon.RegisterCallback<PointerEnterEvent>(_ => ShowInfo(icon, key));
        icon.RegisterCallback<PointerLeaveEvent>(_ => HideInfo(icon));
        icon.RegisterCallback<FocusInEvent>(_ => ShowInfo(icon, key));
        icon.RegisterCallback<FocusOutEvent>(_ => HideInfo(icon));
        icon.RegisterCallback<DetachFromPanelEvent>(_ => HideInfo(icon));
        return icon;
    }

    /// <summary>Shows key's card for icon: below it (above it in the window's lower half), from its left edge (to its right edge
    /// in the window's right half), scaled as the window is, at most InfoMaxWidth wide, over everything in the panel.</summary>
    internal static VisualElement ShowInfo(VisualElement icon, string key) {
        HideInfo(infoFor);
        if(icon.panel == null) return null;
        VisualElement top = icon.panel.visualTree, window = icon;
        while(window.parent != null && window.parent != top) window = window.parent;
        Theme theme = Theme.For(icon);
        VisualElement card;
        using(Theme.Use(theme)) card = Ui.Card();
        Theme.Set(card, theme);
        card.name = "row-info-card";
        card.pickingMode = PickingMode.Ignore;
        Sizes sz = theme.Sizes;
        card.style.position = Position.Absolute;
        card.style.maxWidth = InfoMaxWidth;
        card.style.paddingLeft = card.style.paddingRight = sz.Pad;
        card.style.paddingTop = card.style.paddingBottom = sz.Gap;
        card.style.unityFontDefinition = window.style.unityFontDefinition;   // the window's face (SUIT), as Facet's popups take it
        // Quartz translates the text itself: the card lives outside the window, where Facet's words would be another owner's.
        var text = new Label(QuartzHost.Translate(key)) { name = "row-info-text", pickingMode = PickingMode.Ignore };
        text.style.whiteSpace = WhiteSpace.Normal;
        text.style.fontSize = sz.Small;
        card.Add(text);
        Paint.Fg(text, Role.Text);

        Rect a = icon.worldBound, w = window.worldBound, panel = top.worldBound;
        float scale = icon.layout.height > 0f ? a.height / icon.layout.height : 1f;
        bool right = a.center.x > w.center.x, up = a.center.y > w.center.y;
        if(right) card.style.right = Mathf.Max(0f, panel.xMax - Mathf.Min(a.xMax, w.xMax));
        else card.style.left = Mathf.Max(w.xMin, a.xMin);
        if(up) card.style.bottom = Mathf.Max(0f, panel.yMax - (a.yMin - InfoGap));
        else card.style.top = a.yMax + InfoGap;
        card.style.maxWidth = Mathf.Min(InfoMaxWidth, (right ? Mathf.Min(a.xMax, w.xMax) - w.xMin : w.xMax - Mathf.Max(w.xMin, a.xMin)) / scale);
        card.style.transformOrigin = new TransformOrigin(Length.Percent(right ? 100f : 0f), Length.Percent(up ? 100f : 0f));
        card.style.scale = new Scale(new Vector3(scale, scale, 1f));
        top.Add(card);
        card.BringToFront();
        infoCard = card;
        infoFor = icon;
        return card;
    }

    /// <summary>Drops the card shown for icon (null: whichever is shown).</summary>
    internal static void HideInfo(VisualElement icon) {
        if(infoCard == null || (icon != null && icon != infoFor)) return;
        infoCard.RemoveFromHierarchy();
        infoCard = null;
        infoFor = null;
    }
}
#endif
