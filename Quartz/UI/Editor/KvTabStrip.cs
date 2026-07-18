using Quartz.Core;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
namespace Quartz.UI.Editor;
/// <summary>
/// DM Note's TabTool strip: `flex items-center h-[40px] p-[5px] bg-button-primary rounded-[7px]
/// gap-[5px]` holding `h-[30px] px-[8px] rounded-[7px]` labelled buttons — which is
/// <see cref="KvToolbar.Pill"/> exactly, so the tray is that and only the buttons are new.
///
/// Not <see cref="KvTabs"/>. That is DM Note's *other* tab strip, the PropertiesPanel one, and it
/// is a different control: a recessed #26262C track holding 24px pills. This one is a bar pill of
/// 30px buttons over black. Sharing a builder would mean one of them stopped looking like its
/// source.
///
/// Where DM Note lists four fixed key modes, this lists the document's tabs — an unbounded set up
/// to <see cref="Features.KeyViewer.Layout.KvDocument.MaxCustomTabs"/>. So the buttons ride a
/// ScrollRect and the tray is capped: the strip is the one thing on this bar that can be squeezed
/// (every other pill is icons at a fixed size), and it absorbs that by scrolling rather than by
/// crushing its labels or pushing the file actions off the end.
/// </summary>
internal sealed class KvTabStrip {
    /// <summary>30px button, the same box the bar's icon buttons use.</summary>
    private static float ButtonHeight => KvPalette.IconButton;
    /// <summary>8px px-, either side of the label.</summary>
    private static float LabelPadX => 8f * KvPalette.Scale;
    /// <summary>text-style-4.</summary>
    private static float LabelSize => 14f * KvPalette.Scale;
    /// <summary>
    /// Roughly three tabs. A ceiling rather than a measurement: the bar already carries five pills
    /// of fixed-size icons, and letting the strip grow to its content would push the file actions
    /// out of the region long before thirty tabs.
    /// </summary>
    private static float MaxWidth => 200f * KvPalette.Scale;
    /// <summary>One tab's worth. The floor the layout may shrink the strip to before it stops.</summary>
    private static float MinWidth => 64f * KvPalette.Scale;
    private readonly RectTransform track;
    private readonly LayoutElement viewportLe;
    private readonly ScrollRect scroll;
    private KvTabStrip(RectTransform track, LayoutElement viewportLe, ScrollRect scroll) {
        this.track = track;
        this.viewportLe = viewportLe;
        this.scroll = scroll;
    }
    internal static KvTabStrip Create(RectTransform bar) {
        RectTransform pill = KvToolbar.Pill(bar);
        GameObject viewObj = new("TabViewport");
        viewObj.transform.SetParent(pill, false);
        RectTransform viewport = viewObj.AddComponent<RectTransform>();
        LayoutElement viewportLe = viewObj.AddComponent<LayoutElement>();
        viewportLe.minHeight = ButtonHeight;
        viewportLe.preferredHeight = ButtonHeight;
        viewportLe.flexibleWidth = 0f;
        // Transparent: the pill behind it is already DM Note's black tray, and a second fill here
        // would darken it. Raycastable anyway, or the ScrollRect never receives a drag.
        viewObj.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewObj.AddComponent<RectMask2D>();
        GameObject trackObj = new("Tabs");
        trackObj.transform.SetParent(viewport, false);
        RectTransform track = trackObj.AddComponent<RectTransform>();
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(0f, 1f);
        track.pivot = new Vector2(0f, 0.5f);
        HorizontalLayoutGroup layout = trackObj.AddComponent<HorizontalLayoutGroup>();
        // gap-[5px]. No padding of its own: the pill already carries DM Note's p-[5px].
        layout.spacing = KvPalette.PillPad;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = trackObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        ScrollRect scroll = viewObj.AddComponent<ScrollRect>();
        scroll.content = track;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        // Drag-only, as KvTabs is. The page behind this polls Input rather than consuming a wheel
        // event, so a live sensitivity here would scroll the strip and the page at once.
        scroll.scrollSensitivity = 0f;
        scroll.inertia = false;
        viewObj.AddComponent<KvToolbar.StripWheel>().Init(viewport, track);
        return new KvTabStrip(track, viewportLe, scroll);
    }
    /// <summary>
    /// Repaint the strip for <paramref name="tabs"/>. Called when the tab set or the selection
    /// changes and at no other time — this tears down and rebuilds every button.
    /// </summary>
    internal void Rebuild(
        IReadOnlyList<string> tabs, string selected, Func<string, string> name, Action<string> onPick
    ) {
        if(track == null) return;
        GenerateUI.ClearChildren(track);
        List<(LayoutElement, TextMeshProUGUI, float)> measured = [];
        float width = 0f;
        for(int i = 0; i < tabs.Count; i++) {
            if(i > 0) width += KvPalette.PillPad;
            width += Button(tabs[i], name(tabs[i]), tabs[i] == selected, onPick, measured);
        }
        // Measured here rather than left to a fitter on the viewport: the pill is inside a
        // HorizontalLayoutGroup that drives its children's widths, and a fitter under one fights it.
        viewportLe.preferredWidth = Mathf.Min(width, MaxWidth);
        viewportLe.minWidth = Mathf.Min(width, MinWidth);
        // Re-measure until the font atlas is ready: the first-frame widths after a restart are wrong
        // and would otherwise ellipsize every tab ("16 Keys" → "1…") until the strip is rebuilt.
        KvTabRemeasure.Attach(track, measured, viewportLe, KvPalette.PillPad, MinWidth, MaxWidth);
    }
    /// <summary>One tab button, returning the width it asked for.</summary>
    private float Button(
        string tab, string text, bool selected, Action<string> onPick,
        List<(LayoutElement, TextMeshProUGUI, float)> measured
    ) {
        GameObject obj = new("Tab");
        obj.transform.SetParent(track, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = ButtonHeight;
        le.preferredHeight = ButtonHeight;
        le.flexibleWidth = 0f;
        TextMeshProUGUI label = GenerateUI.AddText(rect, true);
        label.fontSize = LabelSize;
        // The name is the document's, not a UI string: it is stored in customTabs[].name and is
        // whatever DM Note would show. Nothing to localize, so no TextLocalization here.
        label.text = text;
        label.color = KvPalette.TextDim;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        float width = label.GetPreferredValues(text).x + LabelPadX * 2f;
        le.preferredWidth = width;
        le.minWidth = width;
        measured.Add((le, label, LabelPadX * 2f));
        UIButton button = new("kv_tab", rect, label, bg, null) {
            RestColor = selected ? static () => KvPalette.ButtonActive : static () => KvPalette.ButtonPrimary,
            // The selected button has no hover class in DM Note; it stays put.
            HoverColor = selected ? static () => KvPalette.ButtonActive : static () => KvPalette.ButtonHover,
        };
        button.UpdateVisual(true);
        GenerateUI.AddButton(obj, btn => {
            if(btn != InputButton.Left) return;
            if(selected) return;
            onPick?.Invoke(tab);
            // Not hovered back out: the pick rebuilds this strip, so the button the pointer is over
            // is gone before a hover-exit could land on it.
        }, false);
        EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => button.OnHoverExit(), trigger);
        // Without this, the tab's EventTrigger eats the drag and the strip never scrolls — a drag
        // could only start from the gaps between tabs.
        KvToolbar.ForwardDrag(trigger, scroll);
        return width;
    }
}
