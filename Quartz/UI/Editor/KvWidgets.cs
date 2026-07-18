using Quartz.Core;
using Quartz.Resource;
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
/// Row widgets for the layout editor's inspector, which is a column beside the canvas rather
/// than a strip across the page and so has roughly a third of the width the shared
/// <see cref="GenerateUI"/> builders assume.
///
/// Editor-local rather than options on the shared builders: every other page depends on those,
/// and there is no way to visually verify a change to them from here. These mirror the shared
/// signatures minus <c>rightInset</c>, so the inspector reads the same either way.
///
/// The differences that make them fit are all in the label: it is a size smaller, it stops
/// short of whatever sits on the right of the row, and it shrinks rather than running under it.
/// Everything else — sprites, colours, the fill-bar slider, the change dot — is the shared
/// visual language, and the widgets hand back the same <c>UIObject</c> types, so
/// <c>SetDanger</c>, <c>SetBlocked</c>, <c>Format</c> and the rest still apply.
/// </summary>
internal static partial class KvWidgets {
    /// <summary>
    /// The narrowest the pane may be. Nothing in here breaks below it — every widget stretches —
    /// but the labels start ellipsizing and the colour picker's saturation square gets too small
    /// to aim at, so this is where it stops being worth using rather than where it stops working.
    /// </summary>
    internal const float MinPaneWidth = 260f;
    /// <summary>
    /// DM Note's 220px PropertiesPanel, in this menu's units. It also happens to be wide enough for
    /// the longest label the inspector has ("Background (Pressed)") to sit beside its value without
    /// ellipsizing, which is what the hand-picked 320 here was for.
    ///
    /// Unlike DM Note's, this pane is still resizable and its width is still persisted — see the
    /// divider in PageKeyViewer.EditorChrome. This is only where it starts.
    /// </summary>
    internal static float DefaultPaneWidth => KvPalette.PanelWidth;
    /// <summary>Matches <see cref="GenerateUI.Row"/>'s default, and the height
    /// <see cref="UIColorPicker.SetExpanded"/> hard-codes for a collapsed picker.</summary>
    internal const float RowHeight = 50f;
    private const float Pad = 12f;
    private const float LabelSize = 19f;
    /// <summary>
    /// How far a label may shrink to fit its row. A fifth off the top of a size this small is
    /// about where it stops reading as the same typography as the value beside it, and a name cut
    /// short is the better trade past that.
    /// </summary>
    private const float LabelSizeMin = 15f;
    /// <summary>
    /// Let <paramref name="tmp"/> shrink to fit its rect. Width is what runs out here — every
    /// caller sets NoWrap and gives the text a full row of height — so this is TMP fitting one
    /// line into the space left over beside the control, not reflowing anything.
    /// </summary>
    private static void Fit(TextMeshProUGUI tmp, float max, float min) {
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = min;
        tmp.fontSizeMax = max;
    }
    /// <summary>Room for the toggle circle, which sits 23px off the right edge at 26-30px wide.</summary>
    private const float SwitchReserve = 48f;
    /// <summary>
    /// Room for a slider's value. Not just the text: <see cref="GenerateUI.AddSliderValueEditor"/>
    /// puts a 110px click zone there that opens the expression editor, and a label running under
    /// it would read as part of the label rather than as a control.
    /// </summary>
    private const float ValueReserve = 104f;
    /// <summary>
    /// A row label. Shrinks a size or two before it gives up and ellipsizes.
    ///
    /// The pane is a third of the width the shared builders assume and resizes down to
    /// <see cref="MinPaneWidth"/>, so the longer names run out of room at the default width and
    /// most names do at the narrowest. A label a size or two down still says which row it is;
    /// "Short Note Thres…" does not, and no width the pane can be dragged to makes it say so.
    /// Floored rather than free, so rows stay legible and close to one another in size — past the
    /// floor this ellipsizes as before.
    ///
    /// Still never wrapped: a second line needs a taller row than the control beside it.
    /// </summary>
    private static TextMeshProUGUI Caption(
        RectTransform parent, string text, string id, float rightReserve, string labelKey = null
    ) {
        TextMeshProUGUI tmp = GenerateUI.AddText(parent, true);
        tmp.fontSize = LabelSize;
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        Fit(tmp, LabelSize, LabelSizeMin);
        // One TextLocalization per label: a second would fight the first for the same text.
        // labelKey is for rows whose id is per-instance but whose label is not — every colour
        // picker has its own alpha row, and all of them say the same word.
        if(labelKey != null) GenerateUI.Localize(tmp, labelKey, text);
        else GenerateUI.LocalizeById(tmp, id, text);
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(Pad, 0f);
        rect.offsetMax = new Vector2(-rightReserve, 0f);
        return tmp;
    }
    internal static UIToggle Toggle(
        Transform parent, bool defaultValue, bool value, Action<bool> onChanged, string text, string id
    ) {
        RectTransform rect = GenerateUI.BackGround(0f);
        rect.SetParent(parent, false);
        TextMeshProUGUI label = Caption(rect, text, id, SwitchReserve);
        Image changedImg = GenerateUI.AddSmallChangedCircle(rect).GetComponent<Image>();
        GameObject circle = new("ToggleCircle");
        circle.transform.SetParent(rect, false);
        RectTransform circleRect = circle.AddComponent<RectTransform>();
        circleRect.anchorMin = new Vector2(1f, 0.5f);
        circleRect.anchorMax = new Vector2(1f, 0.5f);
        circleRect.pivot = new Vector2(0.5f, 0.5f);
        circleRect.anchoredPosition = new Vector2(-23f, 0f);
        circleRect.sizeDelta = new Vector2(26f, 26f);
        Image circleImage = circle.AddComponent<Image>();
        UIToggle toggle = new(id, rect, label, circleImage, circleRect, changedImg, defaultValue, value, onChanged);
        GenerateUI.AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Left:
                    toggle.Toggle();
                    break;
                case InputButton.Middle:
                    if(MainCore.Conf.MiddleClickToDefault && toggle.Value != toggle.DefaultValue) toggle.Reset();
                    break;
            }
        });
        return toggle;
    }
    internal static UIButton Button(Transform parent, Action onClick, string text, string id) {
        RectTransform rect = GenerateUI.BackGround(0f);
        rect.SetParent(parent, false);
        TextMeshProUGUI label = GenerateUI.AddText(rect, true);
        label.fontSize = LabelSize;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Fit(label, LabelSize, LabelSizeMin);
        GenerateUI.LocalizeById(label, id, text);
        Image bg = rect.GetComponent<Image>();
        bg.color = UIColors.ObjectButton;
        UIButton button = new(id, rect, label, bg, onClick);
        GenerateUI.AddButton(rect.gameObject, btn => {
            if(btn == InputButton.Left) button.Click();
        }, false);
        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => button.OnHoverExit(), trigger);
        return button;
    }
    /// <summary>
    /// A section header. Smaller than <see cref="GenerateUI.AddTextH1"/>'s 32px, which reads as a
    /// page title rather than as a group label at this width.
    /// </summary>
    internal static TextMeshProUGUI Header(RectTransform root, string key, string text) {
        RectTransform row = GenerateUI.Row(root, 30f);
        TextMeshProUGUI tmp = GenerateUI.AddText(row, true);
        tmp.fontSize = 21f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        RectTransform rect = tmp.rectTransform;
        rect.offsetMin = new Vector2(Pad, 0f);
        rect.offsetMax = new Vector2(-Pad, 0f);
        return GenerateUI.Localize(tmp, key, text);
    }
    /// <summary>
    /// Sized for the worst segment strip in the pane — the five inspector tabs, which at the
    /// default width get about 61px each. Every other strip here is three or four short words and
    /// has room to spare.
    /// </summary>
    private const float SegmentLabelSize = 17f;
    /// <summary>Where a segment stops shrinking, proportionally the same drop as a row label's.</summary>
    private const float SegmentLabelSizeMin = 14f;
    /// <summary>
    /// A segmented control on its own row. <see cref="GenerateUI.SegmentedControl"/> only adds its
    /// own layout — with a 250px right pad — when the row has none, so the pane's own is installed
    /// first. Segments also drop to 40px here: the default 50 is a button's height, not a tab's.
    /// </summary>
    internal static Action<T> Segments<T>(
        RectTransform root, IReadOnlyList<T> values, Func<T, string> name, Func<T, string> key,
        T value, Action<T> onChanged
    ) {
        RectTransform row = GenerateUI.Row(root, 40f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        Action<T> setter = GenerateUI.SegmentedControl(row, values, name, key, value, onChanged);
        foreach(Transform child in row) {
            LayoutElement le = child.GetComponent<LayoutElement>();
            if(le == null) continue;
            le.minHeight = 40f;
            le.preferredHeight = 40f;
            le.minWidth = 0f;
            TextMeshProUGUI label = child.GetComponentInChildren<TextMeshProUGUI>();
            if(label == null) continue;
            label.fontSize = SegmentLabelSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            // A segment gets an equal share of the row whatever it says, so the longest label in a
            // strip decides the size for that strip alone. Safe to fit: the label is inside the
            // segment, not a child of the group sizing it, so nothing feeds back.
            Fit(label, SegmentLabelSize, SegmentLabelSizeMin);
        }
        return setter;
    }
}
