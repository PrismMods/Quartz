using Quartz.Overlay;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PagePanels {
    private static GameObject panelsList;
    public static void Create(RectTransform parent) {
        PanelsOverlay.EnsureConf();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var headerRow = GenerateUI.Row(content.transform);
        var headerText = GenerateUI.AddTextH1(headerRow);
        GenerateUI.Localize(headerText, "SECTION_PANELS", "Panels");
        GenerateUI.Button(
            GenerateUI.Row(content.transform),
            () => {
                PanelConfig p = new() {
                    Name = "Panel " + (PanelsOverlay.Conf.Panels.Count + 1),
                };
                p.PosX += 24f * PanelsOverlay.Conf.Panels.Count;
                p.PosY -= 24f * PanelsOverlay.Conf.Panels.Count;
                PanelsOverlay.Conf.Panels.Add(p);
                PanelsOverlay.Save();
                PanelsOverlay.Rebuild();
                RebuildPanelsList();
            },
            "Create Panel",
            "panels_create"
        ).Rect.AddToolTip(
            "DESC_PANELS_CREATE",
            "Adds a new empty panel. Name it, put stats on it, then drag it into place with Reorganize."
        );
        panelsList = new GameObject("PanelsList");
        panelsList.transform.SetParent(content.transform, false);
        panelsList.AddComponent<RectTransform>();
        GenerateUI.FitVertical(panelsList);
        RebuildPanelsList();
    }
    private const int PANEL_SOFT_CAP = 10;
    private static void RebuildPanelsList() {
        if(panelsList == null) return;
        GenerateUI.ClearChildren(panelsList.transform);
        List<PanelConfig> panels = PanelsOverlay.Conf.Panels;
        if(panels.Count == 0) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(panelsList.transform), "PANEL_NO_PANELS", "No panels. Create one above.", 19f);
            return;
        }
        if(panels.Count > PANEL_SOFT_CAP) {
            GenerateUI.AddMutedText(GenerateUI.Row(panelsList.transform), 19f).text = string.Format(
                GenerateUI.Tr("PANEL_TOO_MANY", "{0} panels — that's a lot; performance may suffer."),
                panels.Count
            );
        }
        for(int i = 0; i < panels.Count; i++)
            CreatePanelSection(panelsList.transform, panels[i], i);
    }
    private static GameObject MakeListContainer(string name, Transform parent, float spacing) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        GenerateUI.FitVertical(obj, spacing);
        return obj;
    }
    private static GTween AnimateBody(
        RectTransform section, RectTransform rect,
        VerticalLayoutGroup layout, ContentSizeFitter fitter,
        LayoutElement le, CanvasGroup cg,
        bool open, Action onClosed = null
    ) {
        layout.enabled = true;
        fitter.enabled = true;
        le.preferredHeight = -1f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(section);
        float content = rect.rect.height;
        layout.enabled = false;
        fitter.enabled = false;
        le.preferredHeight = open ? 0f : content;
        cg.alpha = open ? 0f : 1f;
        GTween seq = GTweenSequenceBuilder.New()
            .Join(GTweenExtensions.Tween(
                () => le == null ? (open ? content : 0f) : le.preferredHeight,
                x => {
                    if(le == null) return;
                    le.preferredHeight = Mathf.Max(0f, x);
                    if(section != null) LayoutRebuilder.ForceRebuildLayoutImmediate(section);
                },
                open ? content : 0f,
                0.16f
            ).SetEasing(open ? Easing.OutBack : Easing.OutSine))
            .Join(GTweenExtensions.Tween(
                () => cg == null ? (open ? 1f : 0f) : cg.alpha,
                x => { if(cg != null) cg.alpha = x; },
                open ? 1f : 0f,
                0.16f
            ).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(le == null) return;
                if(open) {
                    if(layout != null) layout.enabled = true;
                    if(fitter != null) fitter.enabled = true;
                    le.preferredHeight = -1f;
                    if(section != null) LayoutRebuilder.ForceRebuildLayoutImmediate(section);
                } else {
                    if(rect != null) GenerateUI.ClearChildren(rect);
                    le.preferredHeight = 0f;
                    onClosed?.Invoke();
                }
            })
            .Build();
        MainCore.TC.Play(seq);
        return seq;
    }
    private static GameObject MakeDragHandle(Transform parent, string name, float width) {
        GameObject handle = new(name);
        handle.transform.SetParent(parent, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.pivot = new Vector2(0f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(width, 0f);
        handle.AddComponent<EmptyGraphic>().raycastTarget = true;
        for(int col = 0; col < 2; col++) {
            for(int dotRow = 0; dotRow < 3; dotRow++) {
                GameObject dot = new("Dot");
                dot.transform.SetParent(handle.transform, false);
                RectTransform dotRect = dot.AddComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = new Vector2(col * 8f - 4f, dotRow * 8f - 8f);
                dotRect.sizeDelta = new Vector2(4f, 4f);
                Image dotImg = dot.AddComponent<Image>();
                dotImg.sprite = MainCore.Spr.Get(UISprite.Circle256);
                dotImg.color = new Color(1f, 1f, 1f, 0.4f);
                dotImg.raycastTarget = false;
            }
        }
        return handle;
    }
}
