using Quartz.Async;
using Quartz.Core;
using Quartz.IO;
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
    private static void CreateExitReorganizeButton() {
        exitReorganizeObj = new GameObject("ExitReorganizeButton");
        exitReorganizeObj.transform.SetParent(canvasObj.transform, false);
        var rect = exitReorganizeObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(240f, 60f);
        rect.anchoredPosition = new Vector2(MainCore.Conf.ExitReorganizeX, MainCore.Conf.ExitReorganizeY);
        exitReorganizeCanvasGroup = exitReorganizeObj.AddComponent<CanvasGroup>();
        var img = exitReorganizeObj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        img.type = Image.Type.Sliced;
        exitReorganizeIdleColor = Color.Lerp(UIColors.MenuHighlight, UIColors.MenuSelected, 0.5f);
        img.color = exitReorganizeIdleColor;
        exitReorganizeObj.AddComponent<Button>();
        HoldToMoveHandle hold = exitReorganizeObj.AddComponent<HoldToMoveHandle>();
        hold.Target = rect;
        hold.Clamp = ClampExitReorganizePosition;
        hold.OnClick = () => ExitReorganize();
        hold.OnMoved = SaveExitReorganizePosition;
        hold.OnReset = () => {
            if(MainCore.Conf.MiddleClickToDefault) ResetExitReorganizePosition();
        };
        hold.OnHoldChanged = holding => {
            if(img == null) return;
            exitReorganizeHoldSeq?.Kill();
            exitReorganizeHoldSeq = img.GTColor(holding ? UIColors.ObjectActive : exitReorganizeIdleColor, 0.16f)
                .SetEasing(Easing.OutSine);
            MainCore.TC.Play(exitReorganizeHoldSeq);
        };
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
        exitReorganizeObj.transform.AddToolTip(
            "EXIT_REORGANIZE_TIP",
            "Hold to move this button, middle click to reset it"
        );
        exitReorganizeObj.SetActive(false);
    }
    private static Vector2 ClampExitReorganizePosition(Vector2 pos) {
        if(canvasObj == null || exitReorganizeObj == null) return pos;
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        if(canvasRect == null) return pos;
        Vector2 canvasSize = canvasRect.rect.size;
        if(canvasSize.x <= 0f || canvasSize.y <= 0f) return pos;
        Vector2 size = ((RectTransform)exitReorganizeObj.transform).sizeDelta;
        float maxX = Mathf.Max(0f, (canvasSize.x - size.x) * 0.5f);
        float minY = Mathf.Min(0f, size.y - canvasSize.y);
        return new Vector2(Mathf.Clamp(pos.x, -maxX, maxX), Mathf.Clamp(pos.y, minY, 0f));
    }
    private static void ApplyExitReorganizePosition() {
        if(exitReorganizeObj == null) return;
        RectTransform rect = (RectTransform)exitReorganizeObj.transform;
        rect.anchoredPosition = ClampExitReorganizePosition(rect.anchoredPosition);
    }
    public static void ResetExitReorganizePosition() {
        if(exitReorganizeObj == null) return;
        CoreSettings def = new();
        Vector2 target = ClampExitReorganizePosition(new Vector2(def.ExitReorganizeX, def.ExitReorganizeY));
        MainCore.Conf.ExitReorganizeX = target.x;
        MainCore.Conf.ExitReorganizeY = target.y;
        MainCore.ConfMgr.RequestSave();
        RectTransform rect = (RectTransform)exitReorganizeObj.transform;
        exitReorganizeResetSeq?.Kill();
        exitReorganizeResetSeq = rect.GTAnchorPos(target, 0.26f).SetEasing(Easing.OutExpo);
        MainCore.TC.Play(exitReorganizeResetSeq);
    }
    private static void SaveExitReorganizePosition() {
        if(exitReorganizeObj == null) return;
        RectTransform rect = (RectTransform)exitReorganizeObj.transform;
        Vector2 pos = ClampExitReorganizePosition(rect.anchoredPosition);
        rect.anchoredPosition = pos;
        MainCore.Conf.ExitReorganizeX = pos.x;
        MainCore.Conf.ExitReorganizeY = pos.y;
        MainCore.ConfMgr.RequestSave();
    }
    public static event Action<bool> OnReorganizeChanged;
    private static void RaiseReorganize(bool entering) {
        try {
            OnReorganizeChanged?.Invoke(entering);
        } catch(Exception e) {
            MainCore.Log.Err($"[UI] reorganize listener threw: {e.Message}");
        }
    }
    public static void EnterReorganize() {
        if(IsReorganizing) return;
        IsReorganizing = true;
        RaiseReorganize(true);
        if(panelCanvasGroup != null) {
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
        if(exitReorganizeObj != null) exitReorganizeObj.SetActive(true);
        ApplyExitReorganizePosition();
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
        Reorganizer.Deselect();
        RaiseReorganize(false);
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
}
internal sealed class HoldToMoveHandle : MonoBehaviour {
    private const float HoldSeconds = 0.8f;
    public RectTransform Target;
    public Action OnClick;
    public Action OnMoved;
    public Action OnReset;
    public Action<bool> OnHoldChanged;
    public Func<Vector2, Vector2> Clamp;
    private bool pressed;
    private bool holding;
    private bool moved;
    private float downTime;
    private Vector2 grabOffset;
    private void Awake() {
        EventTrigger trigger = gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerDown, OnPointerDown, trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, OnDrag, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, OnPointerUp, trigger);
    }
    private void OnDisable() {
        if(holding) OnHoldChanged?.Invoke(false);
        pressed = false;
        holding = false;
        moved = false;
    }
    private void Update() {
        if(!pressed || holding) return;
        if(Time.unscaledTime - downTime < HoldSeconds) return;
        holding = true;
        OnHoldChanged?.Invoke(true);
    }
    private void OnPointerDown(PointerEventData e) {
        if(e.button != PointerEventData.InputButton.Left || Target == null) return;
        pressed = true;
        holding = false;
        moved = false;
        downTime = Time.unscaledTime;
        grabOffset = (Vector2)Target.position - e.position;
    }
    private void OnDrag(PointerEventData e) {
        if(!pressed || !holding || Target == null) return;
        Vector2 next = e.position + grabOffset;
        Vector3 pos = Target.position;
        pos.x = next.x;
        pos.y = next.y;
        Target.position = pos;
        if(Clamp != null) Target.anchoredPosition = Clamp(Target.anchoredPosition);
        moved = true;
    }
    private void OnPointerUp(PointerEventData e) {
        if(e.button == PointerEventData.InputButton.Middle) {
            if(UnityUtils.ReleasedInside(e, transform)) OnReset?.Invoke();
            return;
        }
        if(!pressed) return;
        pressed = false;
        bool wasHolding = holding;
        bool wasMoved = moved;
        holding = false;
        moved = false;
        if(wasHolding) OnHoldChanged?.Invoke(false);
        if(wasMoved) {
            OnMoved?.Invoke();
            return;
        }
        if(!wasHolding && UnityUtils.ReleasedInside(e, transform)) OnClick?.Invoke();
    }
}
