using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Features.Calibration;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Object = UnityEngine.Object;
using TMPro;
namespace Quartz.UI;
public static class CalibrationPopupUI {
    private const float WIDTH = 460f;
    private const float HEIGHT = 210f;
    private static readonly Vector2 ShownPos = Vector2.zero;
    private static readonly Vector2 HiddenPos = new(0f, -36f);
    private static GameObject canvasObj;
    private static GameObject panelObj;
    private static RectTransform panelRect;
    private static CanvasGroup group;
    private static TextMeshProUGUI messageText;
    private static GTween moveSeq;
    private static bool visible;
    private static float pendingOffsetMs;
    private static GameObject ownedEventSystem;
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
        visible = false;
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
        Image bg = panelObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(18.5f);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.PanelBG;
        {
            GameObject border = new("Border");
            border.transform.SetParent(panelObj.transform, false);
            RectTransform rect = border.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new(-2f, -2f);
            rect.offsetMax = new(2f, 2f);
            Image borderImg = border.AddComponent<Image>();
            borderImg.sprite = MainCore.Spr.GetRing(20.5f, 3f);
            borderImg.type = Image.Type.Sliced;
            borderImg.color = UIColors.ObjectActive;
            borderImg.raycastTarget = false;
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
            Image iconImg = icon.AddComponent<Image>();
            iconImg.sprite = MainCore.Spr.Get(UISprite.AdjustmentsHorizontal128, 34f);
            iconImg.color = UIColors.ObjectActive;
            iconImg.raycastTarget = false;
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
            messageText.textWrappingMode = TextWrappingModes.Normal;
            messageText.overflowMode = TextOverflowModes.Truncate;
            messageText.raycastTarget = false;
        }
        Button(panelObj.transform, -118f, UIColors.ObjectButton, "CALIBRATION_POPUP_YES", "Yes", OnYes);
        Button(panelObj.transform, 118f, new Color(1f, 1f, 1f, 0.12f), "CALIBRATION_POPUP_NO", "No", Hide);
        panelObj.SetActive(false);
    }
    private static void Button(Transform parent, float x, Color color, string localeKey, string defaultText, System.Action onClick) {
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
        if(canvasObj == null || !visible) return;
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
}
