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
    private static void BuildStatPickerCategories(
        Transform picker, PanelConfig panel, string idp, Action<string> onPick
    ) {
        bool any = false;
        List<string> categories = ["Accuracy", "Time", "BPM", "Map Stats", "Other"];
        IReadOnlyList<PanelsOverlay.StatDef> allStats = PanelsOverlay.AllStats;
        foreach(PanelsOverlay.StatDef stat in allStats)
            if(!categories.Contains(stat.Category)) categories.Add(stat.Category);
        foreach(string category in categories) {
            bool headerAdded = false;
            foreach(PanelsOverlay.StatDef stat in allStats) {
                if(stat.Category != category) continue;
                if(stat.Id != "text" && panel.Stats.Exists(e => e.Id == stat.Id)) continue;
                if(!headerAdded) {
                    headerAdded = true;
                    GenerateUI.AddLocalizedMutedText(
                        GenerateUI.Row(picker, 32f),
                        GenerateUI.LocaleKeyFromText("PANEL_CATEGORY", category),
                        category
                    );
                }
                any = true;
                string statId = stat.Id;
                GenerateUI.Button(
                    GenerateUI.Row(picker),
                    () => onPick(statId),
                    stat.Label,
                    idp + "_pick_" + statId
                ).SetSecondary();
            }
        }
        if(!any) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(picker), "PANEL_ALL_STATS_ADDED",
                "All stats are already on this panel.", 19f);
        }
    }
    private static void BuildStatRow(
        Transform parent, StatEntry entry,
        Action commitOrder, Action onDelete, Action onSwap, Action onColor, Action save,
        string idp
    ) {
        RectTransform row = GenerateUI.Row(parent);
        row.gameObject.AddComponent<StatRowMarker>().Entry = entry;
        RectTransform bg = GenerateUI.BackGround();
        bg.SetParent(row, false);
        GameObject handle = MakeDragHandle(bg, "DragHandle", 40f);
        StatRowDrag drag = handle.AddComponent<StatRowDrag>();
        drag.Row = row;
        drag.OnReordered = commitOrder;
        if(entry.Id == "text") {
            BuildTextEntryInput(bg, entry, save);
        } else {
            var label = GenerateUI.AddText(bg, true);
            GenerateUI.Localize(
                label,
                GenerateUI.LocaleKeyFromText("PANEL_STAT", entry.Id),
                StatDefaultLabel(entry.Id)
            );
            RectTransform labelRect = label.rectTransform;
            labelRect.offsetMin = new Vector2(48f, 0f);
            labelRect.offsetMax = new Vector2(-300f, 0f);
        }
        GameObject toggleObj = new("EnableDot");
        toggleObj.transform.SetParent(bg, false);
        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-240f, 0f);
        toggleRect.sizeDelta = new Vector2(26f, 26f);
        Image toggleImg = toggleObj.AddComponent<Image>();
        toggleImg.sprite = MainCore.Spr.Get(UISprite.Circle256);
        void ApplyToggleColor() {
            toggleImg.color = entry.Enabled
                ? UIColors.ObjectActive
                : new Color(1f, 1f, 1f, 0.18f);
        }
        ApplyToggleColor();
        GenerateUI.AddButton(toggleObj, btn => {
            if(btn != InputButton.Left) return;
            entry.Enabled = !entry.Enabled;
            ApplyToggleColor();
            save();
        });
        GameObject labelDot = new("LabelDot");
        labelDot.transform.SetParent(bg, false);
        RectTransform labelDotRect = labelDot.AddComponent<RectTransform>();
        labelDotRect.anchorMin = new Vector2(1f, 0.5f);
        labelDotRect.anchorMax = new Vector2(1f, 0.5f);
        labelDotRect.pivot = new Vector2(1f, 0.5f);
        labelDotRect.anchoredPosition = new Vector2(-270f, 0f);
        labelDotRect.sizeDelta = new Vector2(26f, 26f);
        Image labelDotImg = labelDot.AddComponent<Image>();
        labelDotImg.sprite = MainCore.Spr.Get(UISprite.Circle256);
        var labelDotText = GenerateUI.AddText(labelDot.transform, true);
        labelDotText.text = "T";
        labelDotText.fontSize = 15f;
        labelDotText.alignment = TextAlignmentOptions.Center;
        labelDotText.raycastTarget = false;
        void ApplyLabelDotColor() {
            labelDotImg.color = entry.ShowLabel
                ? UIColors.ObjectActive
                : new Color(1f, 1f, 1f, 0.18f);
        }
        ApplyLabelDotColor();
        GenerateUI.AddButton(labelDot, btn => {
            if(btn != InputButton.Left) return;
            entry.ShowLabel = !entry.ShowLabel;
            ApplyLabelDotColor();
            save();
        });
        GenerateUI.MiniButton(bg, "Setting", "SETTING_SHORT", -144f, 88f, onColor);
        GenerateUI.MiniButton(bg, "Swap", "SWAP", -56f, 84f, onSwap);
        GenerateUI.MiniButton(bg, "X", "DELETE_SHORT", -8f, 44f, onDelete);
    }
}
