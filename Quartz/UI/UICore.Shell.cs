using Quartz.Async;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Factory;
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
    public static RectTransform Panel;
    public static Image CloseImage;
    public const float MENU_WIDTH = 210f;
    // Collapsed rail width; the rail widens to MENU_WIDTH over the page on hover.
    public const float RAIL_WIDTH = 76f;
    private const float POWER_HEIGHT = 60f;
    // How much of the window must stay on screen when it is dragged off an edge.
    internal const float PANEL_KEEP_VISIBLE = 120f;
    public const float SUBTAB_HEIGHT = 44f;
    public const float OUTLINE_WIDTH = 3f;
    // The border ring paints over the rail's left edge, so center on what stays visible.
    internal static float RailIconCenter => (RAIL_WIDTH + OUTLINE_WIDTH) * 0.5f;
    private const float TOP_BAR_HEIGHT = 60f;
    public static RectTransform Menu;
    public static RectTransform MenuContent;
    public static RectTransform SubMenu;
    public static RectTransform SubMenuContent;
    private static RectTransform Page;
    public static RectTransform BottomBand;
    public static RectTransform BottomBandContent;
    private const float BandMinHeight = 160f;
    private const float BandMaxHeight = 500f;
    private const float DefaultBandHeight = 260f;
    private const float MinPageHeight = 200f;
    private const string WordmarkFont = "Gmarket Sans Medium";
    private static bool subMenuHasChildren;
    private static float bandHeight;
    private static float bandShown;
    private static CanvasGroup bandCanvasGroup;
    private static GTween bandSeq;
    private static CanvasGroup menuCanvasGroup;
    private static CanvasGroup subMenuCanvasGroup;
    private static TMP_Text menuVersionText;
    private static CanvasGroup panelCanvasGroup;
    private static CanvasGroup reorganizeHubGroup;
    private static GTween reorganizeSeq;
    private static GameObject reorganizeHub;
    private static GameObject reorganizeDim;
    private static CanvasGroup reorganizeDimGroup;
    private static bool panelOverHub;
    public static float PanelScale {
        get;
        set {
            field = value;
            canvasScaler.referenceResolution =
                new Vector2(ReferenceResolution.x, ReferenceResolution.y) / field;
        }
    } = 1f;
    private static void CreatePanel() {
        GameObject panel = new("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        {
            var image = panel.AddComponent<Image>();
            image.color = UIColors.PanelBG;
            image.type = Image.Type.Sliced;
            image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        }
        Panel = panel.GetComponent<RectTransform>();
        Panel.anchorMin = new(0.5f, 0.5f);
        Panel.anchorMax = new(0.5f, 0.5f);
        Panel.pivot = new(0.5f, 0.5f);
        Panel.sizeDelta = LastPanelSize = LoadSavedPanelSize();
        LastPanelPosition = Panel.anchoredPosition;
        panel.AddComponent<RectMask2D>();
        panelCanvasGroup = panel.AddComponent<CanvasGroup>();
        {
            GameObject menuPanel = new("MenuPanel");
            menuPanel.transform.SetParent(panel.transform, false);
            var menuPanelRect = menuPanel.AddComponent<RectTransform>();
            menuPanelRect.anchorMin = Vector2.zero;
            menuPanelRect.anchorMax = new(1, 1);
            menuPanelRect.pivot = new(0.5f, 0.5f);
            menuPanelRect.anchoredPosition = Vector2.zero;
            menuPanelRect.offsetMin = Vector2.zero;
            menuPanelRect.offsetMax = Vector2.zero;
            menuPanelRect.sizeDelta = Vector2.zero;
            var maskImage = menuPanel.AddComponent<Image>();
            maskImage.color = Color.white;
            maskImage.type = Image.Type.Sliced;
            maskImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
            maskImage.raycastTarget = false;
            var mask = menuPanel.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Page = PageFactory.CreatePages(menuPanel);
            pageCanvasGroup = Page.gameObject.AddComponent<CanvasGroup>();
            CreateInactiveOverlay(Page);
            CreateBottomBand(menuPanel.transform);
            ApplyPowerLock();
            CreateSubMenu(menuPanel.transform);
            GameObject menu = new("Menu");
            menu.transform.SetParent(menuPanel.transform, false);
            Menu = menu.AddComponent<RectTransform>();
            Menu.anchorMin = Vector2.zero;
            Menu.anchorMax = new(0, 1);
            Menu.pivot = new(0, 0.5f);
            Menu.sizeDelta = new(RAIL_WIDTH, -TOP_BAR_HEIGHT);
            // Single-page shell: the rail starts off-canvas and the page has no left inset.
            isMenuOpen = !ShellNavHidden;
            Menu.anchoredPosition = isMenuOpen ? MenuOpenPosition : MenuClosedPosition;
            var image = menu.AddComponent<Image>();
            image.color = UIColors.MenuBG;
            menuCanvasGroup = Menu.gameObject.AddComponent<CanvasGroup>();
            menuCanvasGroup.alpha = isMenuOpen ? 1f : 0f;
            menuCanvasGroup.interactable = isMenuOpen;
            menuCanvasGroup.blocksRaycasts = isMenuOpen;
            menu.AddComponent<RectMask2D>();
            EventTrigger railTrigger = menu.AddComponent<EventTrigger>();
            var railEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            railEnter.callback.AddListener(_ => SetRailExpanded(true));
            var railExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            railExit.callback.AddListener(_ => SetRailExpanded(false));
            railTrigger.triggers.Add(railEnter);
            railTrigger.triggers.Add(railExit);
            // The category list scrolls above the power button instead of running under it.
            GameObject viewport = new("Viewport");
            viewport.transform.SetParent(Menu, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(0f, POWER_HEIGHT);
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            GameObject content = new("Content");
            content.transform.SetParent(viewportRect, false);
            MenuContent = content.AddComponent<RectTransform>();
            MenuContent.anchorMin = new(0, 1);
            MenuContent.anchorMax = new(1, 1);
            MenuContent.pivot = new(0.5f, 1);
            MenuContent.offsetMin = Vector2.zero;
            MenuContent.offsetMax = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;
            layout.padding = new() {
                left = 0,
                right = 0,
                top = 0,
                bottom = 0
            };
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            viewport.AddComponent<UIScrollController>().SetContent(MenuContent, viewportRect);
            MenuFactory.CreateMenu(MenuContent);
            GameObject power = new("Power");
            power.transform.SetParent(Menu, false);
            var powerRect = power.AddComponent<RectTransform>();
            powerRect.anchorMin = new Vector2(0f, 0f);
            powerRect.anchorMax = new Vector2(1f, 0f);
            powerRect.offsetMin = Vector2.zero;
            powerRect.offsetMax = Vector2.zero;
            powerRect.sizeDelta = new Vector2(0f, POWER_HEIGHT);
            powerRect.pivot = new Vector2(0.5f, 0f);
            powerBg = power.AddComponent<Image>();
            powerBg.color = PowerColor(MainCore.Conf.Active);
            var btn = power.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(TogglePower);
            GameObject powerIcon = new("PowerIcon");
            powerIcon.transform.SetParent(powerRect, false);
            RectTransform powerIconRect = powerIcon.AddComponent<RectTransform>();
            powerIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            powerIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            powerIconRect.pivot = new Vector2(0.5f, 0.5f);
            // Centered at any rail width, shifted off the border ring like RailIconCenter.
            powerIconRect.anchoredPosition = new Vector2(OUTLINE_WIDTH * 0.5f, 0f);
            powerIconRect.sizeDelta = new Vector2(26f, 26f);
            Image powerIconImage = powerIcon.AddComponent<Image>();
            powerIconImage.sprite = MainCore.Spr.Get(UISprite.Power128);
            powerIconImage.color = new(1f, 1f, 1f, 0.6f);
            GameObject version = new("Version");
            version.transform.SetParent(Menu, false);
            var versionRect = version.AddComponent<RectTransform>();
            versionRect.anchorMin = Vector2.zero;
            versionRect.anchorMax = new(1f, 0f);
            versionRect.offsetMin = new(2f, 2f);
            versionRect.offsetMax = new(0f, 20f);
            versionRect.pivot = Vector2.zero;
            var versionText = version.AddComponent<TextMeshProUGUI>();
            versionText.text = $"v{Info.DisplayVersion}";
            versionText.font = FontManager.MenuFontAsset;
            versionText.fontSize = 12f;
            versionText.color = new Color(1f, 1f, 1f, 0.4f);
            versionText.characterSpacing = -3f;
            versionText.alignment = TextAlignmentOptions.BottomLeft;
            // Spans the power button's lower edge; must not eat its clicks.
            versionText.raycastTarget = false;
            menuVersionText = versionText;
            railSeq?.Kill();
            ApplyRail(0f);
        }
        GameObject topBar = new("TopBar");
        topBar.transform.SetParent(panel.transform, false);
        topBar.AddComponent<DragHandler>().KeepVisible = PANEL_KEEP_VISIBLE;
        var topImage = topBar.AddComponent<Image>();
        topImage.color = UIColors.TopBar;
        topImage.type = Image.Type.Sliced;
        topImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleHalf256P1024);
        var topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new(0, 1);
        topRect.anchorMax = new(1, 1);
        topRect.offsetMin = new(0, -60);
        topRect.offsetMax = Vector2.zero;
        topRect.pivot = new(0.5f, 1);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new(0, 60);
        {
            GameObject logo = new("Logo");
            logo.transform.SetParent(topBar.transform, false);
            // Clear hit area for the click; the tinted layers draw on top.
            logo.AddComponent<Image>().color = Color.clear;
            logo.AddComponent<ThemeExempt>();
            var logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = new(0, 0.5f);
            logoRect.anchorMax = new(0, 0.5f);
            logoRect.pivot = new(0, 0.5f);
            logoRect.anchoredPosition = new(14, 0);
            logoRect.sizeDelta = new(46f, 46f);
            QuartzLogo.Create(logoRect);
            var btn = logo.AddComponent<NonRaycastButton>();
            // No rail to summon in the single-page shell, so the logo is just a logo.
            if(!ShellNavHidden) btn.onClick += ToggleMenu;
        }
        {
            GameObject wordmark = new("Wordmark");
            wordmark.transform.SetParent(topBar.transform, false);
            var wordmarkText = wordmark.AddComponent<TextMeshProUGUI>();
            wordmarkText.text = Info.DisplayName.ToUpperInvariant();
            wordmarkText.font = FontManager.GetFont(WordmarkFont);
            wordmarkText.fontSize = 24f;
            wordmarkText.characterSpacing = 6f;
            wordmarkText.color = new Color(1f, 1f, 1f, 0.92f);
            wordmarkText.alignment = TextAlignmentOptions.MidlineLeft;
            wordmarkText.raycastTarget = false;
            wordmark.AddComponent<FontExempt>();
            var wordmarkRect = wordmark.GetComponent<RectTransform>();
            wordmarkRect.anchorMin = new(0, 0.5f);
            wordmarkRect.anchorMax = new(0, 0.5f);
            wordmarkRect.pivot = new(0, 0.5f);
            wordmarkRect.anchoredPosition = new(68, 0);
            wordmarkRect.sizeDelta = new(220f, 46f);
        }
        {
            GameObject close = new("Close");
            close.transform.SetParent(topBar.transform, false);
            var closeRect = close.AddComponent<RectTransform>();
            closeRect.anchorMin = new(1, 0.5f);
            closeRect.anchorMax = new(1, 0.5f);
            closeRect.pivot = new(1, 0.5f);
            closeRect.anchoredPosition = new(-16, 0);
            closeRect.sizeDelta = new(38, 38);
            GameObject bg = new("Bg");
            bg.transform.SetParent(close.transform, false);
            CloseImage = bg.AddComponent<Image>();
            CloseImage.sprite = MainCore.Spr.Get(UISprite.Circle256);
            CloseImage.color = new Color(UIColors.SoftRed.r, UIColors.SoftRed.g, UIColors.SoftRed.b, 0f);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            GameObject xObj = new("X");
            xObj.transform.SetParent(close.transform, false);
            Image xImage = xObj.AddComponent<Image>();
            xImage.sprite = MainCore.Spr.Get(UISprite.X128);
            RectTransform xRect = xObj.GetComponent<RectTransform>();
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = new(4, 4);
            xRect.offsetMax = new(-4, -4);
            EventTrigger trigger = close.AddComponent<EventTrigger>();
            UnityUtils.AddClickEvent(trigger, _ => ClosePanel());
            var enter = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerEnter
            };
            enter.callback.AddListener(_ => CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 1f));
            var exit = new EventTrigger.Entry {
                eventID = EventTriggerType.PointerExit
            };
            exit.callback.AddListener(_ => CloseImage.color = new Color(CloseImage.color.r, CloseImage.color.g, CloseImage.color.b, 0f));
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }
        {
            GameObject border = new("Border");
            border.transform.SetParent(panel.transform, false);
            RectTransform borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            Image borderImg = border.AddComponent<Image>();
            borderImg.sprite = MainCore.Spr.GetRing(12f, OUTLINE_WIDTH);
            borderImg.type = Image.Type.Sliced;
            borderImg.color = UIColors.Border;
            borderImg.raycastTarget = false;
        }
    }
}
