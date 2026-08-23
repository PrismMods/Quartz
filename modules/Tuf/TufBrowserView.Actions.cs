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
    private void AddDelete(RectTransform card, TufLevel level) =>
        BuildDelete(Rect("Delete", card, new(1f, 0.5f), new(1f, 0.5f), new(-192f, -23f), new(-146f, 23f)), level);
    private void BuildDelete(RectTransform button, TufLevel level) {
        Image image = button.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        bool enabled = !service.IsBusy;
        image.color = DeleteColor(armedDeleteId == level.Id, enabled);
        RectTransform iconRect = Rect("Icon", button, new(0.5f, 0.5f), new(0.5f, 0.5f), new(-11f, -11f), new(11f, 11f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = MainCore.Spr.Get(UISprite.Trash128, 22f);
        icon.color = new(1f, 1f, 1f, enabled ? 0.95f : 0.45f);
        icon.raycastTarget = false;
        deleteChips[level.Id] = image;
        if(!enabled) return;
        GenerateUI.AddButton(button.gameObject, input => {
            if(input != PointerEventData.InputButton.Left) return;
            if(armedDeleteId == level.Id) {
                DisarmDelete();
                service.DeleteInstalled(level);
                return;
            }
            armedDeleteId = level.Id;
            armedUntil = Time.unscaledTime + ArmSeconds;
            RefreshDeleteChips();
        });
        button.AddToolTip("DESC_TUF_DELETE", "Delete this level from your library. Click it twice to confirm; the level can be downloaded again.");
    }
    private static Color DeleteColor(bool armed, bool enabled) => armed
        ? new Color(0.86f, 0.31f, 0.33f, 0.92f)
        : Color.Lerp(UIColors.ObjectBG, new Color(0.86f, 0.31f, 0.33f, 1f), enabled ? 0.22f : 0.08f);
    private void DisarmDelete() {
        if(armedDeleteId == 0) return;
        armedDeleteId = 0;
        RefreshDeleteChips();
    }
    private void RefreshDeleteChips() {
        foreach(TufLevel level in service.Levels)
            if(deleteChips.TryGetValue(level.Id, out Image image) && image != null)
                image.color = DeleteColor(armedDeleteId == level.Id, !service.IsBusy);
    }
    private void BuildRollback(RectTransform button, TufLevel level) {
        Image image = button.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        bool enabled = !service.IsBusy;
        image.color = Color.Lerp(UIColors.ObjectBG, UIColors.ObjectButton, enabled ? 0.5f : 0.15f);
        RectTransform iconRect = Rect("Icon", button, new(0.5f, 0.5f), new(0.5f, 0.5f), new(-11f, -11f), new(11f, 11f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = MainCore.Spr.Get(UISprite.ClockRewind128, 22f);
        icon.color = new(1f, 1f, 1f, enabled ? 0.95f : 0.45f);
        icon.raycastTarget = false;
        if(!enabled) return;
        GenerateUI.AddButton(button.gameObject, input => {
            if(input != PointerEventData.InputButton.Left) return;
            DisarmDelete();
            OpenRollback(level);
        });
        button.AddToolTip("DESC_TUF_ROLLBACK",
            "Go back to a version of this level you had before an update. Quartz keeps the last few copies "
            + "in the rollback folder inside your levels folder.");
    }
    private void OpenRollback(TufLevel level) {
        IReadOnlyList<TufSnapshot> snapshots = service.Snapshots(level.Id);
        if(snapshots.Count == 0) {
            service.RefreshSnapshotCounts();
            Rebuild();
            return;
        }
        string title = string.IsNullOrEmpty(level.Song)
            ? Tr("TUF_UNKNOWN_LEVEL", "Level") + " #" + level.Id
            : level.Song + "  ·  #" + level.Id;
        TufRollbackDialog.Show(title, snapshots, stamp => service.RestoreSnapshot(level, stamp));
    }
    private void AddAction(RectTransform card, TufLevel level) =>
        BuildAction(Rect("Action", card, new(1f, 0.5f), new(1f, 0.5f), new(-138f, -23f), new(-10f, 23f)), level);
    private void BuildAction(RectTransform action, TufLevel level) {
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
        if(level.State == TufItemState.Queued) action.AddToolTip("DESC_TUF_QUEUED", "Waiting for the current download to finish. Click to remove it from the queue.");
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
    private void AddChartChooser(TufLevel level) {
        if(level?.Charts == null) return;
        GTweenSequenceBuilder animation = GTweenSequenceBuilder.New();
        int index = 0;
        foreach(string chart in level.Charts) {
            string display = ChartDisplayName(level, chart);
            RectTransform row = FixedRow("Chart " + display, 40f);
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
