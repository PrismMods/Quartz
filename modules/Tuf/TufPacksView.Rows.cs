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
    private void AddLevelSortRow() {
        RectTransform row = FixedRow("Level Sort", 36f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        AddLevelSortChip(row, TufPackLevelSort.PackOrder, "TUF_PACK_SORT_ORDER", "Pack Order", 108f);
        AddLevelSortChip(row, TufPackLevelSort.Difficulty, "TUF_SORT_DIFFICULTY", "Difficulty", 92f);
        AddLevelSortChip(row, TufPackLevelSort.Clears, "TUF_SORT_CLEARS", "Clears", 70f);
        (Image direction, TMP_Text directionText) = Chip(row, service.LevelAscending ? "↑" : "↓", 48f, service.ToggleLevelAscending);
        direction.color = service.LevelAscending ? UIColors.ObjectBG : UIColors.ObjectActive;
        directionText.color = new(1f, 1f, 1f, service.LevelSort == TufPackLevelSort.PackOrder ? 0.35f : 1f);
    }
    private void AddLevelSortChip(Transform parent, TufPackLevelSort sort, string key, string label, float width) {
        (Image image, TMP_Text text) = Chip(parent, label, width, () => service.SetLevelSort(sort));
        text.gameObject.AddComponent<TextLocalization>().Init(key, label);
        image.color = sort == service.LevelSort ? UIColors.ObjectActive : UIColors.ObjectBG;
    }
    private void AddFolderRow(TufPackItem folder, float indent) {
        bool expanded = expandedFolders.Contains(folder.Key);
        RectTransform row = IndentedRow("Folder " + folder.Name, 52f, indent);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.ObjectBG;
        RectTransform arrowRect = Rect("Arrow", row, new(0f, 0.5f), new(0f, 0.5f), Vector2.zero, Vector2.zero);
        arrowRect.sizeDelta = new(16f, 16f);
        arrowRect.anchoredPosition = new(22f, 0f);
        arrowRect.localEulerAngles = new(0f, 0f, expanded ? 0f : 90f);
        Image arrow = arrowRect.gameObject.AddComponent<Image>();
        arrow.sprite = MainCore.Spr.Get(UISprite.Triangle128);
        arrow.color = expanded ? UIColors.ObjectActive : UIColors.ObjectInactive;
        arrow.raycastTarget = false;
        TMP_Text name = Text(row, folder.Name, 18f, TextAlignmentOptions.Left);
        name.rectTransform.offsetMin = new(46f, 0f);
        name.rectTransform.offsetMax = new(-140f, 0f);
        name.fontStyle = FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(name);
        TMP_Text count = Text(row, string.Format(Tr("TUF_PACK_LEVEL_COUNT", "{0} levels"), folder.LevelCount), 14f, TextAlignmentOptions.Right);
        count.rectTransform.offsetMax = new(-18f, 0f);
        count.color = new(1f, 1f, 1f, 0.46f);
        GenerateUI.AddButton(row.gameObject, input => {
            if(input != PointerEventData.InputButton.Left) return;
            if(!expandedFolders.Add(folder.Key)) expandedFolders.Remove(folder.Key);
            listSignature = null;
            Rebuild();
        });
    }
    private RectTransform IndentedRow(string name, float height, float indent) {
        RectTransform row = FixedRow(name, height);
        if(indent <= 0f) return row;
        return Rect(name + " Inner", row, Vector2.zero, Vector2.one, new(indent, 0f), Vector2.zero);
    }
    private void AddBackRow(TufPack pack) {
        RectTransform row = FixedRow("Back", 52f);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.ObjectBG;
        TMP_Text back = Text(row, "←", 22f, TextAlignmentOptions.Left);
        back.rectTransform.offsetMin = new(20f, 0f);
        back.rectTransform.offsetMax = new(-20f, 0f);
        TMP_Text name = Text(row, pack.Name, 19f, TextAlignmentOptions.Left);
        name.rectTransform.offsetMin = new(52f, 0f);
        name.rectTransform.offsetMax = new(-180f, 0f);
        name.fontStyle = FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(name);
        TMP_Text count = Text(row, string.Format(Tr("TUF_PACK_LEVEL_COUNT", "{0} levels"), pack.LevelCount), 15f, TextAlignmentOptions.Right);
        count.rectTransform.offsetMin = new(0f, 0f);
        count.rectTransform.offsetMax = new(-20f, 0f);
        count.color = new(1f, 1f, 1f, 0.46f);
        GenerateUI.AddButton(row.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) service.ClosePack();
        });
    }
    private void AddPackCard(TufPack pack) {
        RectTransform card = FixedRow("Pack " + pack.Id, 88f);
        Image bg = card.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.12f);
        if(ShowPreviews) previews.Attach(card, "pack-" + pack.Id, TufPreviewSource.ForPack(pack.IconUrl, pack.FirstLevelId));
        RectTransform nameRect = Rect("Name", card, new(0f, 1f), new(1f, 1f), new(22f, -46f), new(-22f, -12f));
        TMP_Text name = Text(nameRect, pack.Name, 22f, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(name);
        string preview = pack.Preview.Count > 0 ? "  ·  " + string.Join(", ", pack.Preview) : "";
        RectTransform metaRect = Rect("Metadata", card, new(0f, 0f), new(1f, 0f), new(22f, 10f), new(-22f, 46f));
        TMP_Text meta = Text(metaRect,
            string.Format(Tr("TUF_PACK_LEVEL_COUNT", "{0} levels"), pack.LevelCount)
                + $"  ·  {pack.Owner}  ·  ♥ {pack.Favorites:N0}" + preview,
            15f, TextAlignmentOptions.Left);
        meta.color = new(1f, 1f, 1f, 0.46f);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(meta);
        GenerateUI.AddButton(card.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) service.OpenPack(pack);
        });
    }
    private void AddLevelCard(TufLevel level, float indent) {
        RectTransform card = IndentedRow("Level " + level.Id, 94f, indent);
        Image bg = card.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.12f);
        if(ShowPreviews) previews.Attach(card, level.Id.ToString(), TufPreviewSource.Video(level.VideoLink));
        RectTransform rail = Rect("Difficulty Rail", card, new(0f, 0f), new(0f, 1f), new(5f, 8f), new(11f, -8f));
        Image railImage = rail.gameObject.AddComponent<Image>();
        railImage.sprite = MainCore.Spr.GetFilled(2f);
        railImage.type = Image.Type.Sliced;
        railImage.color = ColorUtility.TryParseHtmlString(level.DifficultyColor, out Color color) ? color : Color.white;
        RectTransform idRect = Rect("Id", card, new(0f, 1f), new(0f, 1f), new(22f, -35f), new(108f, -8f));
        TMP_Text id = Text(idRect, $"#{level.Id}", 16f, TextAlignmentOptions.Left);
        id.color = new(1f, 1f, 1f, 0.48f);
        RectTransform diffRect = Rect("Difficulty", card, new(0f, 1f), new(0f, 1f), new(104f, -35f), new(235f, -8f));
        TMP_Text diff = Text(diffRect, level.Difficulty, 16f, TextAlignmentOptions.Left);
        diff.color = railImage.color;
        RectTransform songRect = Rect("Song", card, new(0f, 1f), new(1f, 1f), new(22f, -66f), new(-150f, -34f));
        TMP_Text song = Text(songRect, level.Song, 23f, TextAlignmentOptions.Left);
        song.fontStyle = FontStyles.Bold;
        song.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(song);
        RectTransform metaRect = Rect("Metadata", card, new(0f, 0f), new(1f, 0f), new(22f, 8f), new(-150f, 34f));
        TMP_Text meta = Text(metaRect, $"{level.Artist}  ·  {level.Creator}  ·  ✓ {level.Clears:N0}  ♥ {level.Likes:N0}", 15f, TextAlignmentOptions.Left);
        meta.color = new(1f, 1f, 1f, 0.46f);
        meta.overflowMode = TextOverflowModes.Ellipsis;
        TextCompat.NoWrap(meta);
        AddAction(card, level);
    }
    private void AddAction(RectTransform card, TufLevel level) {
        RectTransform action = Rect("Action", card, new(1f, 0.5f), new(1f, 0.5f), new(-138f, -23f), new(-10f, 23f));
        Image image = action.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        bool actionable = level.State is not TufItemState.Unavailable and not TufItemState.Downloading
                and not TufItemState.Extracting and not TufItemState.Loading
            || (level.State == TufItemState.Unavailable && TufMainLevel.Resolve(level, out _) != TufMainLevel.TufMainAction.None);
        bool enabled = actionable && !service.IsLaunching;
        image.color = enabled ? UIColors.ObjectButton : Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.25f);
        TMP_Text label = Text(action, ActionLabel(level), 15f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, enabled ? 1f : 0.5f);
        cardLabels[level.Id] = label;
        if(enabled) GenerateUI.AddButton(action.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) service.Act(level);
        });
        if(!string.IsNullOrWhiteSpace(level.Error)) action.AddToolTip(level.Error.Length > 900 ? level.Error[..900] + "…" : level.Error);
    }
    private string ActionLabel(TufLevel level) => level.State switch {
        TufItemState.Downloading => level.Progress < 0
            ? Tr("TUF_DOWNLOADING", "Downloading…")
            : string.Format(Tr("TUF_DOWNLOADING_PROGRESS", "Downloading {0}%"), Mathf.Clamp((int)(level.Progress * 100f), 0, 100)),
        TufItemState.Extracting => Tr("TUF_EXTRACTING", "Extracting…"),
        TufItemState.Loading => Tr("TUF_LOADING_LEVEL", "Loading…"),
        TufItemState.Load => Tr("TUF_LOAD", "Load"),
        TufItemState.Retry => Tr("TUF_RETRY", "Retry"),
        TufItemState.Unavailable => TufMainLevel.Resolve(level, out _) switch {
            TufMainLevel.TufMainAction.Play => Tr("TUF_PLAY", "Play"),
            TufMainLevel.TufMainAction.BuyDlc => Tr("TUF_BUY_DLC", "Buy DLC"),
            _ => Tr("TUF_UNAVAILABLE", "Unavailable"),
        },
        TufItemState.ChooseChart => Tr("TUF_CANCEL", "Cancel"),
        TufItemState.Queued => string.Format(Tr("TUF_QUEUED", "Queued #{0}"), service.QueuePosition(level.Id)),
        _ => Tr("TUF_DOWNLOAD", "Download")
    };
    private void AddChartChooser(TufLevel level, float indent = 0f) {
        if(level?.Charts == null) return;
        GTweenSequenceBuilder animation = GTweenSequenceBuilder.New();
        int index = 0;
        foreach(string chart in level.Charts) {
            string display = ChartDisplayName(level, chart);
            RectTransform row = IndentedRow("Chart " + display, 40f, indent);
            CanvasGroup fade = row.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            bg.type = Image.Type.Sliced;
            bg.color = UIColors.ObjectBG;
            TMP_Text label = Text(row, "▶  " + display, 15f, TextAlignmentOptions.Left);
            label.rectTransform.offsetMin = new(40f, 0f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            TextCompat.NoWrap(label);
            GenerateUI.AddButton(row.gameObject, input => {
                if(input == PointerEventData.InputButton.Left) service.LaunchChart(level, chart);
            });
            float delay = index++ * 0.035f;
            animation.JoinSequence(sequence => {
                if(delay > 0f) sequence.AppendTime(delay);
                sequence.Append(fade.GTAlpha(1f, 0.18f).SetEasing(Easing.OutSine));
            });
        }
        chartChooserSeq = animation.Build();
        MainCore.TC.Play(chartChooserSeq);
    }
    private static string ChartDisplayName(TufLevel level, string chart) {
        try {
            return string.IsNullOrEmpty(level.ChartsRoot)
                ? Path.GetFileName(chart)
                : Path.GetRelativePath(level.ChartsRoot, chart);
        } catch(Exception e) { Diag.Ignore(e); return Path.GetFileName(chart); }
    }
}
