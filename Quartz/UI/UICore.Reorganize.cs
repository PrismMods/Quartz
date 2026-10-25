using Quartz.Async;
using Quartz.Core;
using Quartz.IO;
using Quartz.Localization;
using Quartz.Resource;
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
    // Lunar-style hub: the hotkey lands here in reorganize mode, and "Settings" opens the full panel.
    private static void CreateReorganizeHub() {
        reorganizeHub = new GameObject("ReorganizeHub");
        reorganizeHub.transform.SetParent(canvasObj.transform, false);
        var rect = reorganizeHub.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260f, 250f);
        reorganizeHubGroup = reorganizeHub.AddComponent<CanvasGroup>();
        GameObject logo = new("Logo");
        logo.transform.SetParent(rect, false);
        var logoRect = logo.AddComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 1f);
        logoRect.anchorMax = new Vector2(0.5f, 1f);
        logoRect.pivot = new Vector2(0.5f, 1f);
        logoRect.sizeDelta = new Vector2(120f, 120f);
        logo.AddComponent<ThemeExempt>();
        QuartzLogo.Create(logoRect);
        GameObject wordmark = new("Wordmark");
        wordmark.transform.SetParent(rect, false);
        var wordmarkText = wordmark.AddComponent<TextMeshProUGUI>();
        wordmarkText.text = Info.DisplayName.ToUpperInvariant();
        wordmarkText.font = FontManager.GetFont(WordmarkFont);
        wordmarkText.fontSize = 28f;
        wordmarkText.characterSpacing = 6f;
        wordmarkText.color = new Color(1f, 1f, 1f, 0.92f);
        wordmarkText.alignment = TextAlignmentOptions.Center;
        wordmarkText.raycastTarget = false;
        wordmark.AddComponent<FontExempt>();
        var wordmarkRect = wordmark.GetComponent<RectTransform>();
        wordmarkRect.anchorMin = new Vector2(0f, 1f);
        wordmarkRect.anchorMax = new Vector2(1f, 1f);
        wordmarkRect.pivot = new Vector2(0.5f, 1f);
        wordmarkRect.sizeDelta = new Vector2(0f, 40f);
        wordmarkRect.anchoredPosition = new Vector2(0f, -128f);
        var settings = GenerateUI.Button(rect, ShowPanelOverHub, "Settings", "settings");
        var br = settings.Rect;
        br.anchorMin = new Vector2(0.5f, 0f);
        br.anchorMax = new Vector2(0.5f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = new Vector2(240f, 56f);
        br.anchoredPosition = new Vector2(0f, 64f);
        var close = GenerateUI.Button(rect, () => Close(), "Close", "close").SetSecondary();
        var cr = close.Rect;
        cr.anchorMin = new Vector2(0.5f, 0f);
        cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(240f, 56f);
        cr.anchoredPosition = Vector2.zero;
        // Behind the panel, so "Settings" opens the panel on top of the hub.
        reorganizeHub.transform.SetAsFirstSibling();
        reorganizeHub.SetActive(false);
        // Own canvas just under the overlays (32754+): dims the game, not the HUD being arranged.
        reorganizeDim = new GameObject("QuartzReorganizeDim");
        reorganizeDim.transform.SetParent(MainCore.Root.transform, false);
        var dimCanvas = reorganizeDim.AddComponent<Canvas>();
        dimCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dimCanvas.sortingOrder = 32700;
        reorganizeDimGroup = reorganizeDim.AddComponent<CanvasGroup>();
        reorganizeDimGroup.blocksRaycasts = false;
        reorganizeDimGroup.interactable = false;
        var dim = new GameObject("Dim").AddComponent<Image>();
        dim.transform.SetParent(reorganizeDim.transform, false);
        dim.color = new Color(0f, 0f, 0f, 0.45f);
        dim.raycastTarget = false;
        var dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        reorganizeDim.SetActive(false);
    }
    private static void ShowPanelOverHub() {
        if(!IsReorganizing || Panel == null) return;
        panelOverHub = true;
        Panel.gameObject.SetActive(true);
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
        reorganizeSeq?.Kill();
        reorganizeSeq = panelCanvasGroup.GTFade(Mathf.Clamp01(MainCore.Conf.PanelOpacity), 0.2f).SetEasing(Easing.OutSine);
        MainCore.TC.Play(reorganizeSeq);
    }
    private static void HidePanelToHub() {
        if(!IsReorganizing || Panel == null) return;
        panelOverHub = false;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        reorganizeSeq?.Kill();
        reorganizeSeq = GTweenSequenceBuilder.New()
            .Join(panelCanvasGroup.GTFade(0f, 0.2f).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(IsReorganizing && !panelOverHub && Panel != null) Panel.gameObject.SetActive(false);
            })
            .Build();
        MainCore.TC.Play(reorganizeSeq);
    }
    [Obsolete("The movable exit button was replaced by the centred hub; this does nothing.")]
    public static void ResetExitReorganizePosition() { }
    internal static void OpenToHub() {
        Open(true);
        // Hidden before the fade so the panel doesn't flash on its way out.
        if(panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        EnterReorganize();
    }
    public static event Action<bool> OnReorganizeChanged;
    private static void RaiseReorganize(bool entering) {
        try {
            OnReorganizeChanged?.Invoke(entering);
        } catch(Exception e) {
            MainCore.Log.Err($"[UI] reorganize listener threw: {e.Message}");
        }
    }
    // Pinned in the empty right inset of every Overlay page, so it stays put while the page scrolls.
    internal static void AddReorganizeShortcut(RectTransform page) {
        var button = GenerateUI.Button(page, () => {
            if(IsReorganizing) HidePanelToHub();
            else EnterReorganize();
        }, "Reorganize", "overlay_reorganize");
        RectTransform r = button.Rect;
        r.anchorMin = new(1f, 1f);
        r.anchorMax = new(1f, 1f);
        r.pivot = new(1f, 1f);
        r.sizeDelta = new(160f, 50f);
        r.anchoredPosition = new(-18f, -18f);
    }
    public static void EnterReorganize() {
        if(IsReorganizing) return;
        IsReorganizing = true;
        panelOverHub = false;
        RaiseReorganize(true);
        if(panelCanvasGroup != null) {
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
        if(reorganizeHub != null) reorganizeHub.SetActive(true);
        if(reorganizeHubGroup != null) reorganizeHubGroup.alpha = 0f;
        if(reorganizeDim != null) reorganizeDim.SetActive(true);
        if(reorganizeDimGroup != null) reorganizeDimGroup.alpha = 0f;
        reorganizeSeq?.Kill();
        reorganizeSeq = GTweenSequenceBuilder.New()
            .Join(panelCanvasGroup.GTFade(0f, 0.2f).SetEasing(Easing.OutSine))
            .Join(reorganizeHubGroup.GTFade(1f, 0.2f).SetEasing(Easing.OutSine))
            .Join(reorganizeDimGroup.GTFade(1f, 0.2f).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(IsReorganizing && Panel != null) Panel.gameObject.SetActive(false);
            })
            .Build();
        MainCore.TC.Play(reorganizeSeq);
    }
    public static void ExitReorganize() {
        if(!IsReorganizing) return;
        IsReorganizing = false;
        panelOverHub = false;
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
            .Join(reorganizeHubGroup.GTFade(0f, 0.2f).SetEasing(Easing.OutSine))
            .Join(reorganizeDimGroup.GTFade(0f, 0.2f).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(IsReorganizing) return;
                if(reorganizeHub != null) reorganizeHub.SetActive(false);
                if(reorganizeDim != null) reorganizeDim.SetActive(false);
            })
            .Build();
        MainCore.TC.Play(reorganizeSeq);
    }
}
