using Quartz.Async;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.FacetUi;
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
    public static GameObject CanvasObj => canvasObj;
    public static Canvas Canvas => canvas;
    public static RectTransform CanvasRect => canvasObj == null ? null : canvasObj.GetComponent<RectTransform>();
    private static CanvasScaler canvasScaler;
    public static readonly Dictionary<int, RectTransform> Pages = [];
    public static int CurrentMenuState = -1;
    public static bool IsReorganizing { get; private set; }
    public static readonly Vector2 ReferenceResolution = new(1920, 1080);
    public static bool TryGetCanvasSize(out Vector2 size) {
        float scale = MainCore.Conf.UIScale <= 0f ? 1f : MainCore.Conf.UIScale;
        Vector2 reference = ReferenceResolution / scale;
        int w = Screen.width;
        int h = Screen.height;
        if(w <= 0 || h <= 0 || reference.x <= 0f || reference.y <= 0f) {
            size = reference;
            return false;
        }
        float match = canvasScaler == null ? 0.5f : canvasScaler.matchWidthOrHeight;
        float factor = Mathf.Pow(w / reference.x, 1f - match) * Mathf.Pow(h / reference.y, match);
        if(factor <= 0.0001f) {
            size = reference;
            return false;
        }
        size = new Vector2(w / factor, h / factor);
        return true;
    }
    public static Vector2 CanvasSize {
        get {
            TryGetCanvasSize(out Vector2 size);
            return size;
        }
    }
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
        canvas = canvasObj.AddComponent<Canvas>();
        // After the Canvas: adding it swaps the Transform for a RectTransform, which would leave a stale (Unity-null) root.
        FontManager.MenuRoot = canvasObj.transform;
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
        CreateReorganizeHub();
        _onPageSettings = state => {
            if(state == TranslationFailState.Success) PageSettings.OnTranslatorLoadEnd();
        };
        _onRefresh = state => {
            if(state == TranslationFailState.Success) {
                themeImages = null;
                themeTexts = null;
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
        // Rebuild runs this too: never over the new menu, which Open would hide.
        if(MainCore.Conf.ShowOnStartup && !FacetBridge.NewMenuVisible()) {
            // The game's start shows the new menu when the switch is on: a pump later, once the runtime is up. A rebuild never opens it.
            if(!startedOnce && NewMenuWanted) MainThread.Enqueue(() => { FacetBridge.OpenNewMenu(); if(!FacetBridge.NewMenuVisible()) Open(true); });
            else Open(true);
        }
        startedOnce = true;
    }
    private static bool startedOnce;
    public static void SetPanelOpacity(float value, bool save) {
        MainCore.Conf.PanelOpacity = Mathf.Clamp01(value);
        if(panelCanvasGroup != null && (!IsReorganizing || panelOverHub)) panelCanvasGroup.alpha = MainCore.Conf.PanelOpacity;
        if(save) MainCore.ConfMgr.RequestSave();
    }
#if DEBUG
    internal static int NewMenuSwitchCalls;   // self-test hook: Prism's row must come through here
#endif
    /// <summary>
    /// Every row that writes the new-menu switch goes through here: saved, the new menu built ahead when on (so its first
    /// open only shows it), and the classic pages rebuilt, since Effect Remover's cached page shows its button only with it on.
    /// </summary>
    internal static void SetNewMenuSwitch(bool on) {
#if DEBUG
        NewMenuSwitchCalls++;
#endif
        MainCore.Conf.ExperimentalNewMenu = on;
        MainCore.ConfMgr.RequestSave();
        // Switched off under a classic menu opened from the new one: its close has nothing to go back to.
        if(!on && MenuHandOff.ReturnTo == MenuHandOff.Menu.New) MenuHandOff.ReturnTo = MenuHandOff.Menu.None;
        if(on) FacetBridge.PrebuildNewMenu();
        RequestPagesRebuild();
    }
    /// <summary>
    /// The menu hotkey from a row that can be handed anything (Prism · Settings, the new menu): one Quartz won't take is refused
    /// with one log line, else it is saved and PrismLib's key registry follows.
    /// </summary>
    internal static void TrySetMenuHotkey(int modifier, int key) {
        if(modifier < 0 || modifier > (int)Keybind.KeyModifier.Cmd || !Enum.IsDefined(typeof(KeyCode), key)
            || Quartz.Prism.PrismRules.HotkeyRefused(modifier, key)) {
            MainCore.Log.Msg($"Menu hotkey refused: {Keybind.Format((Keybind.KeyModifier)modifier, (KeyCode)key)}");
            return;
        }
        MainCore.Conf.ToggleModifier = modifier;
        MainCore.Conf.ToggleKey = key;
        MainCore.Conf.ClearRefusedSwitcherKey();   // the switcher may not keep a chord the menu hotkey now fires on
        MainCore.ConfMgr.RequestSave();
        Quartz.Prism.PrismBridge.RebindMenuKey();
    }
    // Deferred one pump: handlers still pending in this event snapshot would touch destroyed UI.
    private static void RequestPagesRebuild() {
        if(rebuildQueued) return;
        rebuildQueued = true;
        MainThread.Enqueue(() => {
            rebuildQueued = false;
            if(Panel == null) return;
            Rebuild();
        });
    }
    public static void Rebuild() => Rebuild(false);
    /// <summary>
    /// The new menu wrote a field the classic pages show, and a classic toggle never reads its field again: they are stale
    /// until the next rebuild, which clears this.
    /// </summary>
    internal static bool ClassicStale;
    /// <summary>Before the classic menu shows: rebuilt once if the new menu left it stale, untouched otherwise.</summary>
    internal static void FreshenClassic() {
        if(ClassicStale) Rebuild();
    }
    // Rebuild closes and reopens the classic menu; neither is a user's close, so neither hands back to the new menu.
    private static bool rebuilding;
    public static void Rebuild(bool reloadPanelSize) {
        rebuilding = true;
        try { RebuildNow(reloadPanelSize); } finally { rebuilding = false; }
    }
    private static void RebuildNow(bool reloadPanelSize) {
        ClassicStale = false;
        bool wasOpen = isOpen;
        bool wasHub = IsReorganizing;
        bool wasOverHub = panelOverHub;
        if(wasOpen) Close(true);
        Vector2 position = LastPanelPosition;
        Vector2 size = LastPanelSize;
        int scrolledState = CurrentMenuState;
        float scrollOffset = ScrollOf(scrolledState)?.Offset ?? 0f;
        Dispose();
        Initialize();
        LastPanelPosition = reloadPanelSize ? Vector2.zero : position;
        if(!reloadPanelSize) LastPanelSize = size;
        if(wasOpen) Open(true);
        else if(isOpen) Close(true);
        if(isOpen) {
            Panel.anchoredPosition = LastPanelPosition;
            Panel.sizeDelta = LastPanelSize;
            if(wasHub) RestoreHub(wasOverHub);
        }
        RestoreScroll(scrolledState, scrollOffset);
    }
    private static UIScrollController ScrollOf(int state) =>
        Pages.TryGetValue(state, out RectTransform page) && page != null
            ? page.GetComponentInChildren<UIScrollController>(true)
            : null;
    // Restore one pump later: the rebuilt content has zero height until it lays out.
    private static void RestoreScroll(int state, float offset) {
        if(offset <= 0f) return;
        MainThread.Enqueue(() => {
            if(CurrentMenuState != state) return;
            ScrollOf(state)?.JumpTo(offset);
        });
    }
    public static void Dispose() {
        MainCore.Tr.OnLoadEnd -= _onPageSettings;
        MainCore.Tr.OnLoadEnd -= _onRefresh;
        MainCore.Tr.OnLanguageChanged -= _onLanguageRebuild;
        MenuFactory.OnStateChanged -= _onDockTabChanged;
        Panes.PaneState.Changed -= _onPaneChanged;
        bool wasReorganizing = IsReorganizing;
        SetReorganizing(false);
        if(wasReorganizing) RaiseReorganize(false);
        panelTweener?.Kill();
        panelTweener = null;
        resetSequence?.Kill();
        resetSequence = null;
        bandSeq?.Kill();
        bandSeq = null;
        shellSeq?.Kill();
        shellSeq = null;
        reorganizeSeq?.Kill();
        reorganizeSeq = null;
        themeImages = null;
        themeTexts = null;
        UIObject.DisposeAll();
        ToggleBinds.ClearLive();
        Reorganizer.Dispose();
        UpdateToast.Dispose();
        Tooltip.Dispose();
        MainCore.TC.Clear();
        // The helper lives outside canvasObj, so Destroy(canvasObj) below misses it.
        DestroyFirstRunHelper();
        // Lives on its own canvas outside canvasObj too.
        if(reorganizeDim != null) UnityEngine.Object.Destroy(reorganizeDim);
        reorganizeDim = null;
        FontManager.MenuRoot = null;
        UnityEngine.Object.Destroy(canvasObj);
        canvasObj = null;
        canvas = null;
        canvasScaler = null;
        Panel = null;
        isOpen = false;
        holdingToggle = false;
        canvasWasVisible = false;
        lastScreenWidth = 0;
        lastScreenHeight = 0;
    }
}
