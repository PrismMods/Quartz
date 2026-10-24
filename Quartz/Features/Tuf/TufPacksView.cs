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
internal sealed partial class TufPacksView : MonoBehaviour {
    private TufPackService service;
    private RectTransform content;
    private RectTransform viewport;
    private UIScrollController scroll;
    private TMP_InputField search;
    private readonly List<(TufPackSort Sort, Image Image)> sortChips = [];
    private Image directionChip;
    private TMP_Text directionLabel;
    private readonly Dictionary<int, TMP_Text> cardLabels = [];
    private readonly HashSet<long> expandedFolders = [];
    private TufPreviewGroup previews;
    private static bool ShowPreviews => TufService.Instance?.ShowPreviews ?? true;
    private string expandedPackId;
    private GTween chartChooserSeq;
    private GTween viewSwitchSeq;
    private CanvasGroup contentCg;
    private bool lastDetailView;
    private float listScrollY;
    private string listSignature;
    private bool built;
    private bool pendingRebuild;
    private void OnEnable() {
        if(!pendingRebuild) return;
        pendingRebuild = false;
        Rebuild();
    }
    public void Build(RectTransform parent) {
        service = TufPackService.Instance;
        if(service == null) return;
        RectTransform pad = Rect("TUF Packs", parent, Vector2.zero, Vector2.one, new(18f, 18f), new(-18f, -18f));
        BuildHeader(pad);
        viewport = Rect("Pack Viewport", pad, Vector2.zero, Vector2.one, Vector2.zero, new(0f, -138f));
        viewport.gameObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Pack Cards", viewport, new(0f, 1f), new(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new(0.5f, 1f);
        contentCg = content.gameObject.AddComponent<CanvasGroup>();
        GenerateUI.FitVertical(content.gameObject, 8f);
        scroll = pad.gameObject.AddComponent<UIScrollController>();
        scroll.SetContent(content, viewport);
        built = true;
        previews = new TufPreviewGroup();
        service.Changed += Rebuild;
        service.EnsureLoaded();
        Rebuild();
    }
    private void BuildHeader(RectTransform parent) {
        RectTransform titleRect = Rect("Title", parent, new(0f, 1f), new(1f, 1f), new(0f, -30f), Vector2.zero);
        TMP_Text title = Text(titleRect, "Packs", 28f, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<TextLocalization>().Init("TUF_PACKS", "Packs");
        RectTransform taglineRect = Rect("Tagline", titleRect, new(0f, 0f), new(1f, 1f), new(110f, 4f), new(0f, 0f));
        TMP_Text tagline = Text(taglineRect, "Browse level packs, open one, then load its levels.", 14f, TextAlignmentOptions.Left);
        tagline.color = new(1f, 1f, 1f, 0.42f);
        tagline.gameObject.AddComponent<TextLocalization>().Init("TUF_PACKS_TAGLINE", tagline.text);
        RectTransform searchRow = Rect("Search Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -78f), new(0f, -42f));
        AddHorizontal(searchRow);
        BuildSearch(searchRow);
        (Image refresh, TMP_Text refreshLabel) = Chip(searchRow, "Refresh", 92f, service.RefreshPacks);
        refreshLabel.gameObject.AddComponent<TextLocalization>().Init("TUF_REFRESH", "Refresh");
        RectTransform sortRow = Rect("Sort Controls", parent, new(0f, 1f), new(1f, 1f), new(0f, -126f), new(0f, -90f));
        AddHorizontal(sortRow);
        AddSortChip(sortRow, TufPackSort.Recent, "TUF_SORT_RECENT", "Recent", 76f);
        AddSortChip(sortRow, TufPackSort.Name, "TUF_PACK_SORT_NAME", "Name", 64f);
        AddSortChip(sortRow, TufPackSort.Levels, "TUF_PACK_SORT_LEVELS", "Levels", 72f);
        (directionChip, directionLabel) = Chip(sortRow, "↓", 48f, service.ToggleAscending);
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
        TMP_Text placeholder = Text(textArea, "Search packs…", 17f, TextAlignmentOptions.Left);
        TextCompat.NoWrap(placeholder);
        placeholder.color = new(1f, 1f, 1f, 0.28f);
        placeholder.gameObject.AddComponent<TextLocalization>().Init("TUF_PACK_SEARCH_PLACEHOLDER", "Search packs…");
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
    private void Update() {
        if(!built || service == null || content == null || viewport == null) return;
        previews?.Tick();
        if(service.SelectedPack != null) return;
        if(!service.HasMore || service.LoadingMore || service.ListState != TufPackListState.Ready) return;
        float max = content.rect.height - viewport.rect.height;
        if(max <= 0f || content.anchoredPosition.y >= max - 400f) service.LoadMore();
    }
    private void ResetSearchScroll() {
        if(search == null || search.textComponent == null) return;
        search.textComponent.rectTransform.anchoredPosition =
            new(0f, search.textComponent.rectTransform.anchoredPosition.y);
    }
    private void AddSortChip(Transform parent, TufPackSort sort, string key, string label, float width) {
        (Image image, TMP_Text text) = Chip(parent, label, width, () => service.SetSort(sort));
        text.gameObject.AddComponent<TextLocalization>().Init(key, label);
        sortChips.Add((sort, image));
    }
    private void OnDestroy() {
        viewSwitchSeq?.Kill();
        chartChooserSeq?.Kill();
        previews?.Dispose();
        if(service != null) service.Changed -= Rebuild;
    }
}
