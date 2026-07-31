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
    private static GameObject canvasObj;
    private static Canvas canvas;
    private static CanvasScaler canvasScaler;
    public static readonly Dictionary<int, RectTransform> Pages = [];
    public static int CurrentMenuState = -1;
    public static bool IsReorganizing { get; private set; }
    public static readonly Vector2 ReferenceResolution = new(1920, 1080);
    private static Action<TranslationFailState> _onPageSettings;
    private static Action<TranslationFailState> _onRefresh;
    private static Action<string> _onLanguageRebuild;
    private static Action<int> _onDockTabChanged;
    private static Action _onPaneChanged;
    private static bool rebuildQueued;
    public static void Initialize() {
        canvasObj = new GameObject("QuartzUICanvas");
        canvasObj.transform.SetParent(MainCore.Root.transform, false);
        canvasObj.SetActive(false);
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
                themeImages = null;
                TextLocalization.RefreshAll();
                RequestPagesRebuild();
            }
        };
        _onLanguageRebuild = _ => RequestPagesRebuild();
        MainCore.Tr.OnLoadEnd += _onPageSettings;
        MainCore.Tr.OnLoadEnd += _onRefresh;
        MainCore.Tr.OnLanguageChanged += _onLanguageRebuild;
        _onDockTabChanged = _ => {
            ContextPane.Clear();
            LivePreviewPane.Clear();
        };
        MenuFactory.OnStateChanged += _onDockTabChanged;
        _onPaneChanged = () => RefreshBand(true);
        Panes.PaneState.Changed += _onPaneChanged;
        TextLocalization.RefreshAll();
        FontManager.ApplyMenuFont();
        if(MainCore.Conf.IsFirstRun) MakeFirstRunHelper();
        if(MainCore.Conf.ShowOnStartup) Open(true);
    }
    public static void SetPanelOpacity(float value, bool save) {
        MainCore.Conf.PanelOpacity = Mathf.Clamp01(value);
        if(panelCanvasGroup != null && !IsReorganizing) panelCanvasGroup.alpha = MainCore.Conf.PanelOpacity;
        if(save) MainCore.ConfMgr.RequestSave();
    }
    // Deferred one pump so the rebuild never tears down UI objects whose own
    // OnLanguageChanged/OnLoadEnd handlers are still pending in the same event
    // snapshot (a destroyed dropdown's refresh closure would throw on its dead TMP).
    private static void RequestPagesRebuild() {
        if(rebuildQueued) return;
        rebuildQueued = true;
        MainThread.Enqueue(() => {
            rebuildQueued = false;
            if(Panel == null) return;
            Rebuild();
        });
    }
    public static void Rebuild() {
        bool wasOpen = isOpen;
        if(wasOpen) Close(true);
        Vector2 position = LastPanelPosition;
        Vector2 size = LastPanelSize;
        Dispose();
        Initialize();
        LastPanelPosition = position;
        LastPanelSize = size;
        if(wasOpen) Open(true);
        else if(isOpen) Close(true);
        if(isOpen) {
            Panel.anchoredPosition = position;
            Panel.sizeDelta = size;
        }
    }
    public static void Dispose() {
        MainCore.Tr.OnLoadEnd -= _onPageSettings;
        MainCore.Tr.OnLoadEnd -= _onRefresh;
        MainCore.Tr.OnLanguageChanged -= _onLanguageRebuild;
        MenuFactory.OnStateChanged -= _onDockTabChanged;
        Panes.PaneState.Changed -= _onPaneChanged;
        bandSeq?.Kill();
        themeImages = null;
        UIObject.DisposeAll();
        ToggleBinds.ClearLive();
        Reorganizer.Dispose();
        UpdateToast.Dispose();
        Tooltip.Dispose();
        MainCore.TC.Clear();
        FontManager.MenuRoot = null;
        UnityEngine.Object.Destroy(canvasObj);
        canvasObj = null;
    }
}
