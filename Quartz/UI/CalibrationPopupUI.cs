using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Features.Calibration;
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
namespace Quartz.UI;
public static class CalibrationPopupUI {
    private const float WIDTH = 460f;
    private const float HEIGHT = 210f;
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
        bgImage = panelObj.AddComponent<Image>();
        bgImage.sprite = MainCore.Spr.GetFilled(18.5f);
        bgImage.type = Image.Type.Sliced;
        {
            GameObject border = new("Border");
            border.transform.SetParent(panelObj.transform, false);
            RectTransform rect = border.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new(-2f, -2f);
            rect.offsetMax = new(2f, 2f);
            borderImage = border.AddComponent<Image>();
            borderImage.sprite = MainCore.Spr.GetRing(20.5f, 3f);
            borderImage.type = Image.Type.Sliced;
            borderImage.raycastTarget = false;
        }
        {
            GameObject icon = new("Icon");
            icon.transform.SetParent(panelObj.transform, false);
            RectTransform iconRect = icon.AddComponent<RectTransform>();
            iconRect.anchorMin = new(0.5f, 1f);
            iconRect.anchorMax = new(0.5f, 1f);
            iconRect.pivot = new(0.5f, 1f);
            iconRect.anchoredPosition = new(0f, -22f);
            iconRect.sizeDelta = new(34f, 34f);
            iconImage = icon.AddComponent<Image>();
            iconImage.sprite = MainCore.Spr.Get(UISprite.AdjustmentsHorizontal128, 34f);
            iconImage.raycastTarget = false;
        }
        {
            GameObject msg = new("Message");
            msg.transform.SetParent(panelObj.transform, false);
            RectTransform rect = msg.AddComponent<RectTransform>();
            rect.anchorMin = new(0f, 0f);
            rect.anchorMax = new(1f, 1f);
            rect.offsetMin = new(28f, 78f);
            rect.offsetMax = new(-28f, -60f);
            messageText = msg.AddComponent<TextMeshProUGUI>();
            messageText.font = FontManager.Current;
            messageText.fontSize = 22f;
            messageText.color = Color.white;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.verticalAlignment = VerticalAlignmentOptions.Middle;
            TextCompat.Wrap(messageText);
            messageText.overflowMode = TextOverflowModes.Truncate;
            messageText.raycastTarget = false;
        }
        yesImage = Button(panelObj.transform, -118f, UIColors.ObjectButton, "CALIBRATION_POPUP_YES", "Yes", OnYes);
        Button(panelObj.transform, 118f, new Color(1f, 1f, 1f, 0.12f), "CALIBRATION_POPUP_NO", "No", Hide);
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
        if(borderImage != null) borderImage.color = UIColors.ObjectActive;
        if(iconImage != null) iconImage.color = UIColors.ObjectActive;
        if(yesImage != null) yesImage.color = UIColors.ObjectButton;
    }
    private static Image Button(Transform parent, float x, Color color, string localeKey, string defaultText, System.Action onClick) {
        GameObject obj = new("Button_" + defaultText);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new(0.5f, 0f);
        rect.anchorMax = new(0.5f, 0f);
        rect.pivot = new(0.5f, 0.5f);
        rect.anchoredPosition = new(x, 40f);
        rect.sizeDelta = new(180f, 52f);
        Image img = obj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;
        img.color = color;
        GameObject labelObj = new("Label");
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.font = FontManager.Current;
        label.fontSize = 22f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.raycastTarget = false;
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
