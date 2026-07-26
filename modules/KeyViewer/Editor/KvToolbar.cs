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
internal static class KvToolbar {
    internal static RectTransform Bar(RectTransform parent) {
        RectTransform bar = GenerateUI.Row(parent, KvPalette.BarHeight);
        Backdrop(bar, KvPalette.Primary, 0f);
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
    internal sealed class KvBarMarker : MonoBehaviour { }
    internal static RectTransform RegionOf(RectTransform toolbarContent) {
        for(Transform t = toolbarContent; t != null; t = t.parent) {
            if(t.TryGetComponent(out KvBarMarker _))
                return t.parent as RectTransform ?? toolbarContent;
        }
        return toolbarContent.parent as RectTransform ?? toolbarContent;
    }
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
    internal static void Spacer(RectTransform bar) {
        GameObject obj = new("Spacer");
        obj.transform.SetParent(bar, false);
        obj.AddComponent<RectTransform>();
        obj.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }
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
        GenerateUI.AddButton(obj, btn => {
            if(btn == InputButton.Left) button.Click();
        }, false);
        EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => button.OnHoverExit(), trigger);
        ForwardDrag(trigger, obj.GetComponentInParent<ScrollRect>());
        rect.AddToolTip(tipKey, tipText);
        return button;
    }
    internal static void ForwardDrag(EventTrigger trigger, ScrollRect scroll) {
        if(trigger == null || scroll == null) return;
        UnityUtils.AddEvent(EventTriggerType.InitializePotentialDrag, e => scroll.OnInitializePotentialDrag(e), trigger);
        UnityUtils.AddEvent(EventTriggerType.BeginDrag, e => scroll.OnBeginDrag(e), trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, e => scroll.OnDrag(e), trigger);
        UnityUtils.AddEvent(EventTriggerType.EndDrag, e => scroll.OnEndDrag(e), trigger);
    }
    internal sealed class StripWheel : MonoBehaviour {
        private RectTransform viewport, content;
        private bool captured;
        internal void Init(RectTransform viewport, RectTransform content) {
            this.viewport = viewport;
            this.content = content;
        }
        private void OnDestroy() => SetCaptured(false);
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
            float delta = wheel.y + wheel.x;
            if(Mathf.Abs(delta) <= 0.0001f) return;
            if(!RectTransformUtility.RectangleContainsScreenPoint(viewport, UnityEngine.Input.mousePosition, null)) return;
            Vector2 pos = content.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x + delta * MainCore.Conf.ScrollSpeed, -overflow, 0f);
            content.anchoredPosition = pos;
        }
    }
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
