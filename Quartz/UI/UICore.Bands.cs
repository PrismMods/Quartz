using Quartz.Async;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Factory;
using Quartz.UI.Factory.Page;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Panes;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using GTweens.Tweens;
using GTweens.Builders;
using GTweens.Extensions;
using Quartz.Tween;
using GTweens.Easings;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI;
public static partial class UICore {
    private static void CreateSubMenu(Transform parent) {
        GameObject subMenu = new("SubMenu");
        subMenu.transform.SetParent(parent, false);
        SubMenu = subMenu.AddComponent<RectTransform>();
        SubMenu.anchorMin = new(0, 0);
        SubMenu.anchorMax = new(0, 1);
        SubMenu.pivot = new(0, 0.5f);
        SubMenu.sizeDelta = new(0f, -TOP_BAR_HEIGHT);
        SubMenu.anchoredPosition = new(MENU_WIDTH, -TOP_BAR_HEIGHT * 0.5f);
        var image = subMenu.AddComponent<Image>();
        image.color = UIColors.TopBar;
        GameObject hairline = new("Hairline");
        hairline.transform.SetParent(subMenu.transform, false);
        RectTransform hairlineRect = hairline.AddComponent<RectTransform>();
        hairlineRect.anchorMin = new(0, 0);
        hairlineRect.anchorMax = new(0, 1);
        hairlineRect.pivot = new(0, 0.5f);
        hairlineRect.sizeDelta = new(1f, 0f);
        Image hairlineImg = hairline.AddComponent<Image>();
        hairlineImg.color = new Color(1f, 1f, 1f, 0.08f);
        hairlineImg.raycastTarget = false;
        void OutlineEdge(string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size) {
            GameObject edge = new(name);
            edge.transform.SetParent(subMenu.transform, false);
            RectTransform er = edge.AddComponent<RectTransform>();
            er.anchorMin = aMin;
            er.anchorMax = aMax;
            er.pivot = pivot;
            er.sizeDelta = size;
            er.anchoredPosition = Vector2.zero;
            Image ei = edge.AddComponent<Image>();
            ei.color = Color.white;
            ei.raycastTarget = false;
            outlineStrips.Add((er, size.x == 0f));
        }
        subMenu.AddComponent<RectMask2D>();
        subMenuCanvasGroup = subMenu.AddComponent<CanvasGroup>();
        subMenuCanvasGroup.alpha = 1f;
        GameObject content = new("Content");
        content.transform.SetParent(subMenu.transform, false);
        SubMenuContent = content.AddComponent<RectTransform>();
        SubMenuContent.anchorMin = new(0, 1);
        SubMenuContent.anchorMax = new(0, 1);
        SubMenuContent.pivot = new(0, 1);
        SubMenuContent.anchoredPosition = Vector2.zero;
        SubMenuContent.sizeDelta = new(SUBMENU_WIDTH, 0);
        GenerateUI.FitVertical(content, 0f);
        float outline = MainCore.Conf.OutlineWidth;
        OutlineEdge("OutlineBottom", new(0, 0), new(1, 0), new(0.5f, 0f), new(0f, outline));
        OutlineEdge("OutlineLeft", new(0, 0), new(0, 1), new(0f, 0.5f), new(outline, 0f));
        OutlineEdge("OutlineRight", new(1, 0), new(1, 1), new(1f, 0.5f), new(outline, 0f));
    }
    private static void CreateBottomBand(Transform parent) {
        bandHeight = ClampBand(MainCore.Conf.ContextBandHeight > 0f ? MainCore.Conf.ContextBandHeight : DefaultBandHeight);
        GameObject band = new("BottomBand");
        band.transform.SetParent(parent, false);
        BottomBand = band.AddComponent<RectTransform>();
        BottomBand.anchorMin = new(0, 0);
        BottomBand.anchorMax = new(1, 0);
        BottomBand.pivot = new(0.5f, 0f);
        BottomBand.offsetMin = new(MENU_WIDTH, 0f);
        BottomBand.offsetMax = new(0f, bandHeight);
        var image = band.AddComponent<Image>();
        image.color = UIColors.TopBar;
        band.AddComponent<RectMask2D>();
        bandCanvasGroup = band.AddComponent<CanvasGroup>();
        GameObject bandHairline = new("Outline");
        bandHairline.transform.SetParent(band.transform, false);
        RectTransform bandHairlineRect = bandHairline.AddComponent<RectTransform>();
        bandHairlineRect.anchorMin = new(0, 1);
        bandHairlineRect.anchorMax = new(1, 1);
        bandHairlineRect.pivot = new(0.5f, 1f);
        bandHairlineRect.sizeDelta = new(0f, MainCore.Conf.OutlineWidth);
        bandHairlineRect.anchoredPosition = Vector2.zero;
        Image bandHairlineImg = bandHairline.AddComponent<Image>();
        bandHairlineImg.color = Color.white;
        bandHairlineImg.raycastTarget = false;
        outlineStrips.Add((bandHairlineRect, true));
        GameObject content = new("Content");
        content.transform.SetParent(band.transform, false);
        BottomBandContent = content.AddComponent<RectTransform>();
        BottomBandContent.anchorMin = Vector2.zero;
        BottomBandContent.anchorMax = Vector2.one;
        BottomBandContent.offsetMin = Vector2.zero;
        BottomBandContent.offsetMax = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 8f;
        GameObject livePreview = new("LivePreviewPane");
        livePreview.transform.SetParent(content.transform, false);
        RectTransform livePreviewRect = livePreview.AddComponent<RectTransform>();
        var livePreviewLayout = livePreview.AddComponent<LayoutElement>();
        livePreviewLayout.minHeight = 0f;
        livePreviewLayout.preferredHeight = 140f;
        livePreviewLayout.flexibleHeight = 0f;
        GameObject liveCard = new("Card");
        liveCard.transform.SetParent(livePreview.transform, false);
        RectTransform liveCardRect = liveCard.AddComponent<RectTransform>();
        liveCardRect.anchorMin = Vector2.zero;
        liveCardRect.anchorMax = Vector2.one;
        liveCardRect.offsetMin = new(8f, 8f);
        liveCardRect.offsetMax = new(-8f, -8f);
        Image liveCardBg = liveCard.AddComponent<Image>();
        liveCardBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        liveCardBg.type = Image.Type.Sliced;
        liveCardBg.color = UIColors.ObjectBG;
        GameObject liveDivider = new("Divider");
        liveDivider.transform.SetParent(livePreview.transform, false);
        RectTransform liveDividerRect = liveDivider.AddComponent<RectTransform>();
        liveDividerRect.anchorMin = new(0f, 0f);
        liveDividerRect.anchorMax = new(1f, 0f);
        liveDividerRect.pivot = new(0.5f, 0f);
        liveDividerRect.sizeDelta = new(0f, 1f);
        liveDividerRect.anchoredPosition = Vector2.zero;
        Image liveDividerImg = liveDivider.AddComponent<Image>();
        liveDividerImg.color = new Color(1f, 1f, 1f, 0.08f);
        GameObject contextPane = new("ContextPane");
        contextPane.transform.SetParent(content.transform, false);
        RectTransform contextPaneRect = contextPane.AddComponent<RectTransform>();
        var contextPaneLayout = contextPane.AddComponent<LayoutElement>();
        contextPaneLayout.minHeight = 0f;
        contextPaneLayout.flexibleHeight = 1f;
        RectTransform contextContentRect = PageFactory.CreateScrollablePage(contextPaneRect);
        ContextPane.Attach(contextPaneRect, contextContentRect);
        LivePreviewPane.Attach(livePreviewRect, liveCardRect);
        GameObject divider = new("Divider");
        divider.transform.SetParent(band.transform, false);
        RectTransform dividerRect = divider.AddComponent<RectTransform>();
        dividerRect.anchorMin = new(0, 1);
        dividerRect.anchorMax = new(1, 1);
        dividerRect.pivot = new(0.5f, 0.5f);
        dividerRect.sizeDelta = new(0, 8);
        dividerRect.anchoredPosition = Vector2.zero;
        var dividerImg = divider.AddComponent<Image>();
        dividerImg.color = Color.clear;
        var paneDivider = divider.AddComponent<PaneDivider>();
        paneDivider.Target = BottomBand;
        paneDivider.CoordinateSpace = parent as RectTransform;
        paneDivider.Axis = PaneDividerAxis.Vertical;
        paneDivider.MinSize = BandMinHeight;
        paneDivider.MaxSize = BandMaxHeight;
        paneDivider.OnResized = h => {
            bandHeight = ClampBand(h);
            bandShown = bandHeight;
            if(!Mathf.Approximately(bandHeight, h)) BottomBand.sizeDelta = new Vector2(BottomBand.sizeDelta.x, bandHeight);
            Page.offsetMin = new Vector2(Page.offsetMin.x, bandHeight);
        };
        paneDivider.OnResizeEnd = _ => {
            MainCore.Conf.ContextBandHeight = bandHeight;
            MainCore.ConfMgr.RequestSave();
        };
        RefreshBand(false);
    }
    public static void RefreshBand(bool animate) {
        if(BottomBand == null || Page == null) return;
        bool open = ContextPane.HasContent || LivePreviewPane.HasContent;
        float target = open ? ClampBand(bandHeight) : 0f;
        bandSeq?.Kill();
        if(open) BottomBand.gameObject.SetActive(true);
        if(!animate) {
            ApplyBandHeight(target);
            if(!open) BottomBand.gameObject.SetActive(false);
            return;
        }
        bandSeq = GTweenExtensions.Tween(
            () => bandShown,
            ApplyBandHeight,
            target,
            0.28f
        ).SetEasing(Easing.OutExpo);
        bandSeq.OnComplete(() => {
            if(!open && BottomBand != null) BottomBand.gameObject.SetActive(false);
        });
        MainCore.TC.Play(bandSeq);
    }
    private static void ApplyBandHeight(float h) {
        bandShown = h;
        if(BottomBand != null) BottomBand.sizeDelta = new Vector2(BottomBand.sizeDelta.x, h);
        if(Page != null) Page.offsetMin = new Vector2(Page.offsetMin.x, h);
        if(bandCanvasGroup != null) {
            float full = Mathf.Max(1f, ClampBand(bandHeight));
            bandCanvasGroup.alpha = Mathf.Clamp01(h / full);
        }
    }
    private static float ClampBand(float h) =>
        Mathf.Clamp(h, BandMinHeight, Mathf.Min(BandMaxHeight, Panel.sizeDelta.y - 2f - TOP_BAR_HEIGHT - MinPageHeight));
    private static GTween shellSeq;
    private static void ApplyShellLayout(bool animate, float duration = 0f) {
        shellSeq?.Kill();
        if(!animate) {
            SnapShellLayout();
            return;
        }
        Vector2 menuTarget = isMenuOpen ? MenuOpenPosition : MenuClosedPosition;
        float subMenuX = isMenuOpen ? MENU_WIDTH : -SUBMENU_WIDTH;
        float subMenuW = subMenuHasChildren ? SUBMENU_WIDTH : 0f;
        float leftInset = isMenuOpen ? MENU_WIDTH + (subMenuHasChildren ? SUBMENU_WIDTH : 0f) : 0f;
        float menuAlpha = isMenuOpen ? 1f : 0f;
        float subMenuAlpha = isMenuOpen && subMenuHasChildren ? 1f : 0f;
        menuCanvasGroup.interactable = isMenuOpen;
        menuCanvasGroup.blocksRaycasts = isMenuOpen;
        subMenuCanvasGroup.interactable = isMenuOpen && subMenuHasChildren;
        subMenuCanvasGroup.blocksRaycasts = isMenuOpen && subMenuHasChildren;
        float pageBaseX = PrepareSlide(Page, leftInset, bandShown);
        float bandBaseX = PrepareSlide(BottomBand, leftInset, 0f);
        shellSeq = GTweenSequenceBuilder.New()
            .Join(Menu.GTAnchorPos(menuTarget, duration).SetEasing(Easing.OutExpo))
            .Join(SubMenu.GTAnchorPosX(subMenuX, duration).SetEasing(Easing.OutExpo))
            .Join(SubMenu.GTSizeDelta(new Vector2(subMenuW, SubMenu.sizeDelta.y), duration).SetEasing(Easing.OutExpo))
            .Join(Page.GTAnchorPosX(pageBaseX, duration).SetEasing(Easing.OutExpo))
            .Join(BottomBand.GTAnchorPosX(bandBaseX, duration).SetEasing(Easing.OutExpo))
            .Join(menuCanvasGroup.GTFade(menuAlpha, Mathf.Min(duration, 0.3f)).SetEasing(Easing.OutSine))
            .Join(subMenuCanvasGroup.GTFade(subMenuAlpha, Mathf.Min(duration, 0.3f)).SetEasing(Easing.OutSine))
            .AppendCallback(SnapShellLayout)
            .Build();
        MainCore.TC.Play(shellSeq);
    }
    private static float PrepareSlide(RectTransform rect, float targetLeft, float minY) {
        float shift = rect.offsetMin.x - targetLeft;
        rect.offsetMin = new Vector2(targetLeft, minY);
        rect.offsetMax = new Vector2(Mathf.Max(0f, -shift), rect.offsetMax.y);
        float baseX = rect.anchoredPosition.x;
        rect.anchoredPosition = new Vector2(baseX + shift, rect.anchoredPosition.y);
        return baseX;
    }
    private static void SnapShellLayout() {
        Vector2 menuTarget = isMenuOpen ? MenuOpenPosition : MenuClosedPosition;
        float subMenuX = isMenuOpen ? MENU_WIDTH : -SUBMENU_WIDTH;
        float subMenuW = subMenuHasChildren ? SUBMENU_WIDTH : 0f;
        float leftInset = isMenuOpen ? MENU_WIDTH + (subMenuHasChildren ? SUBMENU_WIDTH : 0f) : 0f;
        Menu.anchoredPosition = menuTarget;
        SubMenu.anchoredPosition = new Vector2(subMenuX, SubMenu.anchoredPosition.y);
        SubMenu.sizeDelta = new Vector2(subMenuW, SubMenu.sizeDelta.y);
        Page.offsetMin = new Vector2(leftInset, bandShown);
        Page.offsetMax = new Vector2(0f, Page.offsetMax.y);
        BottomBand.offsetMin = new Vector2(leftInset, 0f);
        BottomBand.offsetMax = new Vector2(0f, BottomBand.offsetMax.y);
        menuCanvasGroup.alpha = isMenuOpen ? 1f : 0f;
        subMenuCanvasGroup.alpha = isMenuOpen && subMenuHasChildren ? 1f : 0f;
        menuCanvasGroup.interactable = isMenuOpen;
        menuCanvasGroup.blocksRaycasts = isMenuOpen;
        subMenuCanvasGroup.interactable = isMenuOpen && subMenuHasChildren;
        subMenuCanvasGroup.blocksRaycasts = isMenuOpen && subMenuHasChildren;
    }
    public static void SetSubMenuVisible(bool hasChildren, bool animate) {
        subMenuHasChildren = hasChildren;
        ApplyShellLayout(animate, 0.22f);
    }
}
