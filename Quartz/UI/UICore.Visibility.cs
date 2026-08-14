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
    private static float holdStartTime = 0f;
    private static bool holdingToggle = false;
    private static GTween panelTweener;
    private static GTween resetSequence;
    private static bool isOpen = false;
    public static bool IsOpen => isOpen;
    public static Vector2 LastPanelPosition;
    public static Vector2 LastPanelSize;
    public static Vector2 DefaultPanelSize {
        get {
            float scale = MainCore.Conf.UIScale <= 0f ? 1f : MainCore.Conf.UIScale;
            return Vector2.Min(new Vector2(1280f / scale, 720f / scale), CanvasSize);
        }
    }
    private static Vector2 MinPanelSize {
        get {
            float scale = MainCore.Conf.UIScale <= 0f ? 1f : MainCore.Conf.UIScale;
            return Vector2.Min(
                new Vector2(ResizeHandle.MIN_WIDTH / scale, ResizeHandle.MIN_HEIGHT / scale),
                CanvasSize
            );
        }
    }
    private static Vector2 ClampToCanvas(Vector2 size) {
        Vector2 canvas = CanvasSize;
        Vector2 min = MinPanelSize;
        return new Vector2(
            Mathf.Clamp(size.x, min.x, Mathf.Max(min.x, canvas.x)),
            Mathf.Clamp(size.y, min.y, Mathf.Max(min.y, canvas.y))
        );
    }
    private static Vector2 LoadSavedPanelSize() {
        Vector2 canvas = CanvasSize;
        float fw = MainCore.Conf.PanelWidthFrac;
        float fh = MainCore.Conf.PanelHeightFrac;
        if(fw <= 0f || fh <= 0f) {
            float legacyW = MainCore.Conf.PanelWidth;
            float legacyH = MainCore.Conf.PanelHeight;
            if(legacyW <= 0f || legacyH <= 0f) return DefaultPanelSize;
            fw = legacyW / canvas.x;
            fh = legacyH / canvas.y;
        }
        return ClampToCanvas(new Vector2(fw * canvas.x, fh * canvas.y));
    }
    public static void SavePanelSize() {
        if(Panel == null) return;
        Vector2 canvas = CanvasSize;
        LastPanelSize = Panel.sizeDelta;
        MainCore.Conf.PanelWidthFrac = Mathf.Clamp01(Panel.sizeDelta.x / canvas.x);
        MainCore.Conf.PanelHeightFrac = Mathf.Clamp01(Panel.sizeDelta.y / canvas.y);
        MainCore.Conf.PanelWidth = 0f;
        MainCore.Conf.PanelHeight = 0f;
        MainCore.ConfMgr.RequestSave();
        float clamped = ClampBand(bandHeight);
        if(!Mathf.Approximately(clamped, bandHeight)) {
            bandHeight = clamped;
            RefreshBand(false);
        }
    }
    private static bool canvasWasVisible;
    private static int lastScreenWidth;
    private static int lastScreenHeight;
    public static Vector2 ClampPanelPosition(Vector2 position, Vector2 size) {
        Vector2 canvas = CanvasSize;
        Vector2 limit = Vector2.Max((canvas - size) * 0.5f, Vector2.zero);
        return new Vector2(
            Mathf.Clamp(position.x, -limit.x, limit.x),
            Mathf.Clamp(position.y, -limit.y, limit.y)
        );
    }
    private static void HandleResolutionChange() {
        if(Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;
        if(Panel == null || !TryGetCanvasSize(out _)) return;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        LastPanelSize = LoadSavedPanelSize();
        LastPanelPosition = ClampPanelPosition(LastPanelPosition, LastPanelSize);
        Panel.sizeDelta = LastPanelSize;
        Panel.anchoredPosition = LastPanelPosition;
        float clamped = ClampBand(bandHeight);
        if(!Mathf.Approximately(clamped, bandHeight)) {
            bandHeight = clamped;
            RefreshBand(false);
        }
    }
    public static void HandleUpdate() {
        if(canvasObj == null) return;
        HandleResolutionChange();
        Keybind.KeyModifier mod = (Keybind.KeyModifier)MainCore.Conf.ToggleModifier;
        KeyCode key = (KeyCode)MainCore.Conf.ToggleKey;
        bool modHeld = Keybind.ModifierHeld(mod);
        bool editing = UIInputBlocker.IsEditing;
        bool pressed = !editing && modHeld && Input.GetKey(key);
        if(!editing && !Keybind.Capturing && modHeld && Input.GetKeyDown(key)) {
            Toggle();
            holdStartTime = Time.unscaledTime;
            holdingToggle = true;
        }
        if(holdingToggle && pressed && Time.unscaledTime - holdStartTime >= 0.4f) {
            ResetScalePosition(!isOpen);
            holdingToggle = false;
        }
        if(Input.GetKeyUp(key)) holdingToggle = false;
        ToggleBinds.HandleUpdate();
        if(!canvasObj.activeSelf) {
            if(canvasWasVisible) {
                canvasWasVisible = false;
                UIObject.NotifyHidden();
            }
            return;
        }
        canvasWasVisible = true;
        UIObject.TickAll();
        Tooltip.Tick();
    }
    private static Vector2 GetRandomOffscreenPosition() {
        float halfW = Screen.width * 0.5f;
        float halfH = Screen.height * 0.5f;
        int side = Random.Range(0, 4);
        return side switch {
            0 => new(
                -halfW - Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),
            1 => new(
                halfW + Panel.sizeDelta.x,
                Random.Range(-halfH, halfH)
            ),
            2 => new(
                Random.Range(-halfW, halfW),
                halfH + Panel.sizeDelta.y
            ),
            _ => new(
                Random.Range(-halfW, halfW),
                -halfH - Panel.sizeDelta.y
            )
        };
    }
    public static void Open(bool noAnimate = false) {
        if(isOpen || Panel == null) return;
        isOpen = true;
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
        // Before the noAnimate early-return, not after it: opening the menu is what
        // completes the first-run tutorial, and ShowOnStartup plus Rebuild() both
        // come through here with noAnimate set. Sitting below the return meant those
        // paths left the "Press <key>" prompt on screen for good.
        if(firstRunHelperActivated) {
            firstRunHelperActivated = false;
            EndFirstRunHelper();
        }
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
    }
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
            if(texts[i] != null) texts[i].ForceMeshUpdate(false, true);
        }
        if(Panel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(Panel);
        refreshedFontId = fontId;
        refreshedCharacterCount = font?.characterTable?.Count ?? 0;
        refreshedAtlasCount = font?.atlasTextures?.Length ?? 0;
    }
    public static void Close(bool noAnimate = false) {
        if(!isOpen) return;
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
}
