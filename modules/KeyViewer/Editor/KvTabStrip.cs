using Quartz.Core;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Quartz.Compat.Game;
namespace Quartz.UI.Editor;
internal sealed class KvTabStrip {
    private static float ButtonHeight => KvPalette.IconButton;
    private static float LabelPadX => 8f * KvPalette.Scale;
    private static float LabelSize => 14f * KvPalette.Scale;
    private static float MaxWidth => 200f * KvPalette.Scale;
    private static float MinWidth => 64f * KvPalette.Scale;
    private readonly RectTransform track;
    private readonly LayoutElement viewportLe;
    private readonly ScrollRect scroll;
    private readonly List<string> order = [];
    private readonly KvTabDragState drag = new();
    private Action<string, int> reorder;
    internal RectTransform Pill { get; private set; }
    private KvTabStrip(RectTransform track, LayoutElement viewportLe, ScrollRect scroll) {
        this.track = track;
        this.viewportLe = viewportLe;
        this.scroll = scroll;
    }
    internal static KvTabStrip Create(RectTransform bar, string captionKey = null, string captionText = null) {
        RectTransform pill = KvToolbar.Pill(bar);
        if(captionText != null) Caption(pill, captionKey, captionText);
        GameObject viewObj = new("TabViewport");
        viewObj.transform.SetParent(pill, false);
        RectTransform viewport = viewObj.AddComponent<RectTransform>();
        LayoutElement viewportLe = viewObj.AddComponent<LayoutElement>();
        viewportLe.minHeight = ButtonHeight;
        viewportLe.preferredHeight = ButtonHeight;
        viewportLe.flexibleWidth = 0f;
        viewObj.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewObj.AddComponent<RectMask2D>();
        GameObject trackObj = new("Tabs");
        trackObj.transform.SetParent(viewport, false);
        RectTransform track = trackObj.AddComponent<RectTransform>();
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(0f, 1f);
        track.pivot = new Vector2(0f, 0.5f);
        HorizontalLayoutGroup layout = trackObj.AddComponent<HorizontalLayoutGroup>();
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
        scroll.scrollSensitivity = 0f;
        scroll.inertia = false;
        viewObj.AddComponent<KvToolbar.StripWheel>().Init(viewport, track);
        return new KvTabStrip(track, viewportLe, scroll) { Pill = pill };
    }
    private static void Caption(RectTransform pill, string key, string text) {
        GameObject obj = new("Caption");
        obj.transform.SetParent(pill, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        TextMeshProUGUI label = GenerateUI.AddText(rect, true);
        label.fontSize = LabelSize;
        label.text = key == null ? text : MainCore.Tr.Get(key, text);
        label.color = KvPalette.TabIdleText;
        label.alignment = TextAlignmentOptions.Center;
        TextCompat.NoWrap(label);
        label.raycastTarget = false;
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = ButtonHeight;
        le.preferredHeight = ButtonHeight;
        le.flexibleWidth = 0f;
        float width = label.GetPreferredValues(label.text).x + LabelPadX * 2f;
        le.preferredWidth = width;
        le.minWidth = width;
        KvTabRemeasure.Attach(rect, [(le, label, LabelPadX * 2f)]);
    }
    internal void Rebuild(
        IReadOnlyList<string> tabs, Func<string, bool> active, string editing,
        Func<string, string> name, Action<string> onPick, Action<string, int> onReorder
    ) {
        if(track == null) return;
        reorder = onReorder;
        drag.Reset();
        order.Clear();
        order.AddRange(tabs);
        GenerateUI.ClearChildren(track);
        List<(LayoutElement, TextMeshProUGUI, float)> measured = [];
        float width = 0f;
        for(int i = 0; i < tabs.Count; i++) {
            if(i > 0) width += KvPalette.PillPad;
            string tab = tabs[i];
            width += Button(tab, name(tab), active?.Invoke(tab) ?? false, tab == editing, onPick, measured);
        }
        viewportLe.preferredWidth = Mathf.Min(width, MaxWidth);
        viewportLe.minWidth = Mathf.Min(width, MinWidth);
        KvTabRemeasure.Attach(track, measured, viewportLe, KvPalette.PillPad, MinWidth, MaxWidth);
    }
    private float Button(
        string tab, string text, bool active, bool editing, Action<string> onPick,
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
        label.text = text;
        label.color = editing ? KvPalette.TextWhite : active ? KvPalette.TextDim : KvPalette.TabIdleText;
        label.alignment = TextAlignmentOptions.Center;
        TextCompat.NoWrap(label);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        float width = label.GetPreferredValues(text).x + LabelPadX * 2f;
        le.preferredWidth = width;
        le.minWidth = width;
        measured.Add((le, label, LabelPadX * 2f));
        UIButton button = new("kv_tab", rect, label, bg, null) {
            RestColor = active ? static () => KvPalette.ButtonActive : static () => KvPalette.ButtonPrimary,
            HoverColor = active ? static () => KvPalette.ButtonActive : static () => KvPalette.ButtonHover,
        };
        button.UpdateVisual(true);
        GenerateUI.AddButton(obj, btn => {
            if(!drag.TryPick(btn == InputButton.Left)) return;
            if(editing) return;
            onPick?.Invoke(tab);
        }, false);
        EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerDown, e => drag.PointerDown(e.button == InputButton.Left), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => button.OnHoverExit(), trigger);
        UnityUtils.AddEvent(EventTriggerType.InitializePotentialDrag, scroll.OnInitializePotentialDrag, trigger);
        UnityUtils.AddEvent(EventTriggerType.BeginDrag, e => BeginDrag(tab, e), trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, Drag, trigger);
        UnityUtils.AddEvent(EventTriggerType.EndDrag, EndDrag, trigger);
        return width;
    }
    private void BeginDrag(string tab, PointerEventData e) {
        if(!drag.Begin(tab, order.IndexOf(tab), order.Count, e.button == InputButton.Left)) {
            scroll.OnBeginDrag(e);
            return;
        }
    }
    private void Drag(PointerEventData e) {
        if(!drag.Reordering) {
            scroll.OnDrag(e);
            return;
        }
        string tab = drag.Tab;
        int from = order.IndexOf(tab);
        int to = SlotAt(e);
        if(from < 0 || to < 0 || to == from) return;
        order.RemoveAt(from);
        order.Insert(to, tab);
        track.GetChild(from).SetSiblingIndex(to);
    }
    private int SlotAt(PointerEventData e) {
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, null, out Vector2 local))
            return -1;
        float cursor = 0f;
        for(int i = 0; i < track.childCount; i++) {
            float w = ((RectTransform)track.GetChild(i)).rect.width;
            if(local.x < cursor + w * 0.5f) return i;
            cursor += w + KvPalette.PillPad;
        }
        return track.childCount - 1;
    }
    private void EndDrag(PointerEventData e) {
        if(!drag.End(out string tab, out int from)) {
            scroll.OnEndDrag(e);
            return;
        }
        int to = order.IndexOf(tab);
        if(to >= 0 && to != from) reorder?.Invoke(tab, to);
    }
}
