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
    private void Rebuild() {
        if(this == null || !built || content == null || service == null) return;
        if(!gameObject.activeInHierarchy) {
            pendingRebuild = true;
            return;
        }
        string signature = BuildSignature();
        if(signature == listSignature && cardLabels.Count > 0) {
            foreach(TufLevel level in service.Levels) {
                if(!cardLabels.TryGetValue(level.Id, out TMP_Text label) || label == null) continue;
                string text = ActionLabel(level);
                if(label.text != text) label.text = text;
            }
            RefreshControls();
            return;
        }
        listSignature = signature;
        float oldY = content.anchoredPosition.y;
        RefreshControls();
        chartChooserSeq?.Kill();
        previews.ClearSlots();
        GenerateUI.ClearChildren(content);
        cardLabels.Clear();
        deleteChips.Clear();
        if(service.State == TufListState.Loading) {
            AddLoadingStatus(Tr("TUF_LOADING", "Loading levels…"));
        } else if(service.State == TufListState.Error && service.Levels.Count == 0) {
            if(service.OfflineError && !service.ShowInstalled) AddOfflineStatus();
            else AddStatus(Tr("TUF_API_ERROR", "Could not load TUF levels.") + "\n" + service.Error, true, service.Refresh);
        } else if(service.State == TufListState.Empty) {
            AddStatus(EmptyMessage(), false, null);
        } else {
            AddLevelCards();
            if(service.HasMore) {
                if(service.LoadingMore) AddLoadingStatus(Tr("TUF_LOADING", "Loading levels…"));
                else if(service.State == TufListState.Error) AddStatus(Tr("TUF_RETRY", "Retry"), true, service.LoadMore);
            }
            else if(service.Levels.Count > 0)
                AddStatus(service.ShowInstalled
                    ? string.Format(Tr("TUF_INSTALLED_COUNT", "{0} level(s) in your library"), service.Levels.Count)
                    : Tr("TUF_END", "End of results"), false, null, 38f);
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.ScrollTo(oldY);
    }
    private string BuildSignature() {
        StringBuilder sb = new();
        sb.Append((int)service.State).Append('|')
            .Append(service.HasMore ? '1' : '0')
            .Append(service.LoadingMore ? '1' : '0')
            .Append(service.IsBusy ? '1' : '0')
            .Append(service.ShowInstalled ? '1' : '0')
            .Append(service.OfflineError ? '1' : '0')
            .Append(service.ShowPreviews ? '1' : '0').Append('|')
            .Append(service.GridView ? 'G' : 'L').Append(gridColumns).Append('|')
            .Append(service.InfoRevision).Append('|');
        foreach(TufLevel level in service.Levels)
            sb.Append(level.Id).Append(':').Append((int)level.State)
                .Append(level.InstallFolder == null ? '-' : '+')
                .Append(string.IsNullOrEmpty(level.Error) ? '-' : '!')
                .Append('#').Append(level.Charts?.Count ?? 0).Append(',');
        return sb.ToString();
    }
    private string EmptyMessage() {
        if(!service.ShowInstalled) return Tr("TUF_EMPTY", "No levels matched your search.");
        return string.IsNullOrEmpty(service.Query)
            ? Tr("TUF_INSTALLED_EMPTY", "You have not downloaded any levels yet.")
            : Tr("TUF_INSTALLED_NO_MATCH", "No downloaded level matched your search.");
    }
    private void AddLevelCards() {
        if(!service.GridView) {
            foreach(TufLevel level in service.Levels) {
                AddCard(level);
                if(level.State == TufItemState.ChooseChart && level.Charts != null) AddChartChooser(level);
            }
            return;
        }
        gridColumns = ComputeColumns();
        List<TufLevel> chunk = [];
        foreach(TufLevel level in service.Levels) {
            chunk.Add(level);
            if(chunk.Count >= gridColumns) FlushGridRow(chunk);
        }
        if(chunk.Count > 0) FlushGridRow(chunk);
    }
    private void FlushGridRow(List<TufLevel> chunk) {
        RectTransform row = FixedRow("Grid Row", GridCardHeight);
        AddHorizontal(row, GridGap);
        foreach(TufLevel level in chunk) AddGridCard(row, level);
        for(int i = chunk.Count; i < gridColumns; i++) AddFlexibleSpacer(row);
        foreach(TufLevel level in chunk)
            if(level.State == TufItemState.ChooseChart && level.Charts != null) AddChartChooser(level);
        chunk.Clear();
    }
    private int ComputeColumns() {
        if(viewport == null) return gridColumns;
        float width = viewport.rect.width;
        if(width < 1f) return gridColumns;
        return Mathf.Clamp(Mathf.FloorToInt((width + GridGap) / (GridMinWidth + GridGap)), 1, 6);
    }
    private void AddGridCard(Transform parent, TufLevel level) {
        RectTransform card = Rect("Level " + level.Id, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 0f;
        size.flexibleWidth = 1f;
        Image bg = card.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.12f);
        if(service.ShowPreviews) previews.Attach(card, level.Id.ToString(), TufPreviewSource.Video(level.VideoLink));
        RectTransform rail = Rect("Difficulty Rail", card, new(0f, 0f), new(0f, 1f), new(5f, 8f), new(11f, -8f));
        Image railImage = rail.gameObject.AddComponent<Image>();
        railImage.sprite = MainCore.Spr.GetFilled(2f);
        railImage.type = Image.Type.Sliced;
        railImage.color = ColorUtility.TryParseHtmlString(level.DifficultyColor, out Color color) ? color : Color.white;
        float x = 20f;
        TMP_Text id = MetaLabel(card, "Id", $"#{level.Id}", ref x, 78f);
        id.color = new(1f, 1f, 1f, 0.48f);
        x += MetaGap;
        TMP_Text diff = MetaLabel(card, "Difficulty", level.Difficulty, ref x, 128f);
        diff.color = railImage.color;
        RectTransform songRect = Rect("Song", card, new(0f, 1f), new(1f, 1f), new(20f, -74f), new(-16f, -40f));
        string song = string.IsNullOrEmpty(level.Song) ? Tr("TUF_UNKNOWN_LEVEL", "Level") + " #" + level.Id : level.Song;
        TMP_Text songText = Text(songRect, song, 20f, TextAlignmentOptions.Left);
        songText.fontStyle = FontStyles.Bold;
        songText.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(songText);
        bool known = !string.IsNullOrEmpty(level.Artist) || !string.IsNullOrEmpty(level.Creator);
        RectTransform creditRect = Rect("Credits", card, new(0f, 1f), new(1f, 1f), new(20f, -98f), new(-16f, -76f));
        TMP_Text credit = Text(creditRect,
            known ? $"{level.Artist}  ·  {level.Creator}" : Tr("TUF_INSTALLED_UNKNOWN", "Downloaded before Quartz tracked level details."),
            14f, TextAlignmentOptions.Left);
        credit.color = new(1f, 1f, 1f, 0.46f);
        credit.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(credit);
        if(known) {
            RectTransform statRect = Rect("Stats", card, new(0f, 1f), new(1f, 1f), new(20f, -120f), new(-16f, -98f));
            TMP_Text stats = Text(statRect, $"✓ {level.Clears:N0}    ♥ {level.Likes:N0}", 14f, TextAlignmentOptions.Left);
            stats.color = new(1f, 1f, 1f, 0.46f);
            stats.overflowMode = TextOverflowModes.Ellipsis;
            TextCompat.NoWrap(stats);
        }
        bool installed = IsInstalled(level);
        BuildAction(Rect("Action", card, new(0f, 0f), new(1f, 0f), new(20f, 14f), new(installed ? -66f : -16f, 48f)), level);
        if(installed) BuildDelete(Rect("Delete", card, new(1f, 0f), new(1f, 0f), new(-58f, 14f), new(-16f, 48f)), level);
    }
    private void AddCard(TufLevel level) {
        RectTransform card = FixedRow("Level " + level.Id, 94f);
        Image bg = card.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.12f);
        if(service.ShowPreviews) previews.Attach(card, level.Id.ToString(), TufPreviewSource.Video(level.VideoLink));
        RectTransform rail = Rect("Difficulty Rail", card, new(0f, 0f), new(0f, 1f), new(5f, 8f), new(11f, -8f));
        Image railImage = rail.gameObject.AddComponent<Image>();
        railImage.sprite = MainCore.Spr.GetFilled(2f);
        railImage.type = Image.Type.Sliced;
        railImage.color = ColorUtility.TryParseHtmlString(level.DifficultyColor, out Color color) ? color : Color.white;
        float x = 22f;
        TMP_Text id = MetaLabel(card, "Id", $"#{level.Id}", ref x, 90f);
        id.color = new(1f, 1f, 1f, 0.48f);
        x += MetaGap;
        TMP_Text diff = MetaLabel(card, "Difficulty", level.Difficulty, ref x, 150f);
        diff.color = railImage.color;
        bool installed = IsInstalled(level);
        if(installed) AddInstalledBadge(card, x + MetaGap);
        float textRight = installed ? -204f : -150f;
        RectTransform songRect = Rect("Song", card, new(0f, 1f), new(1f, 1f), new(22f, -66f), new(textRight, -34f));
        string song = string.IsNullOrEmpty(level.Song) ? Tr("TUF_UNKNOWN_LEVEL", "Level") + " #" + level.Id : level.Song;
        TMP_Text songText = Text(songRect, song, 23f, TextAlignmentOptions.Left);
        songText.fontStyle = FontStyles.Bold;
        songText.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(songText);
        RectTransform metaRect = Rect("Metadata", card, new(0f, 0f), new(1f, 0f), new(22f, 8f), new(textRight, 34f));
        TMP_Text meta = Text(metaRect, CardMeta(level), 15f, TextAlignmentOptions.Left);
        meta.color = new(1f, 1f, 1f, 0.46f);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(meta);
        AddAction(card, level);
        if(installed) AddDelete(card, level);
    }
    private string CardMeta(TufLevel level) {
        if(string.IsNullOrEmpty(level.Artist) && string.IsNullOrEmpty(level.Creator))
            return Tr("TUF_INSTALLED_UNKNOWN", "Downloaded before Quartz tracked level details.");
        return $"{level.Artist}  ·  {level.Creator}  ·  ✓ {level.Clears:N0}  ♥ {level.Likes:N0}";
    }
    private bool IsInstalled(TufLevel level) =>
        level.InstallFolder != null
        && level.State is not TufItemState.Downloading and not TufItemState.Extracting
            and not TufItemState.Loading;
    private static TMP_Text MetaLabel(RectTransform card, string name, string value, ref float x, float maxWidth) {
        RectTransform rect = Rect(name, card, new(0f, 1f), new(0f, 1f), new(x, -35f), new(x, -8f));
        TMP_Text text = Text(rect, value, 16f, TextAlignmentOptions.Left);
        text.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(text);
        float width = Mathf.Min(Mathf.Ceil(text.GetPreferredValues(value).x), maxWidth);
        rect.offsetMax = new(x + width, -8f);
        x += width;
        return text;
    }
    private void AddInstalledBadge(RectTransform card, float x) {
        string value = Tr("TUF_INSTALLED", "Installed");
        RectTransform badge = Rect("Installed Badge", card, new(0f, 1f), new(0f, 1f), new(x, -33f), new(x, -10f));
        Image image = badge.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        image.color = new(0.38f, 0.78f, 0.52f, 0.22f);
        image.raycastTarget = false;
        TMP_Text label = Text(badge, value, 13f, TextAlignmentOptions.Center);
        label.color = new(0.62f, 0.92f, 0.72f, 0.95f);
        label.raycastTarget = false;
        badge.offsetMax = new(x + Mathf.Ceil(label.GetPreferredValues(value).x) + 22f, -10f);
    }
}
