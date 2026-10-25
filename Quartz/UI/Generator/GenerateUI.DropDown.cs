using System.Globalization;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Tweens;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Builders;
using Quartz.Tween;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public static UIDropDown<T> DropDown<T>(
        Transform parent,
        T defaultValue,
        T value,
        IReadOnlyList<T> values,
        Func<T, string> display,
        Action<T> onChanged,
        string id,
        float width = 0f,
        string leftLabel = null,
        float rightInset = 250f
    ) {
        const float rowHeight = 50f;
        const float listTopOffset = 62f;
        GameObject root = new("Dropdown");
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new(0f, 0f);
        rootRect.anchorMax = new(1f, 1f);
        rootRect.pivot = new(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        RectTransform rect = BackGround(rightInset);
        rect.SetParent(root.transform, false);
        rect.pivot = new(rect.pivot.x, 1f);
        rect.anchorMin = new(rect.anchorMin.x, 1f);
        rect.anchorMax = new(rect.anchorMax.x, 1f);
        rect.sizeDelta = new(rect.sizeDelta.x, rowHeight);
        // A labelled dropdown is a full-width O5Kit row whose closed text reads "Label: value",
        // as Overlayer does it. width only sizes an unlabelled box.
        bool labelled = leftLabel != null;
        if(width > 0f && !labelled) {
            rect.anchorMin = new(1f, 1f);
            rect.anchorMax = new(1f, 1f);
            rect.pivot = new(1f, 1f);
            rect.sizeDelta = new(width, rowHeight);
            rect.anchoredPosition = new(-rightInset, 0f);
        }
        TextMeshProUGUI tmp = AddText(rect);
        tmp.text = display(value);
        TextCompat.NoWrap(tmp);
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.rectTransform.offsetMax = new(-50f, 0f);
        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();
        GameObject triangle = new("Triangle");
        triangle.transform.SetParent(rect, false);
        RectTransform triangleRect = triangle.AddComponent<RectTransform>();
        triangleRect.anchorMin = new(1f, 0.5f);
        triangleRect.anchorMax = new(1f, 0.5f);
        triangleRect.pivot = new(0.5f, 0.5f);
        triangleRect.anchoredPosition = new(-23f, 0f);
        triangleRect.sizeDelta = new(26f, 26f);
        Image triangleImage = triangle.AddComponent<Image>();
        triangleImage.sprite = MainCore.Spr.Get(UISprite.Triangle128);
        GameObject list = new("List");
        list.transform.SetParent(root.transform, false);
        RectTransform listRect = list.AddComponent<RectTransform>();
        listRect.anchorMin = new(0f, 1f);
        listRect.anchorMax = new(1f, 1f);
        listRect.pivot = new(0.5f, 1f);
        listRect.offsetMin = new(0f, -listTopOffset);
        listRect.offsetMax = new(-rightInset, -listTopOffset);
        if(width > 0f && !labelled) {
            listRect.anchorMin = new(1f, 1f);
            listRect.anchorMax = new(1f, 1f);
            listRect.pivot = new(1f, 1f);
            listRect.sizeDelta = new(width, listRect.sizeDelta.y);
            listRect.anchoredPosition = new(-rightInset, -listTopOffset);
        }
        Image listBg = list.AddComponent<Image>();
        listBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        listBg.type = Image.Type.Sliced;
        listBg.color = UIColors.ObjectBG;
        VerticalLayoutGroup layout = list.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        CanvasGroup listCg = list.AddComponent<CanvasGroup>();
        listCg.alpha = 0f;
        list.SetActive(false);
        UIDropDown<T> dropdown = new(
            id,
            rootRect,
            tmp,
            triangleImage,
            triangleRect,
            changeImg,
            list,
            listRect,
            listCg,
            values,
            display,
            defaultValue,
            value,
            onChanged
        );
        if(labelled) {
            string labelKey = LocaleKeyFromId(id, "LABEL");
            dropdown.HeaderDisplay = v => $"{(labelKey != null ? Tr(labelKey, leftLabel) : leftLabel)}: {display(v)}";
        }
        root.AddComponent<DropdownLanguageRefresh>().Init(dropdown.RefreshLanguage);
        GTween layoutSeq = null;
        RectTransform parentRect = parent as RectTransform ?? parent.GetComponent<RectTransform>();
        LayoutElement parentLayout = parent.GetComponent<LayoutElement>();
        List<RectTransform> layoutChain = [];
        for(Transform current = parent.parent; current != null; current = current.parent) {
            if(
                current is RectTransform chainRect &&
                (
                    current.GetComponent<LayoutGroup>() != null ||
                    current.GetComponent<ContentSizeFitter>() != null
                )
            )
                layoutChain.Add(chainRect);
        }
        void RebuildParentLayouts() {
            if(rootRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            if(parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            for(int i = 0; i < layoutChain.Count; i++) {
                RectTransform currentRect = layoutChain[i];
                if(currentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(currentRect);
            }
        }
        void UpdateHeight() {
            float spacing = layout.spacing;
            int valueCount = dropdown.Values?.Count ?? 0;
            float listHeight =
                (valueCount * rowHeight) +
                (Mathf.Max(0, valueCount - 1) * spacing);
            float targetHeight = dropdown.Expanded ? listTopOffset + listHeight : rowHeight;
            float targetAlpha = dropdown.Expanded ? 1f : 0f;
            layoutSeq?.Kill();
            if(parentLayout != null) {
                parentLayout.minHeight = rowHeight;
                parentLayout.flexibleHeight = 0f;
            }
            layoutSeq = GTweenSequenceBuilder.New()
                .Join(
                    GTweenExtensions.Tween(
                        () => parentLayout != null ? parentLayout.preferredHeight : rowHeight,
                        x => {
                            if(parentLayout != null) {
                                parentLayout.preferredHeight = Mathf.Max(rowHeight, x);
                            }
                            RebuildParentLayouts();
                        },
                        targetHeight,
                        0.14f
                    ).SetEasing(Easing.OutBack)
                )
                .Join(
                    GTweenExtensions.Tween(
                        () => listCg == null ? targetAlpha : listCg.alpha,
                        x => { if(listCg != null) listCg.alpha = x; },
                        targetAlpha,
                        0.16f
                    ).SetEasing(Easing.OutSine)
                )
                .Build();
            MainCore.TC.Play(layoutSeq);
        }
        dropdown.OnLayoutChanged = () => {
            UpdateHeight();
            RebuildParentLayouts();
        };
        AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Left:
                    dropdown.ToggleExpanded();
                    UpdateHeight();
                    RebuildParentLayouts();
                    break;
                case InputButton.Middle:
                    if(
                        MainCore.Conf.MiddleClickToDefault && dropdown.DefaultValue != null &&
                        !EqualityComparer<T>.Default.Equals(
                            dropdown.Value,
                            dropdown.DefaultValue
                        )
                    ) {
                        dropdown.Reset();
                    }
                    break;
            }
        });
        UpdateHeight();
        return dropdown;
    }
}
