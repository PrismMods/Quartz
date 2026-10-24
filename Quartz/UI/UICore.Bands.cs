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
        SubMenu.anchorMin = new(0, 1);
        SubMenu.anchorMax = new(1, 1);
        SubMenu.pivot = new(0.5f, 1f);
        SubMenu.offsetMin = new(0f, -TOP_BAR_HEIGHT);
        SubMenu.offsetMax = new(0f, -TOP_BAR_HEIGHT);
        var image = subMenu.AddComponent<Image>();
        image.color = UIColors.TopBar;
        subMenu.AddComponent<RectMask2D>();
        subMenuCanvasGroup = subMenu.AddComponent<CanvasGroup>();
        subMenuCanvasGroup.alpha = 1f;
        GameObject content = new("Content");
        content.transform.SetParent(subMenu.transform, false);
        SubMenuContent = content.AddComponent<RectTransform>();
        SubMenuContent.anchorMin = new(0, 0);
        SubMenuContent.anchorMax = new(0, 1);
        SubMenuContent.pivot = new(0, 0.5f);
        SubMenuContent.anchoredPosition = Vector2.zero;
        SubMenuContent.sizeDelta = Vector2.zero;
        var layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 4f;
        layout.padding = new RectOffset(8, 8, 6, 6);
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        subMenu.AddComponent<StripWheel>().Init(SubMenu, SubMenuContent);
    }
    private static void CreateBottomBand(Transform parent) {
        bandHeight = ClampBand(MainCore.Conf.ContextBandHeight > 0f ? MainCore.Conf.ContextBandHeight : DefaultBandHeight);
        GameObject band = new("BottomBand");
        band.transform.SetParent(parent, false);
        BottomBand = band.AddComponent<RectTransform>();
        BottomBand.anchorMin = new(0, 0);
        BottomBand.anchorMax = new(1, 0);
        BottomBand.pivot = new(0.5f, 0f);
        // SnapShellLayout corrects this on the first layout pass anyway; seeding it
        // right just avoids a one-frame band indent in the single-page shell.
        BottomBand.offsetMin = new(ShellNavHidden ? 0f : RAIL_WIDTH, 0f);
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
        bandHairlineRect.sizeDelta = new(0f, OUTLINE_WIDTH);
        bandHairlineRect.anchoredPosition = Vector2.zero;
        Image bandHairlineImg = bandHairline.AddComponent<Image>();
        bandHairlineImg.color = UIColors.Border;
        bandHairlineImg.raycastTarget = false;
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
    private static float subTabShown;
    private static float railShown;
    private static GTween railSeq;
    private const float RailHoverDelay = 0.1f;
    private static void SetRailExpanded(bool expanded) {
        if(Menu == null) return;
        railSeq?.Kill();
        // Expanding waits for a short dwell so passing over the rail doesn't pop it open.
        railSeq = GTweenSequenceBuilder.New()
            .AppendTime(expanded ? RailHoverDelay : 0f)
            .Append(GTweenExtensions.Tween(
                () => railShown,
                ApplyRail,
                expanded ? 1f : 0f,
                expanded ? 0.22f : 0.18f
            ).SetEasing(Easing.OutExpo))
            .Build();
        MainCore.TC.Play(railSeq);
    }
    private static void ApplyRail(float t) {
        railShown = t;
        Menu.sizeDelta = new Vector2(Mathf.Lerp(RAIL_WIDTH, MENU_WIDTH, t), Menu.sizeDelta.y);
        MenuFactory.SetLabelAlpha(t);
        if(menuVersionText != null) menuVersionText.alpha = 0.4f * t;
    }
    private static void ApplySubTabHeight(float h) {
        subTabShown = h;
        SubMenu.offsetMin = new Vector2(SubMenu.offsetMin.x, -TOP_BAR_HEIGHT - h);
        Page.offsetMax = new Vector2(Page.offsetMax.x, -TOP_BAR_HEIGHT - h);
    }
    private static void ApplyShellLayout(bool animate, float duration = 0f) {
        shellSeq?.Kill();
        if(!animate) {
            SnapShellLayout();
            return;
        }
        Vector2 menuTarget = isMenuOpen ? MenuOpenPosition : MenuClosedPosition;
        bool subVisible = isMenuOpen && subMenuHasChildren;
        float leftInset = isMenuOpen ? RAIL_WIDTH : 0f;
        float menuAlpha = isMenuOpen ? 1f : 0f;
        float subMenuAlpha = subVisible ? 1f : 0f;
        menuCanvasGroup.interactable = isMenuOpen;
        menuCanvasGroup.blocksRaycasts = isMenuOpen;
        subMenuCanvasGroup.interactable = subVisible;
        subMenuCanvasGroup.blocksRaycasts = subVisible;
        float pageBaseX = PrepareSlide(Page, leftInset, bandShown);
        float bandBaseX = PrepareSlide(BottomBand, leftInset, 0f);
        float subMenuBaseX = PrepareSlide(SubMenu, leftInset, SubMenu.offsetMin.y);
        shellSeq = GTweenSequenceBuilder.New()
            .Join(Menu.GTAnchorPos(menuTarget, duration).SetEasing(Easing.OutExpo))
            .Join(SubMenu.GTAnchorPosX(subMenuBaseX, duration).SetEasing(Easing.OutExpo))
            .Join(Page.GTAnchorPosX(pageBaseX, duration).SetEasing(Easing.OutExpo))
            .Join(BottomBand.GTAnchorPosX(bandBaseX, duration).SetEasing(Easing.OutExpo))
            .Join(GTweenExtensions.Tween(
                () => subTabShown,
                ApplySubTabHeight,
                subVisible ? SUBTAB_HEIGHT : 0f,
                duration
            ).SetEasing(Easing.OutExpo))
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
        bool subVisible = isMenuOpen && subMenuHasChildren;
        float leftInset = isMenuOpen ? RAIL_WIDTH : 0f;
        Menu.anchoredPosition = menuTarget;
        SubMenu.offsetMin = new Vector2(leftInset, SubMenu.offsetMin.y);
        SubMenu.offsetMax = new Vector2(0f, SubMenu.offsetMax.y);
        Page.offsetMin = new Vector2(leftInset, bandShown);
        Page.offsetMax = new Vector2(0f, Page.offsetMax.y);
        ApplySubTabHeight(subVisible ? SUBTAB_HEIGHT : 0f);
        BottomBand.offsetMin = new Vector2(leftInset, 0f);
        BottomBand.offsetMax = new Vector2(0f, BottomBand.offsetMax.y);
        menuCanvasGroup.alpha = isMenuOpen ? 1f : 0f;
        subMenuCanvasGroup.alpha = subVisible ? 1f : 0f;
        menuCanvasGroup.interactable = isMenuOpen;
        menuCanvasGroup.blocksRaycasts = isMenuOpen;
        subMenuCanvasGroup.interactable = subVisible;
        subMenuCanvasGroup.blocksRaycasts = subVisible;
    }
    public static void SetSubMenuVisible(bool hasChildren, bool animate) {
        subMenuHasChildren = hasChildren;
        ApplyShellLayout(animate, 0.22f);
    }
}
