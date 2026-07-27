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
