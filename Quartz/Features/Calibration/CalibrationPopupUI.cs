using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Object = UnityEngine.Object;
using TMPro;
using Quartz.Compat.Game;
using Quartz.UI;
namespace Quartz.Features.Calibration;
public static class CalibrationPopupUI {
    private const float WIDTH = 460f;
    private const float HEIGHT = 200f;
    private const float TITLE_HEIGHT = 44f;
    private static Vector2 ShownPos {
        get {
            Calibration.EnsureConf();
            return OverlayCalibration.Scale(new Vector2(Calibration.Conf.PopupOffsetX, Calibration.Conf.PopupOffsetY));
        }
    }
    private static Vector2 HiddenPos => ShownPos + new Vector2(0f, -36f);
    private static GameObject canvasObj;
    private static GameObject panelObj;
    private static RectTransform panelRect;
    private static CanvasGroup group;
    private static TextMeshProUGUI messageText;
    private static Image bgImage;
    private static Image borderImage;
    private static Image iconImage;
    private static Image yesImage;
    private static Image noImage;
    private static Image barImage;
    private static GTween moveSeq;
    private static bool visible;
    private static float pendingOffsetMs;
    private static GameObject ownedEventSystem;
    private static GameObject dragObj;
    private static bool reorganizing;
    public static void Initialize() => SceneManager.sceneUnloaded += OnSceneUnloaded;
    public static void Dispose() {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        moveSeq?.Kill();
        ReleaseEventSystem();
        if(canvasObj != null) Object.Destroy(canvasObj);
        canvasObj = null;
        panelObj = null;
        panelRect = null;
        group = null;
        messageText = null;
        bgImage = null;
        borderImage = null;
        iconImage = null;
        yesImage = null;
        noImage = null;
        barImage = null;
        dragObj = null;
        visible = false;
        reorganizing = false;
    }
    private static void OnSceneUnloaded(Scene _) => Hide();
    private static void Build() {
        canvasObj = new GameObject("QuartzCalibrationPopupCanvas");
        canvasObj.transform.SetParent(MainCore.Root.transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        canvas.pixelPerfect = true;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = UICore.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        panelObj = new GameObject("Popup");
        panelObj.transform.SetParent(canvasObj.transform, false);
        panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new(0.5f, 0.5f);
        panelRect.anchorMax = new(0.5f, 0.5f);
        panelRect.pivot = new(0.5f, 0.5f);
        panelRect.sizeDelta = new(WIDTH, HEIGHT);
        panelRect.anchoredPosition = HiddenPos;
        group = panelObj.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        // Same chrome as the settings window: rounded body, title bar, faint outline.
        bgImage = panelObj.AddComponent<Image>();
        bgImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bgImage.type = Image.Type.Sliced;
        {
            GameObject bar = new("TitleBar");
            bar.transform.SetParent(panelObj.transform, false);
            RectTransform rect = bar.AddComponent<RectTransform>();
            rect.anchorMin = new(0f, 1f);
            rect.anchorMax = new(1f, 1f);
            rect.pivot = new(0.5f, 1f);
            rect.offsetMin = new(0f, -TITLE_HEIGHT);
            rect.offsetMax = Vector2.zero;
            barImage = bar.AddComponent<Image>();
            barImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleHalf256P1024);
            barImage.type = Image.Type.Sliced;
            barImage.raycastTarget = false;
            GameObject icon = new("Icon");
            icon.transform.SetParent(bar.transform, false);
            RectTransform iconRect = icon.AddComponent<RectTransform>();
            iconRect.anchorMin = new(0f, 0.5f);
            iconRect.anchorMax = new(0f, 0.5f);
            iconRect.pivot = new(0f, 0.5f);
            iconRect.anchoredPosition = new(18f, 0f);
            iconRect.sizeDelta = new(24f, 24f);
            iconImage = icon.AddComponent<Image>();
            iconImage.sprite = MainCore.Spr.Get(UISprite.AdjustmentsHorizontal128, 24f);
            iconImage.raycastTarget = false;
            GameObject title = new("Title");
            title.transform.SetParent(bar.transform, false);
            RectTransform titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new(52f, 0f);
            titleRect.offsetMax = new(-18f, 0f);
            TextMeshProUGUI titleText = MenuText(title, 18f);
            titleText.alignment = TextAlignmentOptions.Left;
            GenerateUI.Localize(titleText, "SECTION_CALIBRATION", "Calibration");
        }
        {
            GameObject msg = new("Message");
            msg.transform.SetParent(panelObj.transform, false);
            RectTransform rect = msg.AddComponent<RectTransform>();
            rect.anchorMin = new(0f, 0f);
            rect.anchorMax = new(1f, 1f);
            rect.offsetMin = new(24f, 72f);
            rect.offsetMax = new(-24f, -TITLE_HEIGHT - 8f);
            messageText = MenuText(msg, 20f);
            messageText.alignment = TextAlignmentOptions.Left;
            TextCompat.Wrap(messageText);
            // Fixed box: shrink long messages instead of cutting them.
            messageText.fontSizeMax = 20f;
            messageText.fontSizeMin = 15f;
            messageText.enableAutoSizing = true;
            messageText.overflowMode = TextOverflowModes.Ellipsis;
        }
        {
            GameObject border = new("Border");
            border.transform.SetParent(panelObj.transform, false);
            RectTransform rect = border.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            borderImage = border.AddComponent<Image>();
            borderImage.sprite = MainCore.Spr.GetRing(12f, UICore.OUTLINE_WIDTH);
            borderImage.type = Image.Type.Sliced;
            borderImage.raycastTarget = false;
        }
        noImage = Button(panelObj.transform, -154f, "CALIBRATION_POPUP_NO", "No", Hide);
        yesImage = Button(panelObj.transform, -16f, "CALIBRATION_POPUP_YES", "Yes", OnYes);
        dragObj = ReorganizeHandle.CreateDragSurface(
            panelRect,
            () => MainCore.Tr.Get("SECTION_CALIBRATION", "Calibration"),
            CaptureOffset
        );
        ApplyTheme();
        panelObj.SetActive(false);
    }
    private static void ApplyTheme() {
        if(bgImage != null) bgImage.color = UIColors.PanelBG;
        if(barImage != null) barImage.color = UIColors.TopBar;
        if(borderImage != null) borderImage.color = UIColors.Border;
        if(noImage != null) noImage.color = UIColors.ObjectBG;
        if(iconImage != null) iconImage.color = UIColors.ObjectActive;
        if(yesImage != null) yesImage.color = UIColors.ObjectButton;
    }
    private static TextMeshProUGUI MenuText(GameObject obj, float size) {
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.MenuFontAsset;
        text.fontSize = size;
        text.characterSpacing = -3f;
        text.color = Color.white;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.raycastTarget = false;
        return text;
    }
    // Right-aligned pill, like the settings window's buttons; x is the right edge's offset.
    private static Image Button(Transform parent, float x, string localeKey, string defaultText, System.Action onClick) {
        GameObject obj = new("Button_" + defaultText);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(1f, 0f);
        rect.anchorMax = new(1f, 0f);
        rect.pivot = new(1f, 0.5f);
        rect.anchoredPosition = new(x, 38f);
        rect.sizeDelta = new(130f, 44f);
        Image img = obj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;
        GameObject labelObj = new("Label");
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = MenuText(labelObj, 18f);
        label.alignment = TextAlignmentOptions.Center;
        GenerateUI.Localize(label, localeKey, defaultText);
        GenerateUI.AddButton(obj, btn => {
            if(btn == InputButton.Left) onClick();
        });
        return img;
    }
    private static void OnYes() {
        Calibration.SetOffsetMs(pendingOffsetMs);
        Hide();
    }
    private static void EnsureEventSystem() {
        if(EventSystem.current != null) return;
        ownedEventSystem = new GameObject("QuartzCalibrationEventSystem");
        Object.DontDestroyOnLoad(ownedEventSystem);
        ownedEventSystem.AddComponent<EventSystem>();
        ownedEventSystem.AddComponent<StandaloneInputModule>();
    }
    private static void ReleaseEventSystem() {
        if(ownedEventSystem == null) return;
        Object.Destroy(ownedEventSystem);
        ownedEventSystem = null;
    }
    public static void Show(float currentOffsetMs, float suggestedOffsetMs) {
        if(canvasObj == null) Build();
        if(canvasObj == null) return;
        EnsureEventSystem();
        ApplyTheme();
        pendingOffsetMs = suggestedOffsetMs;
        messageText.text = string.Format(
            MainCore.Tr.Get("CALIBRATION_POPUP_CHANGE_OFFSET", "Change input offset from {0}ms to {1}ms?"),
            Calibration.FormatMs(currentOffsetMs), Calibration.FormatMs(suggestedOffsetMs)
        );
        visible = true;
        panelObj.SetActive(true);
        Cursor.visible = true;
        moveSeq?.Kill();
        moveSeq = GTweenSequenceBuilder.New()
            .Join(panelRect.GTAnchorPos(ShownPos, 0.4f).SetEasing(Easing.OutBack))
            .Join(group.GTFade(1f, 0.25f).SetEasing(Easing.OutSine))
            .Build();
        MainCore.TC.Play(moveSeq);
    }
    public static void Hide() {
        if(reorganizing || canvasObj == null || !visible) return;
        visible = false;
        ReleaseEventSystem();
        moveSeq?.Kill();
        moveSeq = GTweenSequenceBuilder.New()
            .Join(panelRect.GTAnchorPos(HiddenPos, 0.2f).SetEasing(Easing.OutSine))
            .Join(group.GTFade(0f, 0.18f).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(!visible && panelObj != null) panelObj.SetActive(false);
            })
            .Build();
        MainCore.TC.Play(moveSeq);
        if((ADOBase.isLevelEditor || ADOBase.controller is { paused: false }) && ADOBase.conductor is { isGameWorld: true })
            Cursor.visible = !Persistence.GetHideCursorWhilePlaying();
    }
    public static void BeginReorganize() {
        if(reorganizing) return;
        if(canvasObj == null) Build();
        if(canvasObj == null) return;
        reorganizing = true;
        EnsureEventSystem();
        ApplyTheme();
        messageText.text = string.Format(
            MainCore.Tr.Get("CALIBRATION_POPUP_CHANGE_OFFSET", "Change input offset from {0}ms to {1}ms?"),
            Calibration.FormatMs(0f), Calibration.FormatMs(12f)
        );
        moveSeq?.Kill();
        panelObj.SetActive(true);
        panelRect.anchoredPosition = ShownPos;
        group.alpha = 1f;
        dragObj.SetActive(true);
    }
    public static void EndReorganize() {
        if(!reorganizing) return;
        reorganizing = false;
        CaptureOffset();
        if(dragObj != null) dragObj.SetActive(false);
        if(visible) return;
        ReleaseEventSystem();
        if(panelRect != null) panelRect.anchoredPosition = HiddenPos;
        if(group != null) group.alpha = 0f;
        if(panelObj != null) panelObj.SetActive(false);
    }
    private static void CaptureOffset() {
        if(panelRect == null) return;
        Calibration.EnsureConf();
        Vector2 stored = OverlayCalibration.Unscale(panelRect.anchoredPosition);
        Calibration.Conf.PopupOffsetX = stored.x;
        Calibration.Conf.PopupOffsetY = stored.y;
        Calibration.Save();
    }
    public static void ResetPosition() {
        Calibration.EnsureConf();
        CalibrationSettings def = new();
        Calibration.Conf.PopupOffsetX = def.PopupOffsetX;
        Calibration.Conf.PopupOffsetY = def.PopupOffsetY;
        Calibration.Save();
        if(panelRect != null) panelRect.anchoredPosition = reorganizing || visible ? ShownPos : HiddenPos;
    }
}
