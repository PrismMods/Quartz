using Quartz.Core;
using Quartz.Localization;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
namespace Quartz.UI.Editor;
/// <summary>
/// DM Note's ListPopup: the black tray of choices an icon button opens, and the reason its toolbar
/// needs one icon where this editor had four buttons.
///
/// Editor-local and action-only. <see cref="Objects.Impl.UIDropDown{T}"/> is the menu's list
/// control, but it is a value selector — it holds a current value and a changed-dot against a
/// default — and "Add Key" is not a value. Modelling these as a selection would leave it reporting
/// whichever action was run last as the field's state.
///
/// Opens upward, because the bar it hangs off is at the bottom of the editor.
/// </summary>
internal sealed class KvPopup : MonoBehaviour {
    /// <summary>24px row.</summary>
    private static float ItemHeight => 24f * KvPalette.Scale;
    /// <summary>108px min-w item, inside a 5px tray pad.</summary>
    private static float MinWidth => (108f + 10f) * KvPalette.Scale;
    /// <summary>13px item label (text-style-2).</summary>
    private static float LabelSize => 13f * KvPalette.Scale;
    /// <summary>1px flex gap.</summary>
    private static float ItemGap => 1f * KvPalette.Scale;
    /// <summary>The tray floats clear of the button that opened it.</summary>
    private static float AnchorGap => 6f * KvPalette.Scale;
    private static KvPopup open;
    private Action onClosed;
    /// <summary>
    /// Show a list over <paramref name="host"/>, hanging off <paramref name="anchor"/>. Re-opening
    /// from the same button closes it, which is how DM Note's toggle behaves and what a user who
    /// missed the list expects from a second click.
    /// </summary>
    internal static void Show(
        RectTransform host, RectTransform anchor, IReadOnlyList<(string Key, string Text)> items,
        Action<int> onPick, Action onClosed = null
    ) {
        bool sameAnchor = open != null && open.transform.parent == host && ReferenceEquals(open.Anchor, anchor);
        CloseAny();
        if(sameAnchor) return;
        GameObject obj = new("KvPopup");
        obj.transform.SetParent(host, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        // The full-bleed overlay is positioned by Place, never by a parent group. A host with a
        // layout group (the editor region has a VerticalLayoutGroup) would otherwise count it as a
        // row and open one extra spacing gap, shunting the whole bar up until the popup closed.
        obj.AddComponent<LayoutElement>().ignoreLayout = true;
        // Above every sibling in the region, including the canvas and the panel.
        obj.transform.SetAsLastSibling();
        KvPopup popup = obj.AddComponent<KvPopup>();
        popup.Anchor = anchor;
        popup.onClosed = onClosed;
        // A click anywhere but the tray dismisses. Full-bleed and behind the tray, so the tray's
        // own rows get the pointer first.
        GameObject catcher = new("Catcher");
        catcher.transform.SetParent(rect, false);
        RectTransform catcherRect = catcher.AddComponent<RectTransform>();
        catcherRect.anchorMin = Vector2.zero;
        catcherRect.anchorMax = Vector2.one;
        catcherRect.offsetMin = Vector2.zero;
        catcherRect.offsetMax = Vector2.zero;
        catcher.AddComponent<EmptyGraphic>().raycastTarget = true;
        GenerateUI.AddButton(catcher, _ => CloseAny(), false);
        RectTransform tray = BuildTray(rect, items, onPick);
        Place(tray, anchor, rect);
        AnimateIn(tray);
        open = popup;
    }
    internal RectTransform Anchor { get; private set; }
    internal static void CloseAny() {
        if(open == null) return;
        KvPopup popup = open;
        open = null;
        Action closed = popup.onClosed;
        if(popup.gameObject != null) Destroy(popup.gameObject);
        closed?.Invoke();
    }
    private void OnDestroy() {
        if(open == this) open = null;
    }
    private static RectTransform BuildTray(
        RectTransform parent, IReadOnlyList<(string Key, string Text)> items, Action<int> onPick
    ) {
        GameObject obj = new("Tray");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        bg.color = KvPalette.ButtonPrimary;
        VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = ItemGap;
        int pad = Mathf.RoundToInt(KvPalette.PillPad);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fit = obj.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for(int i = 0; i < items.Count; i++) {
            int index = i;
            Item(rect, items[i].Key, items[i].Text, () => {
                CloseAny();
                onPick?.Invoke(index);
            });
        }
        return rect;
    }
    private static void Item(RectTransform tray, string key, string text, Action onClick) {
        GameObject obj = new("Item");
        obj.transform.SetParent(tray, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        bg.color = KvPalette.ButtonPrimary;
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = ItemHeight;
        le.preferredHeight = ItemHeight;
        le.minWidth = MinWidth;
        TextMeshProUGUI label = GenerateUI.AddText(rect, true);
        label.fontSize = LabelSize;
        label.text = text;
        label.color = KvPalette.TextDim;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.gameObject.AddComponent<TextLocalization>().Init(key, text);
        GenerateUI.AddButton(obj, btn => {
            if(btn == InputButton.Left) onClick();
        }, false);
        EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => bg.color = KvPalette.ButtonHover, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => bg.color = KvPalette.ButtonPrimary, trigger);
    }
    /// <summary>
    /// Sit the tray above the button, centred on it and kept inside the region. The layout has to
    /// be resolved before it can be measured, hence the forced rebuild — the fitter would otherwise
    /// not have run until the end of the frame and every tray would place off a zero-size rect.
    /// </summary>
    /// <summary>Fade the tray in and let it rise the last few px into place — a light pop, matching
    /// the toast's fade+slide. Placed after <see cref="Place"/> so the rest position is known.</summary>
    private static void AnimateIn(RectTransform tray) {
        CanvasGroup cg = tray.GetComponent<CanvasGroup>() ?? tray.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        Vector2 target = tray.anchoredPosition;
        tray.anchoredPosition = target - new Vector2(0f, 8f * KvPalette.Scale);
        MainCore.TC.Play(GTweenSequenceBuilder.New()
            .Join(cg.GTFade(1f, 0.12f).SetEasing(Easing.OutSine))
            .Join(tray.GTAnchorPos(target, 0.17f).SetEasing(Easing.OutExpo))
            .Build());
    }
    private static void Place(RectTransform tray, RectTransform anchor, RectTransform host) {
        LayoutRebuilder.ForceRebuildLayoutImmediate(tray);
        tray.anchorMin = Vector2.zero;
        tray.anchorMax = Vector2.zero;
        tray.pivot = new Vector2(0.5f, 0f);
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 bottomLeft = host.InverseTransformPoint(corners[0]);
        Vector3 topRight = host.InverseTransformPoint(corners[2]);
        float centreX = (bottomLeft.x + topRight.x) * 0.5f;
        float half = tray.rect.width * 0.5f;
        Rect area = host.rect;
        float x = Mathf.Clamp(centreX, area.xMin + half + KvPalette.BarPad, area.xMax - half - KvPalette.BarPad);
        float y = topRight.y + AnchorGap;
        tray.anchoredPosition = new Vector2(x - area.xMin, y - area.yMin);
    }
}
