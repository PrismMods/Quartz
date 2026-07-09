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

// The death-triggered "change your offset?" popup's visual half — a polished,
// animated replacement for BetterCalibration's raw placeholder canvas. Built
// once and shown/hidden on demand, same shape as UpdateToast.cs: own always-
// present canvas (so it draws over gameplay regardless of the settings panel's
// state), rounded panel + ring border, GTween slide+fade in/out instead of a
// hard SetActive cut.
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

    // Only ours if we had to make one — see EnsureEventSystem. Destroyed on Hide
    // so we never leave a second EventSystem fighting the game's.
    private static GameObject ownedEventSystem;

    // Build() is NOT called here. This runs during FeatureRegistry.EnableAll(),
    // which fires synchronously inside QuartzRuntime.Initialize() — early
    // enough in MelonLoader's boot that FontManager.Current/TMP's own static
    // init aren't guaranteed ready yet (AddComponent<TextMeshProUGUI>() came
    // back null and crashed the whole mod load). Building lazily on the first
    // Show() sidesteps that regardless of the exact cause, matching why
    // UpdateToast — the other on-demand popup-shaped UI — isn't wired through
    // FeatureRegistry either.
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

        // CanvasGroup defaults alpha to 1 — without this the fade-in tween in
        // Show() has nothing to animate from and the popup just snaps visible.
        group = panelObj.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Image bg = panelObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.PanelBG;

        {
            // Ring border — same technique as UpdateToast: a concentric ring
            // sprite instead of Outline, which smears at rounded corners.
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
            // Icon
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
            // Message — "Change input offset from Xms to Yms?"
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

        // The fill + click surface. Its raycastTarget (defaults true) is what
        // the GraphicRaycaster hits.
        Image img = obj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;
        img.color = color;

        // Label on a CHILD object, NOT on `obj`: an Image and a TextMeshProUGUI
        // cannot share one GameObject — both are uGUI Graphics writing to the
        // single CanvasRenderer, so the text mesh gets overwritten by the fill
        // and never shows (and the clobbered graphic broke the button's raycast
        // too). One Graphic per GameObject; the message text worked precisely
        // because it already lived on its own object.
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

        // Reuses GenerateUI's own hover-outline button behavior so this popup
        // feels consistent with every other clickable surface in Quartz.
        GenerateUI.AddButton(obj, btn => {
            if(btn == InputButton.Left) onClick();
        });
    }

    private static void OnYes() {
        Calibration.SetOffsetMs(pendingOffsetMs);
        Hide();
    }

    // uGUI clicks need an active EventSystem. The game has one, but the popup
    // fires on death — a moment where it isn't guaranteed active — so if none
    // is current we stand up a temporary one (the game uses the legacy input
    // manager via CustomStandaloneInputModule, so a plain StandaloneInputModule
    // drives it). Guarded on current == null, so when the game's IS active we
    // never create a second, conflicting one.
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
