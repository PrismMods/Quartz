using System.Text;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal sealed partial class TufBrowserView : MonoBehaviour {
    private void BuildHeader(RectTransform parent) {
        RectTransform titleRect = Rect("Title", parent, new(0f, 1f), new(1f, 1f), new(0f, -30f), Vector2.zero);
        TMP_Text title = Text(titleRect, "TUF", 28f, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<TextLocalization>().Init("TUF", "TUF");
        RectTransform taglineRect = Rect("Tagline", titleRect, new(0f, 0f), new(1f, 1f), new(78f, 4f), new(0f, 0f));
        TMP_Text tagline = Text(taglineRect, "Browse community levels, download them, then load them in the editor.", 14f, TextAlignmentOptions.Left);
        tagline.color = new(1f, 1f, 1f, 0.42f);
        tagline.gameObject.AddComponent<TextLocalization>().Init("TUF_TAGLINE", tagline.text);
        RectTransform searchRow = Rect("Search Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -78f), new(0f, -42f));
        AddHorizontal(searchRow);
        BuildSearch(searchRow);
        (Image refresh, TMP_Text refreshLabel) = Chip(searchRow, "Refresh", 92f, service.Refresh);
        refreshLabel.gameObject.AddComponent<TextLocalization>().Init("TUF_REFRESH", "Refresh");
        RectTransform sortRow = Rect("Sort Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -126f), new(0f, -90f));
        AddHorizontal(sortRow);
        AddSortChip(sortRow, TufSort.Recent, "TUF_SORT_RECENT", "Recent", 76f);
        AddSortChip(sortRow, TufSort.Difficulty, "TUF_SORT_DIFFICULTY", "Difficulty", 92f);
        AddSortChip(sortRow, TufSort.Clears, "TUF_SORT_CLEARS", "Clears", 70f);
        AddSortChip(sortRow, TufSort.Likes, "TUF_SORT_LIKES", "Likes", 64f);
        (directionChip, directionLabel) = Chip(sortRow, "↓", 48f, service.ToggleAscending);
        (installedChip, installedLabel) = Chip(sortRow, "Installed", 96f, () => {
            DisarmDelete();
            service.ToggleInstalled();
        });
        installedLabel.gameObject.AddComponent<TextLocalization>().Init("TUF_INSTALLED", "Installed");
        installedChip.rectTransform.AddToolTip("DESC_TUF_INSTALLED",
            "Show only the levels you have downloaded, newest first. Works offline.");
        gridChip = IconChip(sortRow, UISprite.Grid128, 48f, () => service.SetGridView(!service.GridView));
        gridChip.rectTransform.AddToolTip("DESC_TUF_GRID_VIEW",
            "Lay the level browser out as a grid of cards instead of one column of rows. The column count follows the window width.");
        AddFlexibleSpacer(sortRow);
        BuildDifficultyChips(sortRow);
        RectTransform rangeRow = Rect("Difficulty Range", parent, new(0f, 1f), new(1f, 1f), new(0f, -186f), new(0f, -130f));
        difficultyRange = TufDifficultyRangeBar.Create(rangeRow, service.MinDifficultyIndex,
            service.MaxDifficultyIndex, service.SetDifficultyRange);
        quantumRow = Rect("Quantum Range", parent, new(0f, 1f), new(1f, 1f), new(0f, -256f), new(0f, -192f));
        quantumRange = TufDifficultyRangeBar.CreateQuantum(quantumRow, sortRow, service.QuantumEnabled,
            service.QuantumMinIndex, service.QuantumMaxIndex, service.SetQuantumRange, service.ClearQuantum);
    }
    private void BuildDifficultyChips(Transform parent) {
        RectTransform host = Rect("Special Dropleft", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement hostSize = host.gameObject.AddComponent<LayoutElement>();
        hostSize.minWidth = hostSize.preferredWidth = 94f;
        RectTransform button = Rect("Special Button", host, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image buttonBg = button.gameObject.AddComponent<Image>();
        buttonBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        buttonBg.type = Image.Type.Sliced;
        buttonBg.color = UIColors.ObjectBG;
        TMP_Text label = Text(button, "Special", 14f, TextAlignmentOptions.Left);
        label.rectTransform.offsetMin = new(38f, 0f);
        label.rectTransform.offsetMax = new(-12f, 0f);
        label.color = new(1f, 1f, 1f, 0.7f);
        label.raycastTarget = false;
        label.gameObject.AddComponent<TextLocalization>().Init("TUF_SPECIAL", "Special");
        specialArrowRect = Rect("Arrow", button, new(0f, 0.5f), new(0f, 0.5f), Vector2.zero, Vector2.zero);
        specialArrowRect.sizeDelta = new(18f, 18f);
        specialArrowRect.anchoredPosition = new(16f, 0f);
        specialArrowRect.localEulerAngles = new(0f, 0f, 90f);
        specialArrow = specialArrowRect.gameObject.AddComponent<Image>();
        specialArrow.sprite = MainCore.Spr.Get(UISprite.Triangle128);
        specialArrow.color = UIColors.ObjectInactive;
        specialArrow.raycastTarget = false;
        GenerateUI.AddButton(button.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) ToggleSpecialDropdown();
        });
        button.AddToolTip("TUF_SPECIAL", "Special difficulties");
        specialChecks = Rect("Special Options", host, new(0f, 0f), new(0f, 1f), new(2f, 0f), new(2f, 0f));
        specialChecks.pivot = new(1f, 0.5f);
        specialChecks.localScale = new(0.82f, 1f, 1f);
        Image checksBackdrop = specialChecks.gameObject.AddComponent<Image>();
        checksBackdrop.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        checksBackdrop.type = Image.Type.Sliced;
        checksBackdrop.color = UIColors.PanelBG;
        HorizontalLayoutGroup checksLayout = AddHorizontal(specialChecks, 6f);
        checksLayout.padding = new RectOffset(6, 6, 0, 0);
        ContentSizeFitter checksFit = specialChecks.gameObject.AddComponent<ContentSizeFitter>();
        checksFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        specialChecksCg = specialChecks.gameObject.AddComponent<CanvasGroup>();
        specialChecksCg.alpha = 0f;
        specialChecksCg.blocksRaycasts = false;
        specialChecksCg.interactable = false;
        AddDifficultyCheckbox(specialChecks, "Unranked", "TUF_SPECIAL_UNRANKED", "Unranked", 116f);
        AddDifficultyCheckbox(specialChecks, "Censored", "TUF_SPECIAL_CENSORED", "Censored", 114f);
        AddDifficultyCheckbox(specialChecks, "Impossible", "TUF_SPECIAL_IMPOSSIBLE", "Impossible", 122f);
    }
    private void ToggleSpecialDropdown() {
        specialExpanded = !specialExpanded;
        specialChecksCg.blocksRaycasts = specialExpanded;
        specialChecksCg.interactable = specialExpanded;
        specialArrowSeq?.Kill();
        specialArrowSeq = GTweenSequenceBuilder.New()
            .Join(specialArrowRect.GTRotate(new Vector3(0f, 0f, specialExpanded ? -90f : 90f), 0.45f)
                .SetEasing(specialExpanded ? Easing.OutBounce : Easing.OutBack))
            .Join(specialArrow.GTColor(specialExpanded ? UIColors.ObjectActive : UIColors.ObjectInactive, 0.2f)
                .SetEasing(Easing.OutSine))
            .Join(specialChecksCg.GTAlpha(specialExpanded ? 1f : 0f, specialExpanded ? 0.14f : 0.16f).SetEasing(Easing.OutSine))
            .Join(GTweens.Extensions.GTweenExtensions.Tween(
                () => specialChecksScale,
                value => {
                    specialChecksScale = value;
                    if(specialChecks != null) specialChecks.localScale = new Vector3(value, 1f, 1f);
                },
                specialExpanded ? 1f : 0.82f,
                specialExpanded ? 0.42f : 0.18f)
                .SetEasing(specialExpanded ? Easing.OutBack : Easing.OutSine))
            .Build();
        MainCore.TC.Play(specialArrowSeq);
    }
    private void ApplyFilterLayout() {
        if(viewport == null || quantumRow == null) return;
        float qShift = (1f - quantumLayout) * 64f;
        quantumRow.offsetMin = new(0f, -256f + qShift);
        viewport.offsetMax = new(0f, -266f + qShift);
    }
    private void AnimateFilterLayout() {
        filterLayoutSeq?.Kill();
        filterLayoutSeq = GTweenSequenceBuilder.New()
            .Join(GTweens.Extensions.GTweenExtensions.Tween(
                () => quantumLayout,
                x => { quantumLayout = x; ApplyFilterLayout(); },
                lastQuantumOn ? 1f : 0f,
                0.16f).SetEasing(Easing.OutSine))
            .Build();
        MainCore.TC.Play(filterLayoutSeq);
    }
    private void AddDifficultyCheckbox(Transform parent, string name, string key, string label, float width) {
        RectTransform cell = Rect("Check " + name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = cell.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        Image cellBg = cell.gameObject.AddComponent<Image>();
        cellBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        cellBg.type = Image.Type.Sliced;
        cellBg.color = new(1f, 1f, 1f, 0.05f);
        RectTransform box = Rect("Box", cell, new(0f, 0.5f), new(0f, 0.5f), Vector2.zero, Vector2.zero);
        box.sizeDelta = new(18f, 18f);
        box.anchoredPosition = new(19f, 0f);
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        boxImage.type = Image.Type.Sliced;
        boxImage.color = new(1f, 1f, 1f, 0.5f);
        boxImage.raycastTarget = false;
        RectTransform fill = Rect("Fill", box, Vector2.zero, Vector2.one, new(4f, 4f), new(-4f, -4f));
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        fillImage.type = Image.Type.Sliced;
        fillImage.color = new(1f, 1f, 1f, 0f);
        fillImage.raycastTarget = false;
        TMP_Text text = Text(cell, label, 14f, TextAlignmentOptions.Left);
        text.rectTransform.offsetMin = new(38f, 0f);
        text.rectTransform.offsetMax = new(-10f, 0f);
        text.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(text);
        text.gameObject.AddComponent<TextLocalization>().Init(key, label);
        GenerateUI.AddButton(cell.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) service.ToggleSpecialDifficulty(name);
        });
        cell.AddToolTip(name);
        difficultyChips.Add((name, fillImage));
    }
    private void BuildSearch(Transform parent) {
        RectTransform bg = Rect("Search", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = bg.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 170f;
        size.flexibleWidth = 1f;
        Image image = bg.gameObject.AddComponent<Image>();
        image.color = UIColors.ObjectBG;
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        search = bg.gameObject.AddComponent<TMP_InputField>();
        RectTransform textArea = Rect("Text Area", bg, Vector2.zero, Vector2.one, new(16f, 0f), new(-40f, 0f));
        textArea.gameObject.AddComponent<RectMask2D>();
        TMP_Text value = Text(textArea, "", 17f, TextAlignmentOptions.Left);
        TextCompat.NoWrap(value);
        TMP_Text placeholder = Text(textArea, "Search levels…", 17f, TextAlignmentOptions.Left);
        TextCompat.NoWrap(placeholder);
        placeholder.color = new(1f, 1f, 1f, 0.28f);
        placeholder.gameObject.AddComponent<TextLocalization>().Init("TUF_SEARCH_PLACEHOLDER", "Search levels…");
        GameObject iconObj = new("Search Icon");
        iconObj.transform.SetParent(bg, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new(1f, 0.5f);
        iconRect.sizeDelta = new(22f, 22f);
        iconRect.anchoredPosition = new(-17f, 0f);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = MainCore.Spr.Get(UISprite.MagnifyingGlass128);
        icon.color = new(1f, 1f, 1f, 0.25f);
        search.textViewport = textArea;
        search.textComponent = value as TextMeshProUGUI;
        search.placeholder = placeholder as TextMeshProUGUI;
        search.lineType = TMP_InputField.LineType.SingleLine;
        search.richText = false;
        search.characterLimit = 128;
        search.SetTextWithoutNotify(service.Query);
        search.onValueChanged.AddListener(text => {
            service.SetQuery(text);
            if(string.IsNullOrEmpty(text)) ResetSearchScroll();
        });
        search.onDeselect.AddListener(_ => ResetSearchScroll());
    }
    private void ResetSearchScroll() {
        if(search == null || search.textComponent == null) return;
        search.textComponent.rectTransform.anchoredPosition =
            new(0f, search.textComponent.rectTransform.anchoredPosition.y);
    }
    private void AddSortChip(Transform parent, TufSort sort, string key, string label, float width) {
        (Image image, TMP_Text text) = Chip(parent, label, width, () => service.SetSort(sort));
        text.gameObject.AddComponent<TextLocalization>().Init(key, label);
        sortChips.Add((sort, image));
    }
    private void RefreshControls() {
        foreach((TufSort sort, Image image) in sortChips)
            image.color = sort == service.Sort ? UIColors.ObjectActive : UIColors.ObjectBG;
        if(installedChip != null)
            installedChip.color = service.ShowInstalled ? UIColors.ObjectActive : UIColors.ObjectBG;
        if(gridChip != null)
            gridChip.color = service.GridView ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionChip.color = service.Ascending ? UIColors.ObjectActive : UIColors.ObjectBG;
        directionLabel.text = service.Ascending ? "↑" : "↓";
        difficultyRange?.SetRange(service.MinDifficultyIndex, service.MaxDifficultyIndex);
        quantumRange?.SetQuantum(service.QuantumEnabled, service.QuantumMinIndex, service.QuantumMaxIndex);
        if(service.QuantumEnabled != lastQuantumOn) {
            lastQuantumOn = service.QuantumEnabled;
            AnimateFilterLayout();
        }
        foreach((string name, Image fill) in difficultyChips)
            fill.color = service.DifficultyFilter.IsSelected(name)
                ? UIColors.ObjectActive : new Color(1f, 1f, 1f, 0f);
    }
}
