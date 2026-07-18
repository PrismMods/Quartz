using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
namespace Quartz.UI.Editor;
/// <summary>
/// DM Note's toolbar idiom: a bar of black grouping pills, each holding square icon buttons.
///
/// The shape is not decoration. DM Note fits seventeen actions in one 60px bar by grouping them
/// into pills and folding the multi-choice ones behind list popups, which is what lets its canvas
/// have the rest of the window. A row of full-width labelled buttons per action cannot be made to
/// fit, however it is styled.
///
/// Built on <see cref="UIButton"/> rather than a bespoke type so <c>SetBlocked</c>, the hover tween
/// and the tooltip helper all keep working; only the colours and the geometry are DM Note's. The
/// label a labelled button would carry is left null — nothing here reads it.
/// </summary>
internal static class KvToolbar {
    /// <summary>
    /// The bar itself: a fixed-height strip of pills over DM Note's page colour, with the hairline
    /// it draws along its top edge (border-t-[#2A2A30]).
    ///
    /// Children are laid out left to right. <see cref="Spacer"/> is what splits them into DM Note's
    /// two clusters, so the caller decides what sits against which edge.
    /// </summary>
    internal static RectTransform Bar(RectTransform parent) {
        RectTransform bar = GenerateUI.Row(parent, KvPalette.BarHeight);
        Backdrop(bar, KvPalette.Primary, 0f);
        // The top hairline is a child rather than an outline: it spans the bar's full width, which
        // an inset border on a rounded backdrop would not.
        GameObject edge = new("TopEdge");
        edge.transform.SetParent(bar, false);
        RectTransform edgeRect = edge.AddComponent<RectTransform>();
        edge.AddComponent<LayoutElement>().ignoreLayout = true;
        edgeRect.anchorMin = new Vector2(0f, 1f);
        edgeRect.anchorMax = new Vector2(1f, 1f);
        edgeRect.pivot = new Vector2(0.5f, 1f);
        edgeRect.sizeDelta = new Vector2(0f, 1f);
        edgeRect.anchoredPosition = Vector2.zero;
        Image edgeImg = edge.AddComponent<Image>();
        edgeImg.color = KvPalette.ButtonActive;
        edgeImg.raycastTarget = false;
        // The pills ride a masked, drag/wheel-scrollable content strip: the bar's clusters are
        // fixed-size icons, so a panel narrower than their sum used to overflow the region.
        // The marker is what RegionOf climbs past.
        bar.gameObject.AddComponent<KvBarMarker>();
        GameObject viewObj = new("BarViewport");
        viewObj.transform.SetParent(bar, false);
        RectTransform viewport = viewObj.AddComponent<RectTransform>();
        viewObj.AddComponent<LayoutElement>().ignoreLayout = true;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewObj.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewObj.AddComponent<RectMask2D>();
        GameObject contentObj = new("BarContent");
        contentObj.transform.SetParent(viewport, false);
        RectTransform content = contentObj.AddComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        HorizontalLayoutGroup layout = contentObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = KvPalette.GroupGap;
        int pad = Mathf.RoundToInt(KvPalette.BarPad);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        // Pills are as wide as the icons they hold, not an even share of the bar: the whole point
        // of the idiom is that a five-icon group is compact.
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ScrollRect scroll = viewObj.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 0f;
        scroll.inertia = false;
        viewObj.AddComponent<StripWheel>().Init(viewport, content);
        KvBarFill fill = contentObj.AddComponent<KvBarFill>();
        fill.Viewport = viewport;
        fill.Content = content;
        return content;
    }
    /// <summary>Marks the bar row so <see cref="RegionOf"/> can find the editor region from a
    /// rect inside the bar's scroll hierarchy.</summary>
    internal sealed class KvBarMarker : MonoBehaviour { }
    /// <summary>
    /// The editor region a toolbar lives in — where popups mount so a tray can hang above the bar
    /// instead of being clipped by its scroll mask. Climbs out of the bar's viewport/content
    /// nesting to the marked bar row, then takes its parent.
    /// </summary>
    internal static RectTransform RegionOf(RectTransform toolbarContent) {
        for(Transform t = toolbarContent; t != null; t = t.parent) {
            if(t.TryGetComponent(out KvBarMarker _))
                return t.parent as RectTransform ?? toolbarContent;
        }
        return toolbarContent.parent as RectTransform ?? toolbarContent;
    }
    /// <summary>
    /// Sizes the bar's scroll content to max(preferred, viewport): at the preferred floor the
    /// bar scrolls instead of overflowing the panel; any surplus goes to the layout's flexible
    /// spacer so the file pill keeps hugging the right edge. Also re-clamps the scroll offset
    /// when the panel widens back out.
    /// </summary>
    internal sealed class KvBarFill : MonoBehaviour {
        internal RectTransform Viewport, Content;
        private void LateUpdate() {
            if(Viewport == null || Content == null) return;
            float preferred = LayoutUtility.GetPreferredWidth(Content);
            float width = Mathf.Max(preferred, Viewport.rect.width);
            if(Mathf.Abs(Content.sizeDelta.x - width) > 0.5f)
                Content.sizeDelta = new Vector2(width, Content.sizeDelta.y);
            float overflow = Mathf.Max(0f, width - Viewport.rect.width);
            Vector2 pos = Content.anchoredPosition;
            float clamped = Mathf.Clamp(pos.x, -overflow, 0f);
            if(Mathf.Abs(clamped - pos.x) > 0.01f)
                Content.anchoredPosition = new Vector2(clamped, pos.y);
        }
    }
    /// <summary>The gap that pushes everything after it to the far edge — DM Note's justify-between.</summary>
    internal static void Spacer(RectTransform bar) {
        GameObject obj = new("Spacer");
        obj.transform.SetParent(bar, false);
        obj.AddComponent<RectTransform>();
        obj.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }
    /// <summary>
    /// A grouping pill. DM Note's `flex items-center h-[40px] p-[5px] bg-button-primary
    /// rounded-[7px] gap-[5px]` — the black tray its icon buttons sit in.
    /// </summary>
    internal static RectTransform Pill(RectTransform bar) {
        GameObject obj = new("Pill");
        obj.transform.SetParent(bar, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Backdrop(rect, KvPalette.ButtonPrimary, KvPalette.Radius);
        HorizontalLayoutGroup layout = obj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = KvPalette.PillPad;
        int pad = Mathf.RoundToInt(KvPalette.PillPad);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fit = obj.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = KvPalette.PillHeight;
        le.preferredHeight = KvPalette.PillHeight;
        return rect;
    }
    /// <summary>
    /// One 30x30 icon button. <paramref name="tipKey"/> is not optional in practice: an icon with
    /// no label is unreadable without one, and every icon button DM Note draws carries a tooltip.
    ///
    /// <paramref name="glyphScale"/> is for the few glyphs DM Note only ever draws small. Its
    /// chevron is a 6x4 dropdown affordance, so at its true size it reads as a speck on a button
    /// this size; the sprites are baked against a shared reference box precisely so the exception
    /// has to be stated here rather than hidden in the asset.
    /// </summary>
    internal static UIButton Icon(
        RectTransform pill, UISprite sprite, string id, Action onClick, string tipKey, string tipText,
        float glyphScale = 1f
    ) {
        GameObject obj = new("IconButton");
        obj.transform.SetParent(pill, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        bg.color = KvPalette.ButtonPrimary;
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minWidth = KvPalette.IconButton;
        le.preferredWidth = KvPalette.IconButton;
        le.minHeight = KvPalette.IconButton;
        le.preferredHeight = KvPalette.IconButton;
        GameObject glyphObj = new("Glyph");
        glyphObj.transform.SetParent(rect, false);
        RectTransform glyph = glyphObj.AddComponent<RectTransform>();
        glyph.anchorMin = new Vector2(0.5f, 0.5f);
        glyph.anchorMax = new Vector2(0.5f, 0.5f);
        glyph.pivot = new Vector2(0.5f, 0.5f);
        // Every sprite in the set shares one reference box, so one size here renders each at the
        // size DM Note draws it. See KvPalette.IconSize.
        float size = KvPalette.IconSize * glyphScale;
        glyph.sizeDelta = new Vector2(size, size);
        glyph.anchoredPosition = Vector2.zero;
        Image glyphImg = glyphObj.AddComponent<Image>();
        glyphImg.sprite = MainCore.Spr.Get(sprite);
        glyphImg.color = KvPalette.TextDim;
        glyphImg.raycastTarget = false;
        UIButton button = new(id, rect, null, bg, onClick) {
            RestColor = static () => KvPalette.ButtonPrimary,
            HoverColor = static () => KvPalette.ButtonHover,
        };
        button.UpdateVisual(true);
        // outline: false — the shared hover outline is drawn for the menu's own button language and
        // reads as a stray ring on a 30px square.
        GenerateUI.AddButton(obj, btn => {
            if(btn == InputButton.Left) button.Click();
        }, false);
        EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => button.OnHoverExit(), trigger);
        // Icons cover most of the bar; without this their EventTriggers eat the drag that should
        // scroll a bar narrower than its pills. Finds the bar's own ScrollRect (or a strip's inner
        // one, which is the nearer parent and the right target there).
        ForwardDrag(trigger, obj.GetComponentInParent<ScrollRect>());
        rect.AddToolTip(tipKey, tipText);
        return button;
    }
    /// <summary>
    /// Re-route drag events from a button to the strip's ScrollRect. An <see cref="EventTrigger"/>
    /// implements every drag interface just by existing, so a pill built with
    /// <see cref="GenerateUI.AddButton"/> eats the drag that should scroll the strip it sits in —
    /// the strip only ever saw drags that started in the gaps between pills.
    /// </summary>
    internal static void ForwardDrag(EventTrigger trigger, ScrollRect scroll) {
        if(trigger == null || scroll == null) return;
        UnityUtils.AddEvent(EventTriggerType.InitializePotentialDrag, e => scroll.OnInitializePotentialDrag(e), trigger);
        UnityUtils.AddEvent(EventTriggerType.BeginDrag, e => scroll.OnBeginDrag(e), trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, e => scroll.OnDrag(e), trigger);
        UnityUtils.AddEvent(EventTriggerType.EndDrag, e => scroll.OnEndDrag(e), trigger);
    }
    /// <summary>
    /// Wheel (and trackpad) scrolling for a horizontal tab strip. The page and pane scrolls poll
    /// Input rather than implementing IScrollHandler, so there is no event for the strip to
    /// consume — instead the viewport registers as an input-capture region (the same mechanism the
    /// canvas uses), which keeps the scroll behind it still, and this polls the wheel itself.
    /// </summary>
    internal sealed class StripWheel : MonoBehaviour {
        private RectTransform viewport, content;
        private bool captured;
        internal void Init(RectTransform viewport, RectTransform content) {
            this.viewport = viewport;
            this.content = content;
        }
        private void OnDestroy() => SetCaptured(false);
        // Registered only while there is something to scroll: the capture region silences the
        // page/pane scroll behind it, and a strip whose content fits shouldn't cost the page
        // its wheel.
        private void SetCaptured(bool value) {
            if(value == captured) return;
            captured = value;
            if(value) UIScrollController.AddInputCapture(viewport);
            else if(viewport != null) UIScrollController.RemoveInputCapture(viewport);
        }
        private void Update() {
            if(viewport == null || content == null) return;
            float overflow = content.rect.width - viewport.rect.width;
            SetCaptured(overflow > 0.5f);
            if(overflow <= 0f) return;
            Vector2 wheel = UnityEngine.Input.mouseScrollDelta;
            // Both axes: a mouse wheel only has y, a trackpad's sideways swipe arrives as x.
            float delta = wheel.y + wheel.x;
            if(Mathf.Abs(delta) <= 0.0001f) return;
            if(!RectTransformUtility.RectangleContainsScreenPoint(viewport, UnityEngine.Input.mousePosition, null)) return;
            Vector2 pos = content.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x + delta * MainCore.Conf.ScrollSpeed, -overflow, 0f);
            content.anchoredPosition = pos;
        }
    }
    /// <summary>
    /// A backdrop as an ignored, anchor-filling child. On the rect itself it would be a second
    /// <c>ILayoutElement</c> competing with the group's own sizing — the same reason the editor's
    /// card is built this way.
    /// </summary>
    private static void Backdrop(RectTransform parent, Color color, float radius) {
        GameObject obj = new("Bg");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        obj.AddComponent<LayoutElement>().ignoreLayout = true;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image img = obj.AddComponent<Image>();
        if(radius > 0f) {
            img.sprite = MainCore.Spr.GetFilled(radius);
            img.type = Image.Type.Sliced;
        }
        img.color = color;
        img.raycastTarget = false;
    }
}
