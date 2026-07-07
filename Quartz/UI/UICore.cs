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

public enum OriginalMenuState {
    // Each contiguous block below is one sidebar category (see
    // MenuFactory.CategoryChildren). A category's FIRST value is its
    // "representative" state — the target of its column-1 row and the
    // CategoryChildren key. Child display order matches the old on-tab section
    // order. The trailing block (Profiles…Developer) are childless categories,
    // each its own single state. Enum is never persisted by name, so this
    // reshape can't corrupt saved settings.

    // Overlay
    OverlayGeneral,
    KeyViewer,
    ProgressBar,
    Combo,
    Judgement,
    SongTitle,
    Panels,
    // Gameplay
    GameplayKeyLimiter,
    GameplayChatter,
    GameplayJudgement,
    GameplayDeath,
    GameplayAutoDeafen,
    // Visuals
    VisualsEffectRemover,
    VisualsHideJudgements,
    VisualsVisualTweaks,
    VisualsPlanetColors,
    VisualsOttoIcon,
    VisualsUiHiding,
    // Tweaks
    TweaksGeneral,
    TweaksOptimizer,
    TweaksMainMenu,
    TweaksResults,
    // Editor
    EditorInspector,
    EditorTileReadout,
    EditorBga,
    // Nostalgia — one category aggregating the retro toggles that used to be
    // injected into the four feature tabs, split by their old host tab.
    NostalgiaGameplay,
    NostalgiaVisuals,
    NostalgiaTweaks,
    NostalgiaEditor,
    // Single-page categories (no column-2)
    Profiles,
    Import,
    // Addons — the category page (addon list/reload). Addon-registered pages
    // are NOT enum values: they get dynamic states past the enum (AddonUI)
    // and show as this category's column-2 children.
    Addons,
    Settings,
    Search,
    Credits,
    Developer, // dev-only tab; the menu item is created only in "dev" builds
}

public static class UICore {
    private static GameObject canvasObj;
    private static Canvas canvas;
    private static CanvasScaler canvasScaler;

    public static readonly Dictionary<int, RectTransform> Pages = [];
    public static int CurrentMenuState = (int)OriginalMenuState.OverlayGeneral;

    // Reorganize is a mode (not a tab): it hides the settings panel and lets
    // the on-screen overlay elements be dragged. Entered from a button on the
    // Overlay page, exited via the floating Exit button.
    public static bool IsReorganizing { get; private set; }
    public static readonly Vector2 ReferenceResolution = new(1920, 1080);

    private static Action<TranslationFailState> _onPageSettings;
    private static Action<TranslationFailState> _onRefresh;
    private static Action<int> _onDockTabChanged;
    private static Action _onPaneChanged;

    public static void Initialize() {
        canvasObj = new GameObject("QuartzUICanvas");
        canvasObj.transform.SetParent(MainCore.Root.transform, false);
        canvasObj.SetActive(false);

        // Texts under this canvas follow the settings-window font; everything
        // else under the mod root uses the overlay font.
        FontManager.MenuRoot = canvasObj.transform;

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        canvasScaler = canvasObj.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        PanelScale = MainCore.Conf.UIScale;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        UIColors.ApplyAccent(MainCore.Conf.GetAccentColor());
        CreatePanel();
        ResizeHandle.CreateResizeHandles(Panel, canvasObj.GetComponent<RectTransform>());
        Tooltip.Initialize(canvasObj.transform);
        UpdateToast.Initialize();

        CreateExitReorganizeButton();

        _onPageSettings = state => {
            if(state == TranslationFailState.Success) PageSettings.OnTranslatorLoadEnd();
        };

        _onRefresh = state => {
            if(state == TranslationFailState.Success) {
                // Dropdowns rebuild their option rows on load-end (their own
                // handlers run first — subscribed during CreatePanel above) —
                // new Images the accent-preview cache can't know about.
                themeImages = null;
                TextLocalization.RefreshAll();
            }
        };

        MainCore.Tr.OnLoadEnd += _onPageSettings;
        MainCore.Tr.OnLoadEnd += _onRefresh;

        // Any tab switch clears the Context/Live Preview panes — simpler than
        // hardcoding which tab "owns" them, and still correct once more pages
        // start pushing content into them.
        _onDockTabChanged = _ => {
            ContextPane.Clear();
            LivePreviewPane.Clear();
        };
        MenuFactory.OnStateChanged += _onDockTabChanged;

        // Reveal/hide the bottom band (animated) whenever pane content changes.
        _onPaneChanged = () => RefreshBand(true);
        Panes.PaneState.Changed += _onPaneChanged;

        TextLocalization.RefreshAll();

        // Every settings-window text was built with the overlay font; if a
        // window-specific font is set, re-point the window's texts to it now.
        FontManager.ApplyMenuFont();

        if(MainCore.Conf.IsFirstRun) MakeFirstRunHelper();

        if(MainCore.Conf.ShowOnStartup) Open(true);
    }

    private static void CreateExitReorganizeButton() {
        exitReorganizeObj = new GameObject("ExitReorganizeButton");
        exitReorganizeObj.transform.SetParent(canvasObj.transform, false);

        var rect = exitReorganizeObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(240f, 60f);
        rect.anchoredPosition = new Vector2(0f, -40f);

        exitReorganizeCanvasGroup = exitReorganizeObj.AddComponent<CanvasGroup>();

        var img = exitReorganizeObj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        img.type = Image.Type.Sliced;
        img.color = Color.Lerp(UIColors.MenuHighlight, UIColors.MenuSelected, 0.5f);

        var btn = exitReorganizeObj.AddComponent<Button>();
        btn.onClick.AddListener(() => ExitReorganize());

        GameObject textObj = new("Text");
        textObj.transform.SetParent(rect, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = "Exit Reorganize";
        label.font = FontManager.Current;
        label.fontSize = 24f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        label.gameObject.AddComponent<TextLocalization>()
            .Init("EXIT_REORGANIZE", "Exit Reorganize");

        exitReorganizeObj.SetActive(false);
    }

    // Enter reorganize mode: fade the settings panel out (then hide it),
    // reveal the on-screen draggable overlay elements and fade the floating
    // Exit button in.
    public static void EnterReorganize() {
        if(IsReorganizing) return;

        IsReorganizing = true;

        if(panelCanvasGroup != null) {
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if(exitReorganizeObj != null) exitReorganizeObj.SetActive(true);

        if(exitReorganizeCanvasGroup != null) exitReorganizeCanvasGroup.alpha = 0f;

        reorganizeSeq?.Kill();
        reorganizeSeq = GTweenSequenceBuilder.New()
            .Join(panelCanvasGroup.GTFade(0f, 0.2f).SetEasing(Easing.OutSine))
            .Join(exitReorganizeCanvasGroup.GTFade(1f, 0.2f).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(IsReorganizing && Panel != null) Panel.gameObject.SetActive(false);
            })
            .Build();
        MainCore.TC.Play(reorganizeSeq);
    }

    public static void ExitReorganize() {
        if(!IsReorganizing) return;

        IsReorganizing = false;

        // Drop any element selection (outline + position panel) right away —
        // the per-overlay drag surfaces deactivate on their next Update.
        Reorganizer.Deselect();

        if(Panel != null) Panel.gameObject.SetActive(true);

        if(panelCanvasGroup != null) {
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        reorganizeSeq?.Kill();
        reorganizeSeq = GTweenSequenceBuilder.New()
            .Join(panelCanvasGroup.GTFade(Mathf.Clamp01(MainCore.Conf.PanelOpacity), 0.2f).SetEasing(Easing.OutSine))
            .Join(exitReorganizeCanvasGroup.GTFade(0f, 0.2f).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(!IsReorganizing && exitReorganizeObj != null) exitReorganizeObj.SetActive(false);
            })
            .Build();
        MainCore.TC.Play(reorganizeSeq);
    }

    // Live-applies the settings-window opacity (used by the Settings slider).
    public static void SetPanelOpacity(float value, bool save) {
        MainCore.Conf.PanelOpacity = Mathf.Clamp01(value);

        if(panelCanvasGroup != null && !IsReorganizing) panelCanvasGroup.alpha = MainCore.Conf.PanelOpacity;

        if(save) MainCore.ConfMgr.RequestSave();
    }

    private static bool firstRunHelperActivated = false;
    private static GameObject firstRunCanvasObj;
    private static Image firstRunHelperImage;
    private static TextMeshProUGUI firstRunHelperText;
    private static GTween firstRunHelperImageSequence;
    private static GTween secondRunHelperTextSequence;

    private static void MakeFirstRunHelper() {
        Task.Run(async () => {
            await Task.Delay(4000);
            MainThread.Enqueue(() => {
                firstRunHelperActivated = true;

                firstRunCanvasObj = new GameObject("FirstRunHelperCanvas");
                firstRunCanvasObj.transform.SetParent(MainCore.Root.transform, false);

                firstRunCanvasObj.AddComponent<RectTransform>();

                var canvas = firstRunCanvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767;

                var scaler = firstRunCanvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                var frh = new GameObject("FirstRunHelper");
                var frhRect = frh.AddComponent<RectTransform>();
                frh.transform.SetParent(firstRunCanvasObj.transform, false);

                frhRect.anchorMin = new Vector2(0f, 0f);
                frhRect.anchorMax = new Vector2(1f, 0f);
                frhRect.pivot = new Vector2(0.5f, 0f);
                frhRect.offsetMin = new Vector2(0f, 0f);
                frhRect.offsetMax = new Vector2(0f, 4f);

                firstRunHelperImage = frh.AddComponent<Image>();
                firstRunHelperImage.raycastTarget = false;
                firstRunHelperImage.color = new Color(1f, 1f, 1f, 0f);

                var frhTextObj = new GameObject("Text");
                var frhTextRect = frhTextObj.AddComponent<RectTransform>();
                frhTextObj.transform.SetParent(frh.transform, false);

                var tmp = frhTextObj.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 22f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Bottom;
                tmp.text = "";
                tmp.font = FontManager.Current;

                frhTextRect.anchorMin = new Vector2(0.5f, 0.5f);
                frhTextRect.anchorMax = new Vector2(0.5f, 0.5f);
                frhTextRect.anchoredPosition = new Vector2(0f, 6f);
                frhTextRect.sizeDelta = new Vector2(1000f, 50f);
                frhTextRect.pivot = new Vector2(0.5f, 0f);

                firstRunHelperText = tmp;
                firstRunHelperImageSequence = GTweenSequenceBuilder.New()
                    .Append(firstRunHelperImage.GTAlpha(1.6f, 0.1f).SetEasing(Easing.OutSine))
                    .Append(firstRunHelperImage.GTAlpha(0.04f, 1f).SetEasing(Easing.OutSine))
                    .Build()
                    .SetMaxLoops();

                string fullText = string.Format(
                    MainCore.Tr.Get("FIRST_RUN_PRESS", "Press {0}"),
                    Keybind.Format(
                        (Keybind.KeyModifier)MainCore.Conf.ToggleModifier,
                        (KeyCode)MainCore.Conf.ToggleKey
                    )
                );
                secondRunHelperTextSequence = GTweenSequenceBuilder.New()
                    .Append(GTweenExtensions.Tween(
                        () => 0,
                        x => firstRunHelperText.text = fullText[..x],
                        fullText.Length,
                        1.4f
                    ).SetEasing(Easing.OutSine))
                    .Build();

                MainCore.TC.Play(firstRunHelperImageSequence);
                MainCore.TC.Play(secondRunHelperTextSequence);
            });
        });
    }

    private static void EndFirstRunHelper() {
        MainCore.Conf.IsFirstRun = false;
        MainCore.ConfMgr.Save();

        firstRunHelperImageSequence?.Kill();
        secondRunHelperTextSequence?.Kill();

        firstRunHelperText.text = "";
        string endText = MainCore.Tr.Get("FIRST_RUN_GREAT_JOB", "Great Job!");

        var sequence = GTweenSequenceBuilder.New()
            .Append(firstRunHelperImage.GTAlpha(1.0f, 0.2f).SetEasing(Easing.OutSine))
            .Join(GTweenExtensions.Tween(
                () => 0,
                x => firstRunHelperText.text = endText[..x],
                endText.Length,
                0.8f
            ).SetEasing(Easing.Linear))
            .AppendTime(3.0f)
            .Append(firstRunHelperImage.GTAlpha(0f, 2.0f))
            .Join(firstRunHelperText.GTAlpha(0f, 2.0f))

            .AppendCallback(() => {
                if(firstRunCanvasObj != null) UnityEngine.Object.Destroy(firstRunCanvasObj);
            })
            .Build();

        MainCore.TC.Play(sequence);
    }

    public static RectTransform Panel;
    public static Image CloseImage;
    public const float MENU_WIDTH = 210f;
    // Second sidebar column's width when the active top-level category has
    // children. A bit wider than the first column (Menu) so the longest child
    // labels ("Keyboard Chatter Blocker") don't clip against the column mask.
    public const float SUBMENU_WIDTH = 260f;
    private const float TOP_BAR_HEIGHT = 60f;
    public static RectTransform Menu;
    public static RectTransform MenuContent;
    public static RectTransform SubMenu;
    public static RectTransform SubMenuContent;
    private static RectTransform Page;

    // Bottom Band — a strip under the main page content hosting ContextPane/
    // LivePreviewPane. A genuine child of MenuPanel (like Menu/SubMenu/Page),
    // not a floating canvas-level panel: it shrinks Page's own height from
    // below the same way Menu/SubMenu shrink its width from the left, via
    // plain anchors — no follow-script needed.
    public static RectTransform BottomBand;
    public static RectTransform BottomBandContent;
    private const float BandMinHeight = 160f;
    private const float BandMaxHeight = 500f;
    private const float DefaultBandHeight = 260f;
    private const float MinPageHeight = 200f;
    private static bool subMenuHasChildren;
    // bandHeight = the user's configured/desired band height (drag + config).
    // bandShown = its live, animated height: 0 while both panes are empty
    // (the band is hidden), tweening up to bandHeight when content arrives.
    // Page's bottom inset and the shell layout follow bandShown, not bandHeight.
    private static float bandHeight;
    private static float bandShown;
    private static CanvasGroup bandCanvasGroup;
    private static GTween bandSeq;

    // The flat white outline strips (submenu column edges, the top rule, the
    // bottom band edge) whose thickness the Outline Width setting drives live.
    // horizontal = thickness is the y size (top/bottom rule); else the x size
    // (left/right column edge). Repopulated on every panel (re)build.
    private static readonly List<(RectTransform rect, bool horizontal)> outlineStrips = [];
    // The panel's sprite-based outer border ring; its stroke also follows the
    // setting (regenerated, since it's a procedural ring sprite not a strip).
    private static Image borderImage;
    private static CanvasGroup menuCanvasGroup;
    private static CanvasGroup subMenuCanvasGroup;
    private static CanvasGroup panelCanvasGroup;
    private static CanvasGroup exitReorganizeCanvasGroup;
    private static GTween reorganizeSeq;
    private static GameObject exitReorganizeObj;

    public static float PanelScale {
        get;
        set {
            field = value;
            canvasScaler.referenceResolution =
                new Vector2(ReferenceResolution.x, ReferenceResolution.y) / field;
        }
    } = 1f;

    private static void CreatePanel() {
        // Outline elements are rebuilt below; drop the previous build's refs.
        outlineStrips.Clear();
        borderImage = null;

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
        // anchoredPosition (centered = zero here), NOT .position. Every consumer of
        // LastPanelPosition feeds it back as an anchoredPosition (Open()'s
        // GTAnchorPos, Close() at L922, ResetScalePosition), so it must be captured
        // as one. .position is the pivot's world/screen-pixel coord — and worse,
        // it's read while the canvas is still SetActive(false) (L58), so its value
        // is layout-state-dependent: ~zero on one machine (panel opens centered,
        // looks fine) but ~(screenW/2, screenH/2) on another, which then animates
        // the panel into the top-right corner, mostly off-screen ("pressed Alt+K,
        // nothing happens"). It does not self-correct — Close() saves the corner
        // back — and position isn't persisted, so every launch re-rolls the bug.
        // That machine-dependence is why it hit a Windows user but not local macOS.
        // anchoredPosition is deterministically zero here: panel opens centered.
        LastPanelPosition = Panel.anchoredPosition;

        panel.AddComponent<RectMask2D>();
        panelCanvasGroup = panel.AddComponent<CanvasGroup>();

        // (Border ring is added at the very end of CreatePanel so it draws on
        // top of the menu and top bar — see the "Border" block below. We avoid
        // the uGUI Outline component on purpose: it duplicates the whole panel
        // mesh recolored solid white, which bleeds through the interior during
        // a CanvasGroup fade and washes the panel white.)

        {
            // Menu Panel
            GameObject menuPanel = new("MenuPanel");
            menuPanel.transform.SetParent(panel.transform, false);

            var menuPanelRect = menuPanel.AddComponent<RectTransform>();
            menuPanelRect.anchorMin = Vector2.zero;
            menuPanelRect.anchorMax = new(1, 1);
            menuPanelRect.pivot = new(0.5f, 0.5f);
            menuPanelRect.anchoredPosition = Vector2.zero;
            menuPanelRect.offsetMin = new(1, 1);
            menuPanelRect.offsetMax = new(-1, -1);
            menuPanelRect.sizeDelta = Vector2.zero;

            // Mask
            var maskImage = menuPanel.AddComponent<Image>();
            maskImage.color = Color.white;
            maskImage.type = Image.Type.Sliced;
            maskImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
            maskImage.raycastTarget = false;

            var mask = menuPanel.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            Page = PageFactory.CreatePages(menuPanel);
            CreateBottomBand(menuPanel.transform);
            CreateSubMenu(menuPanel.transform);

            // Menu
            GameObject menu = new("Menu");
            menu.transform.SetParent(menuPanel.transform, false);

            Menu = menu.AddComponent<RectTransform>();
            Menu.anchorMin = Vector2.zero;
            Menu.anchorMax = new(0, 1);
            Menu.pivot = new(0, 0.5f);

            Menu.sizeDelta = new(MENU_WIDTH, -TOP_BAR_HEIGHT);
            Menu.anchoredPosition = MenuOpenPosition;

            var image = menu.AddComponent<Image>();
            image.color = UIColors.MenuBG;

            menuCanvasGroup = Menu.gameObject.AddComponent<CanvasGroup>();
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
            isMenuOpen = true;

            // Menu Content
            GameObject content = new("Content");
            content.transform.SetParent(Menu, false);

            MenuContent = content.AddComponent<RectTransform>();
            MenuContent.anchorMin = new(0, 1);
            MenuContent.anchorMax = new(1, 1);
            MenuContent.pivot = new(0.5f, 1);

            MenuContent.offsetMin = Vector2.zero;
            MenuContent.offsetMax = Vector2.zero;

            // Layout
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

            MenuFactory.CreateMenu(MenuContent);

            GameObject power = new("Power");
            power.transform.SetParent(Menu, false);
            var powerRect = power.AddComponent<RectTransform>();
            powerRect.anchorMin = new Vector2(0f, 0f);
            powerRect.anchorMax = new Vector2(1f, 0f);
            powerRect.offsetMin = Vector2.zero;
            powerRect.offsetMax = Vector2.zero;
            powerRect.sizeDelta = new Vector2(0f, 60f);
            powerRect.pivot = new Vector2(0.5f, 0f);
            var powerBg = power.AddComponent<Image>();
            powerBg.color = MainCore.Conf.Active
                    ? new(0, 0, 0, 0.1f)
                    : UIColors.SoftRed;
            var btn = power.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            GTween powerSeq = null;
            btn.onClick.AddListener(() => {
                bool enable = MainCore.Conf.Active = !MainCore.Conf.Active;
                MainCore.SetModEnabled(enable);

                Color target = enable
                    ? new Color(0f, 0f, 0f, 0.1f)
                    : UIColors.SoftRed;

                powerSeq?.Kill();
                powerSeq = GTweenSequenceBuilder.New()
                    .Append(powerBg.GTColor(target, 0.32f).SetEasing(Easing.OutExpo))
                    .Build();

                MainCore.TC.Play(powerSeq);
            });
            GameObject powerIcon = new("PowerIcon");
            powerIcon.transform.SetParent(powerRect, false);
            RectTransform powerIconRect = powerIcon.AddComponent<RectTransform>();
            powerIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            powerIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            powerIconRect.pivot = new Vector2(0.5f, 0.5f);
            powerIconRect.sizeDelta = new Vector2(26f, 26f);
            Image powerIconImage = powerIcon.AddComponent<Image>();
            powerIconImage.sprite = MainCore.Spr.Get(UISprite.Power128);
            powerIconImage.color = new(1f, 1f, 1f, 0.6f);

            GameObject version = new("Version");
            version.transform.SetParent(Menu, false);
            var versionRect = version.AddComponent<RectTransform>();
            versionRect.anchorMin = Vector2.zero;
            versionRect.anchorMax = new(1f, 0f);
            // Bottom-anchored, so height comes from the offsets. offsetMax.y was 0
            // -> a zero-height box, which pushed the glyphs below the Menu's bottom
            // edge and clipped them. Give it an 18px tall strip 2px off the bottom.
            versionRect.offsetMin = new(2f, 2f);
            versionRect.offsetMax = new(0f, 20f);
            versionRect.pivot = Vector2.zero;
            var versionText = version.AddComponent<TextMeshProUGUI>();
            versionText.text = $"v{Info.DisplayVersion}";
            versionText.font = FontManager.Current;
            versionText.fontSize = 12f;
            versionText.color = new Color(1f, 1f, 1f, 0.4f);
            versionText.characterSpacing = -3f;
            versionText.alignment = TextAlignmentOptions.BottomLeft;
        }

        // Top Bar
        GameObject topBar = new("TopBar");
        topBar.transform.SetParent(panel.transform, false);
        topBar.AddComponent<DragHandler>();

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
            // Logo
            GameObject logo = new("Logo");
            logo.transform.SetParent(topBar.transform, false);

            var logoImage = logo.AddComponent<Image>();
            logoImage.sprite = MainCore.Spr.Get(UISprite.QuartzLogo);
            logoImage.preserveAspect = true;
            // The logo isn't a theme color — don't let accent remapping touch it.
            logo.AddComponent<ThemeExempt>();

            var logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = new(0, 0.5f);
            logoRect.anchorMax = new(0, 0.5f);
            logoRect.pivot = new(0, 0.5f);
            logoRect.anchoredPosition = new(14, 0);
            logoRect.sizeDelta = new(46f, 46f);

            var btn = logo.AddComponent<NonRaycastButton>();
            btn.onClick += ToggleMenu;
        }

        {
            // Root button
            GameObject close = new("Close");
            close.transform.SetParent(topBar.transform, false);

            var closeRect = close.AddComponent<RectTransform>();
            closeRect.anchorMin = new(1, 0.5f);
            closeRect.anchorMax = new(1, 0.5f);
            closeRect.pivot = new(1, 0.5f);
            closeRect.anchoredPosition = new(-16, 0);
            closeRect.sizeDelta = new(38, 38);

            // Click handled via PointerUp on the EventTrigger below — a Button
            // here would get its click cancelled by the trigger's drag
            // handling whenever the cursor moves mid-click.

            // Background circle (hover layer)
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

            // X icon (always visible)
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

            UnityUtils.AddClickEvent(trigger, _ => Close());

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

        // Full-width white rule directly under the top bar — the SubMenu
        // column's top outline, extended across the whole window (its
        // left/right/bottom edges stay on the column itself). A panel child so
        // it spans the full width and draws over the menu/submenu/page; added
        // before the border ring so the ring still frames the corners. y sits
        // at the content top (panel top - top bar height), which is the
        // SubMenu's top edge, so the column's side outlines meet it flush.
        {
            GameObject topRule = new("SubMenuTopRule");
            topRule.transform.SetParent(panel.transform, false);

            RectTransform ruleRect = topRule.AddComponent<RectTransform>();
            ruleRect.anchorMin = new(0, 1);
            ruleRect.anchorMax = new(1, 1);
            ruleRect.pivot = new(0.5f, 1);
            ruleRect.anchoredPosition = new(0, -TOP_BAR_HEIGHT);
            ruleRect.sizeDelta = new(0, MainCore.Conf.OutlineWidth);

            Image ruleImg = topRule.AddComponent<Image>();
            ruleImg.color = Color.white;
            ruleImg.raycastTarget = false;
            outlineStrips.Add((ruleRect, true));
        }

        // Border ring — last child so it frames over the menu and top bar.
        // Replaces the old white uGUI Outline that washed the panel white on
        // fade (see note where the CanvasGroup is added).
        {
            GameObject border = new("Border");
            border.transform.SetParent(panel.transform, false);

            RectTransform borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            Image borderImg = border.AddComponent<Image>();
            // Procedural ring so the stroke can follow the Outline Width
            // setting; 12.5 corner units matches the panel's rounded corners
            // (the same radius CircleOutline256P1024 baked in).
            borderImg.sprite = MainCore.Spr.GetRing(12.5f, BorderStroke(MainCore.Conf.OutlineWidth));
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.white;
            borderImg.raycastTarget = false;
            borderImage = borderImg;
        }
    }

    // The border ring's stroke can't exceed its 12.5-unit corner radius or the
    // sliced ring self-overlaps at the corners; the flat edge strips take the
    // raw width. Floor at 1 so it never vanishes.
    private static float BorderStroke(float outlineWidth) => Mathf.Clamp(outlineWidth, 1f, 12.5f);

    // Live-applies the Outline Width setting. Flat strips resize instantly
    // (cheap); the border ring sprite is regenerated only when asked
    // (regenBorder), since each distinct stroke builds a new procedural
    // texture — do that on the slider's release, not every drag frame.
    public static void SetOutlineWidth(float width, bool regenBorder) {
        for(int i = 0; i < outlineStrips.Count; i++) {
            (RectTransform rect, bool horizontal) = outlineStrips[i];
            if(rect == null) continue;
            rect.sizeDelta = horizontal
                ? new Vector2(rect.sizeDelta.x, width)
                : new Vector2(width, rect.sizeDelta.y);
        }

        if(regenBorder && borderImage != null)
            borderImage.sprite = MainCore.Spr.GetRing(12.5f, BorderStroke(width));
    }

    // Second sidebar column: reveals the active top-level category's children
    // (currently only the Overlay group's 7 pages) at a fixed width. Width is
    // tweened by ApplyShellLayout, but SubMenuContent itself stays fixed-width
    // so row text never reflows mid-tween — the tween is a pure reveal wipe,
    // masked by the RectMask2D below (same trick as the old collapsible
    // sidebar group, rotated 90 degrees).
    private static void CreateSubMenu(Transform parent) {
        GameObject subMenu = new("SubMenu");
        subMenu.transform.SetParent(parent, false);

        SubMenu = subMenu.AddComponent<RectTransform>();
        SubMenu.anchorMin = new(0, 0);
        SubMenu.anchorMax = new(0, 1);
        SubMenu.pivot = new(0, 0.5f);
        SubMenu.sizeDelta = new(0f, -TOP_BAR_HEIGHT);
        SubMenu.anchoredPosition = new(MENU_WIDTH, -TOP_BAR_HEIGHT * 0.5f);

        // TopBar shade (a darker, less accent-tinted palette role than the
        // Menu's MenuBG) so the secondary column reads as its own recessed
        // surface instead of blending into the primary tabs. Still a palette
        // color, so RefreshTheme's accent remap keeps working.
        var image = subMenu.AddComponent<Image>();
        image.color = UIColors.TopBar;

        // Hairline on the left edge crisps the seam against Menu — still useful
        // now that the fill differs, and it stays a palette-safe white alpha.
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

        // White outline around the column — four crisp edge strips rather than
        // a sliced outline sprite (those are rounded and would gap at the
        // column's square top/bottom corners). Created AFTER Content below so
        // they're the last siblings and draw on TOP of the sidebar rows (like
        // the panel's own border ring is added last) — otherwise the selected
        // row and row backgrounds cover the top/edge strips and the outline
        // looks cut off. Row text is inset well past the strip, so drawing over
        // the row edge only paints the border, not the label. Still children of
        // SubMenu, so the RectMask2D keeps them inside the column and the width
        // tween wipes the right edge in with the reveal.
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
            // horizontal strip (top/bottom) sets its y size; vertical its x.
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

        // Outline last, so it renders over the rows (see OutlineEdge note).
        // Width matches the panel's outer border-ring stroke: that ring is
        // CircleOutline(cornerRadius, cornerRadius/2), i.e. half the 12.5-unit
        // corner radius = 6.25 units, so the column outline reads as thick as
        // the window's. The TOP edge isn't drawn here — it's a full-width rule
        // under the top bar (CreatePanel's SubMenuTopRule), so it spans the
        // whole window rather than just this column.
        float outline = MainCore.Conf.OutlineWidth;
        OutlineEdge("OutlineBottom", new(0, 0), new(1, 0), new(0.5f, 0f), new(0f, outline));
        OutlineEdge("OutlineLeft", new(0, 0), new(0, 1), new(0f, 0.5f), new(outline, 0f));
        OutlineEdge("OutlineRight", new(1, 0), new(1, 1), new(1f, 0.5f), new(outline, 0f));
    }

    // Bottom Band — a strip under the main page content hosting ContextPane/
    // LivePreviewPane. A genuine child of MenuPanel, shrinking Page's height
    // from below the same way Menu/SubMenu shrink its width from the left.
    private static void CreateBottomBand(Transform parent) {
        bandHeight = ClampBand(MainCore.Conf.ContextBandHeight > 0f ? MainCore.Conf.ContextBandHeight : DefaultBandHeight);

        GameObject band = new("BottomBand");
        band.transform.SetParent(parent, false);

        BottomBand = band.AddComponent<RectTransform>();
        BottomBand.anchorMin = new(0, 0);
        BottomBand.anchorMax = new(1, 0);
        BottomBand.pivot = new(0.5f, 0f);
        // Initial left inset is provisional — the first ApplyShellLayout call
        // (triggered from MenuFactory.CreateMenu, later in the same build)
        // corrects it the same frame.
        BottomBand.offsetMin = new(MENU_WIDTH, 0f);
        BottomBand.offsetMax = new(0f, bandHeight);

        // TopBar shade (matching SubMenu) so the band reads as its own recessed
        // surface, distinct from the darker Page content above it.
        var image = band.AddComponent<Image>();
        image.color = UIColors.TopBar;
        band.AddComponent<RectMask2D>();
        // Faded together with the height when the band reveals/hides.
        bandCanvasGroup = band.AddComponent<CanvasGroup>();

        // White outline along the band's top edge (the seam against Page) —
        // 6.25px to match the submenu column outline and the panel border
        // stroke, instead of the old faint 1px hairline.
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

        // Live Preview Pane — fixed-height strip; collapses entirely
        // (SetActive) when empty so it takes no layout space at all. Its own
        // card background (distinct from the band's base fill) plus a hairline
        // divider at its own bottom edge read it as a separate space from
        // Context Pane below, rather than one continuous surface — both only
        // visible while this pane has content, since they're its own children.
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

        // Context Pane — scrollable (reuses the same pad/viewport/content
        // machinery every other page uses), fills whatever height Live
        // Preview doesn't take.
        GameObject contextPane = new("ContextPane");
        contextPane.transform.SetParent(content.transform, false);
        RectTransform contextPaneRect = contextPane.AddComponent<RectTransform>();
        var contextPaneLayout = contextPane.AddComponent<LayoutElement>();
        contextPaneLayout.minHeight = 0f;
        contextPaneLayout.flexibleHeight = 1f;
        RectTransform contextContentRect = PageFactory.CreateScrollablePage(contextPaneRect);

        ContextPane.Attach(contextPaneRect, contextContentRect);
        LivePreviewPane.Attach(livePreviewRect, liveCardRect);

        // Divider strip on the band's top edge — dragging up grows it,
        // dragging down shrinks it, since the band's bottom edge is pinned to
        // Panel and can't move.
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
        // Bypasses the shell tween entirely — writes BottomBand.sizeDelta.y
        // (via PaneDivider itself) and Page.offsetMin.y directly every drag
        // frame, so Page yields height live instead of visibly overlapping
        // the band until pointer-up.
        paneDivider.OnResized = h => {
            bandHeight = ClampBand(h);
            // The band is open while it's being dragged, so the live height
            // tracks the configured one (keeps the shell layout in sync).
            bandShown = bandHeight;
            if(!Mathf.Approximately(bandHeight, h)) BottomBand.sizeDelta = new Vector2(BottomBand.sizeDelta.x, bandHeight);
            Page.offsetMin = new Vector2(Page.offsetMin.x, bandHeight);
        };
        paneDivider.OnResizeEnd = _ => {
            MainCore.Conf.ContextBandHeight = bandHeight;
            MainCore.ConfMgr.RequestSave();
        };

        // Panes start empty, so the band starts collapsed (hidden) — it reveals
        // itself, animated, the first time a page pushes pane content.
        RefreshBand(false);
    }

    // Reveals or hides the bottom band depending on whether either pane has
    // content, animating its height (and fade) between 0 and the configured
    // height. Page's bottom inset follows via bandShown. Called on every pane
    // content change (PaneState.Changed) and once at build.
    public static void RefreshBand(bool animate) {
        if(BottomBand == null || Page == null) return;

        bool open = ContextPane.HasContent || LivePreviewPane.HasContent;
        float target = open ? ClampBand(bandHeight) : 0f;

        bandSeq?.Kill();

        // Must be active to be seen growing in; deactivated only after it has
        // fully collapsed (below), so it takes no layout/raycast space when hidden.
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

    // Writes the live band height everywhere that depends on it: the band's own
    // size, Page's bottom inset, and the band fade.
    private static void ApplyBandHeight(float h) {
        bandShown = h;
        if(BottomBand != null) BottomBand.sizeDelta = new Vector2(BottomBand.sizeDelta.x, h);
        if(Page != null) Page.offsetMin = new Vector2(Page.offsetMin.x, h);
        if(bandCanvasGroup != null) {
            float full = Mathf.Max(1f, ClampBand(bandHeight));
            bandCanvasGroup.alpha = Mathf.Clamp01(h / full);
        }
    }

    // Clamps a candidate band height to its declared min/max and to whatever
    // vertical space the panel can actually spare (top bar + a minimum page
    // height), so the band can never squeeze Page below a usable size.
    private static float ClampBand(float h) =>
        Mathf.Clamp(h, BandMinHeight, Mathf.Min(BandMaxHeight, Panel.sizeDelta.y - 2f - TOP_BAR_HEIGHT - MinPageHeight));

    private static GTween shellSeq;

    // Single writer for all shell geometry (Menu/SubMenu/Page/BottomBand
    // insets + fades) — prevents a menu-open/close tween, a submenu-visibility
    // change, and the initial build from fighting each other with disjoint
    // partial writes to the same RectTransforms.
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

        // Interactable/raycast flags flip immediately, not tweened.
        menuCanvasGroup.interactable = isMenuOpen;
        menuCanvasGroup.blocksRaycasts = isMenuOpen;
        subMenuCanvasGroup.interactable = isMenuOpen && subMenuHasChildren;
        subMenuCanvasGroup.blocksRaycasts = isMenuOpen && subMenuHasChildren;

        shellSeq = GTweenSequenceBuilder.New()
            .Join(Menu.GTAnchorPos(menuTarget, duration).SetEasing(Easing.OutExpo))
            .Join(SubMenu.GTAnchorPosX(subMenuX, duration).SetEasing(Easing.OutExpo))
            .Join(SubMenu.GTSizeDelta(new Vector2(subMenuW, SubMenu.sizeDelta.y), duration).SetEasing(Easing.OutExpo))
            .Join(Page.GTOffsetMin(new Vector2(leftInset, bandShown), duration).SetEasing(Easing.OutExpo))
            .Join(BottomBand.GTOffsetMin(new Vector2(leftInset, 0f), duration).SetEasing(Easing.OutExpo))
            .Join(menuCanvasGroup.GTFade(menuAlpha, Mathf.Min(duration, 0.3f)).SetEasing(Easing.OutSine))
            .Join(subMenuCanvasGroup.GTFade(subMenuAlpha, Mathf.Min(duration, 0.3f)).SetEasing(Easing.OutSine))
            // Self-heal: re-snap every target from *current* state, in case
            // state changed again mid-tween (rapid open/close/category clicks).
            .AppendCallback(SnapShellLayout)
            .Build();
        MainCore.TC.Play(shellSeq);
    }

    // Re-derives every shell target from current state and writes it directly
    // (no tween). Used for the non-animated build/rebuild path and as the
    // animated sequence's completion callback.
    private static void SnapShellLayout() {
        Vector2 menuTarget = isMenuOpen ? MenuOpenPosition : MenuClosedPosition;
        float subMenuX = isMenuOpen ? MENU_WIDTH : -SUBMENU_WIDTH;
        float subMenuW = subMenuHasChildren ? SUBMENU_WIDTH : 0f;
        float leftInset = isMenuOpen ? MENU_WIDTH + (subMenuHasChildren ? SUBMENU_WIDTH : 0f) : 0f;

        Menu.anchoredPosition = menuTarget;
        SubMenu.anchoredPosition = new Vector2(subMenuX, SubMenu.anchoredPosition.y);
        SubMenu.sizeDelta = new Vector2(subMenuW, SubMenu.sizeDelta.y);
        Page.offsetMin = new Vector2(leftInset, bandShown);
        BottomBand.offsetMin = new Vector2(leftInset, 0f);
        menuCanvasGroup.alpha = isMenuOpen ? 1f : 0f;
        subMenuCanvasGroup.alpha = isMenuOpen && subMenuHasChildren ? 1f : 0f;
        menuCanvasGroup.interactable = isMenuOpen;
        menuCanvasGroup.blocksRaycasts = isMenuOpen;
        subMenuCanvasGroup.interactable = isMenuOpen && subMenuHasChildren;
        subMenuCanvasGroup.blocksRaycasts = isMenuOpen && subMenuHasChildren;
    }

    // MenuFactory owns column-2 content (which category is active, whether it
    // has children); UICore owns the geometry that reveals/hides it.
    public static void SetSubMenuVisible(bool hasChildren, bool animate) {
        subMenuHasChildren = hasChildren;
        ApplyShellLayout(animate, 0.22f);
    }

    private static float holdStartTime = 0f;
    private static bool holdingToggle = false;

    private static GTween panelTweener;
    private static GTween resetSequence;

    private static bool isOpen = false;
    public static bool IsOpen => isOpen;

    public static Vector2 LastPanelPosition;
    public static Vector2 LastPanelSize;

    public static Vector2 DefaultPanelSize => new(
        Mathf.Min(1280f / MainCore.Conf.UIScale, Screen.width / MainCore.Conf.UIScale),
        Mathf.Min(720f / MainCore.Conf.UIScale, Screen.height / MainCore.Conf.UIScale)
    );

    // The user-resized panel size saved from a previous session, clamped to the
    // current screen and the resize minimums; falls back to the default when
    // unset (first run) or if the screen shrank, so a restored panel can't land
    // off-screen or below a usable size.
    private static Vector2 LoadSavedPanelSize() {
        float w = MainCore.Conf.PanelWidth;
        float h = MainCore.Conf.PanelHeight;
        if(w <= 0f || h <= 0f) return DefaultPanelSize;

        float scale = MainCore.Conf.UIScale;
        float minW = ResizeHandle.MIN_WIDTH / scale;
        float minH = ResizeHandle.MIN_HEIGHT / scale;
        float maxW = Screen.width / scale;
        float maxH = Screen.height / scale;
        return new Vector2(
            Mathf.Clamp(w, minW, Mathf.Max(minW, maxW)),
            Mathf.Clamp(h, minH, Mathf.Max(minH, maxH))
        );
    }

    // Persists the current panel size so a resize is restored next launch. Called
    // when a resize-drag ends.
    public static void SavePanelSize() {
        if(Panel == null) return;

        LastPanelSize = Panel.sizeDelta;
        MainCore.Conf.PanelWidth = Panel.sizeDelta.x;
        MainCore.Conf.PanelHeight = Panel.sizeDelta.y;
        MainCore.ConfMgr.RequestSave();

        // A panel shrink can leave the band claiming more height than the
        // (now smaller) panel can spare. Accepted transient: during the
        // shrink drag itself the band can momentarily crowd the page; this
        // snaps it right at drag end.
        float clamped = ClampBand(bandHeight);
        if(!Mathf.Approximately(clamped, bandHeight)) {
            bandHeight = clamped;
            // Re-apply through the visibility state so a hidden (empty) band
            // isn't forced open by the re-clamp — only an already-open band
            // snaps to the new height.
            RefreshBand(false);
        }
    }

    public static void HandleUpdate() {
        if(canvasObj == null) return;

        Keybind.KeyModifier mod = (Keybind.KeyModifier)MainCore.Conf.ToggleModifier;
        KeyCode key = (KeyCode)MainCore.Conf.ToggleKey;
        bool modHeld = Keybind.ModifierHeld(mod);

        bool pressed = modHeld && Input.GetKey(key);

        // key down — suppressed while a settings keybind capture is listening,
        // so pressing keys to rebind doesn't also toggle the menu.
        if(!Keybind.Capturing && modHeld && Input.GetKeyDown(key)) {
            Toggle();

            holdStartTime = Time.unscaledTime;
            holdingToggle = true;
        }

        // hold reset
        if(holdingToggle && pressed && Time.unscaledTime - holdStartTime >= 0.4f) {
            ResetScalePosition(!isOpen);
            holdingToggle = false;
        }

        // key up
        if(Input.GetKeyUp(key)) holdingToggle = false;

        UIObject.TickAll();
        Tooltip.Tick();
    }

    private static Vector2 GetRandomOffscreenPosition() {
        float halfW = Screen.width * 0.5f;
        float halfH = Screen.height * 0.5f;

        int side = Random.Range(0, 4);

        return side switch {
            // Left
            0 => new(
                -halfW - Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),

            // Right
            1 => new(
                halfW + Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),

            // Top
            2 => new(
                Random.Range(-halfW, halfW),
                halfH + Panel.sizeDelta.y
            ),

            // Bottom
            _ => new(
                Random.Range(-halfW, halfW),
                -halfH - Panel.sizeDelta.y
            )
        };
    }

    public static void Open(bool noAnimate = false) {
        if(isOpen || Panel == null) return;

        isOpen = true;

        // Make sure a previous reorganize session left the panel fully visible.
        Panel.gameObject.SetActive(true);
        if(panelCanvasGroup != null) {
            panelCanvasGroup.alpha = Mathf.Clamp01(MainCore.Conf.PanelOpacity);
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        panelTweener.CompleteAndKill();
        resetSequence.CompleteAndKill();

        if(noAnimate) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;

            canvasObj.SetActive(true);
            RefreshAllText();
            return;
        }

        Vector2 startPos = GetRandomOffscreenPosition();

        Panel.anchoredPosition = startPos;
        Panel.sizeDelta = LastPanelSize;

        canvasObj.SetActive(true);
        RefreshAllText();

        panelTweener = Panel.GTAnchorPos(LastPanelPosition, 0.25f)
            .SetEasing(Easing.OutExpo);
        MainCore.TC.Play(panelTweener);

        if(firstRunHelperActivated) {
            firstRunHelperActivated = false;
            EndFirstRunHelper();
        }
    }

    // Repaints menu labels after the shared dynamic font atlas changes. The menu
    // and the mod's in-game overlays (combo, judgement, song title, …) all draw
    // with the same dynamic TMP font (FontManager.Current), so they share one
    // atlas. Those overlays keep populating it with new glyphs during play; when
    // TMP grows the atlas, menu labels generated earlier are left with stale glyph
    // geometry and the whole menu renders overlapped, clearing only on a restart.
    // Forcing a mesh update (so glyphs re-resolve against the current atlas) and a
    // layout rebuild (so rows re-flow off the corrected preferred sizes) on open
    // makes that corruption self-heal instead of persisting for the session.
    private static int refreshedFontId = -1;
    private static int refreshedCharacterCount = -1;
    private static int refreshedAtlasCount = -1;

    private static void RefreshAllText() {
        if(canvasObj == null) return;

        TMP_FontAsset font = FontManager.Current;
        int fontId = font != null ? font.GetInstanceID() : 0;
        int characterCount = font?.characterTable?.Count ?? 0;
        int atlasCount = font?.atlasTextures?.Length ?? 0;
        if(fontId == refreshedFontId
            && characterCount == refreshedCharacterCount
            && atlasCount == refreshedAtlasCount) {
            return;
        }
        TMP_Text[] texts = canvasObj.GetComponentsInChildren<TMP_Text>(true);
        for(int i = 0; i < texts.Length; i++) {
            // Every tab is always active (PageSwicher slides pages off-screen and
            // fades them rather than SetActive-toggling), so ignoreActiveState:false
            // still repaints all of them — the whole menu, not just the visible tab.
            // Off-screen pages keep laid-out rects, so geometry regenerates at the
            // right width.
            if(texts[i] != null) texts[i].ForceMeshUpdate(false, true);
        }

        if(Panel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(Panel);

        // ForceMeshUpdate can itself add missing menu glyphs to a dynamic atlas;
        // record the post-refresh signature to avoid one redundant refresh later.
        refreshedFontId = fontId;
        refreshedCharacterCount = font?.characterTable?.Count ?? 0;
        refreshedAtlasCount = font?.atlasTextures?.Length ?? 0;
    }

    public static void Close(bool noAnimate = false) {
        if(!isOpen) return;

        // Leaving reorganize restores the panel so the next open is clean.
        ExitReorganize();

        isOpen = false;

        LastPanelPosition = Panel.anchoredPosition;
        LastPanelSize = Panel.sizeDelta;

        CloseImage.color = new Color(
            CloseImage.color.r,
            CloseImage.color.g,
            CloseImage.color.b,
            0f
        );

        panelTweener.CompleteAndKill();
        resetSequence.CompleteAndKill();

        if(noAnimate) {
            canvasObj.SetActive(false);
            return;
        }

        Vector2 targetPos = GetRandomOffscreenPosition();

        panelTweener = Panel
            .GTAnchorPos(targetPos, 0.25f)
            .SetEasing(Easing.OutExpo)
            .OnComplete(() => canvasObj.SetActive(false));
        MainCore.TC.Play(panelTweener);
    }

    public static void Toggle(bool noAnimate = false) {
        if(isOpen) Close(noAnimate);
        else Open(noAnimate);
    }

    public static void ResetScalePosition(bool noAnimate = false) {
        Vector2 targetSize = DefaultPanelSize;

        LastPanelPosition = Vector2.zero;
        LastPanelSize = targetSize;

        // Reset clears the saved size too, so the next launch uses the default.
        MainCore.Conf.PanelWidth = 0f;
        MainCore.Conf.PanelHeight = 0f;
        MainCore.ConfMgr.RequestSave();

        panelTweener?.Kill();
        resetSequence?.Kill();

        if(noAnimate) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
            return;
        }

        resetSequence = GTweenSequenceBuilder.New()
            .Append(Panel.GTAnchorPos(LastPanelPosition, 0.26f).SetEasing(Easing.OutExpo))
            .Join(Panel.GTSizeDelta(LastPanelSize, 0.26f).SetEasing(Easing.OutExpo))
            .Build();

        MainCore.TC.Play(resetSequence);
    }

    private static bool isMenuOpen = false;
    private static Vector2 MenuOpenPosition => new(0f, -TOP_BAR_HEIGHT * 0.5f);
    private static Vector2 MenuClosedPosition => new(-MENU_WIDTH, -TOP_BAR_HEIGHT * 0.5f);

    public static void OpenMenu() {
        isMenuOpen = true;

        // Force a consistent slide-in distance even if a rapid double-toggle
        // caught Menu mid-close.
        Menu.anchoredPosition = MenuClosedPosition;
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;

        ApplyShellLayout(true, 0.6f);
    }

    public static void CloseMenu() {
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        isMenuOpen = false;

        ApplyShellLayout(true, 0.4f);
    }

    public static void ToggleMenu() {
        if(isMenuOpen) CloseMenu();
        else OpenMenu();
    }


    // Accent theming. Applies a new accent palette and recolors every already-built
    // Image in the canvas by remapping old palette colors -> new ones (white/icon
    // images aren't palette entries, so they pass through untouched).
    public static void SetAccentColor(Color accent, bool save) {
        UIColors.Palette previous = UIColors.Current;
        MainCore.Conf.SetAccentColor(accent);
        UIColors.ApplyAccent(MainCore.Conf.GetAccentColor());

        // Live preview while dragging the picker. This remaps existing colours
        // by matching the old palette, which is only approximate — for a near
        // gray/black accent several palette roles collapse to the same colour,
        // so the match can't tell them apart and the UI drifts toward one tone.
        RefreshTheme(previous);

        if(save) {
            MainCore.ConfMgr.Save();
            // On commit, rebuild the UI so every widget re-reads its colour
            // straight from the palette role — the only way to recover cleanly
            // after the lossy live remap (e.g. dragging through black). Deferred
            // so we don't tear down the colour picker mid-callback.
            MainThread.Enqueue(Rebuild);
        }
    }

    // Non-exempt Images collected by the last RefreshTheme scan. The full
    // GetComponentsInChildren sweep plus the per-image exempt-ancestor walk is
    // the expensive part of a live accent drag, and its result only changes
    // when the canvas is (re)built — every accent commit goes through
    // Rebuild/Dispose — or when a translation load-end regenerates dropdown
    // rows, so the cache is dropped at both of those points and rebuilt lazily
    // on the next drag event.
    private static List<Image> themeImages;

    private static void RefreshTheme(UIColors.Palette previous) {
        if(canvasObj != null) {
            if(themeImages == null) {
                themeImages = [];
                Image[] images = canvasObj.GetComponentsInChildren<Image>(true);
                for(int i = 0; i < images.Length; i++) {
                    Image img = images[i];
                    if(img == null) continue;
                    // Data-colored images (colour swatches/previews, the logo) opt
                    // out of accent remapping — their colour isn't a theme colour
                    // and must not be hijacked just because it matches a palette
                    // entry. Walk parents by hand (active-agnostic, and avoids the
                    // newer GetComponentInParent(bool) overload).
                    if(IsThemeExempt(img.transform)) continue;
                    themeImages.Add(img);
                }
            }

            for(int i = 0; i < themeImages.Count; i++) {
                Image img = themeImages[i];
                if(img == null) continue;
                img.color = RemapThemeColor(img.color, previous);
            }
        }

        MenuFactory.RefreshTheme();
    }

    private static bool IsThemeExempt(Transform t) {
        for(; t != null; t = t.parent)
            if(t.GetComponent<ThemeExempt>() != null) return true;
        return false;
    }

    private static Color RemapThemeColor(Color color, UIColors.Palette previous) {
        Color next;
        if(TryRemapRgb(color, previous.ObjectActive, UIColors.ObjectActive, out next)) return next;
        if(TryRemapRgb(color, previous.ObjectActiveBright, UIColors.ObjectActiveBright, out next)) return next;
        if(TryRemapRgb(color, previous.ObjectActiveLightBright, UIColors.ObjectActiveLightBright, out next)) return next;
        if(TryRemapRgb(color, previous.ObjectButton, UIColors.ObjectButton, out next)) return next;
        if(TryRemapRgb(color, previous.ObjectBG, UIColors.ObjectBG, out next)) return next;
        if(TryRemapRgb(color, previous.MenuSelected, UIColors.MenuSelected, out next)) return next;
        if(TryRemapRgb(color, previous.MenuHighlight, UIColors.MenuHighlight, out next)) return next;
        if(TryRemapRgb(color, previous.MenuHover, UIColors.MenuHover, out next)) return next;
        if(TryRemapRgb(color, previous.MenuNormal, UIColors.MenuNormal, out next)) return next;
        if(TryRemapRgb(color, previous.MenuBG, UIColors.MenuBG, out next)) return next;
        if(TryRemapRgb(color, previous.TopBar, UIColors.TopBar, out next)) return next;
        if(TryRemapRgb(color, previous.PanelBG, UIColors.PanelBG, out next)) return next;
        return color;
    }

    private static bool TryRemapRgb(Color color, Color from, Color to, out Color result) {
        const float tolerance = 0.018f;
        if(Mathf.Abs(color.r - from.r) <= tolerance
            && Mathf.Abs(color.g - from.g) <= tolerance
            && Mathf.Abs(color.b - from.b) <= tolerance) {
            result = new Color(to.r, to.g, to.b, color.a);
            return true;
        }

        result = color;
        return false;
    }

    // Full teardown + rebuild of the settings UI. Used after a profile switch:
    // every widget bakes its value in at Create time, so the only way to show
    // the freshly loaded settings everywhere is to build the panel again.
    public static void Rebuild() {
        bool wasOpen = isOpen;

        if(wasOpen) Close(true);

        // CreatePanel re-seeds these from the new panel, so keep the user's
        // placement across the rebuild.
        Vector2 position = LastPanelPosition;
        Vector2 size = LastPanelSize;

        Dispose();
        Initialize();

        LastPanelPosition = position;
        LastPanelSize = size;

        if(wasOpen) Open(true);

        // Initialize may have opened the panel itself (ShowOnStartup) before
        // the placement was restored — pin it either way.
        if(isOpen) {
            Panel.anchoredPosition = position;
            Panel.sizeDelta = size;
        }
    }

    public static void Dispose() {
        MainCore.Tr.OnLoadEnd -= _onPageSettings;
        MainCore.Tr.OnLoadEnd -= _onRefresh;
        MenuFactory.OnStateChanged -= _onDockTabChanged;
        Panes.PaneState.Changed -= _onPaneChanged;
        bandSeq?.Kill();
        themeImages = null;
        UIObject.DisposeAll();
        Reorganizer.Dispose();
        UpdateToast.Dispose();
        Tooltip.Dispose();

        // Drop every in-flight tween before destroying the canvas. Widget
        // tweens (block fades, button hover/rest colors, panel moves) capture
        // components that are about to be destroyed; a tween that ticks or
        // completes onto a destroyed object throws every frame. Only runs on
        // teardown (full shutdown or a profile-switch rebuild), so it can't
        // interrupt a live gameplay overlay animation in practice.
        MainCore.TC.Clear();

        FontManager.MenuRoot = null;
        UnityEngine.Object.Destroy(canvasObj);
        canvasObj = null;
    }
}
