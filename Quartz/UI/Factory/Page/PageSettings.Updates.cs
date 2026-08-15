using GTweens.Builders;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Tweens;
using Quartz.Async;
using Quartz.Core;
using Quartz.Core.Service;
using Quartz.IO;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using Quartz.Utility;
using Quartz.Update;
using UnityEngine;
using UnityEngine.UI;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
namespace Quartz.UI.Factory.Page;
internal static partial class PageSettings {
    private static void CreateUpdatesSection(RectTransform content, CoreSettings defSet) {
        var updatesLabelRow = GenerateUI.Row(content.transform);
        var updatesText = GenerateUI.AddTextH1(updatesLabelRow);
        var updatesTextTr = updatesText.gameObject.AddComponent<TextLocalization>().Init("UPDATES", "Updates");
        updatesAnchor = updatesLabelRow;
        ReleaseChannel[] channels = [
            ReleaseChannel.Stable,
            ReleaseChannel.Beta,
            ReleaseChannel.Alpha,
        ];
        var channelRow = GenerateUI.Row(content.transform);
        UIDropDown<ReleaseChannel> channelDropdown = GenerateUI.DropDown(
            channelRow,
            ReleaseChannel.Stable,
            MainCore.Conf.GetUpdateChannel(),
            channels,
            ch => ch switch {
                ReleaseChannel.Stable => MainCore.Tr.Get("UPDATE_CHANNEL_STABLE", "Stable"),
                ReleaseChannel.Beta => MainCore.Tr.Get("UPDATE_CHANNEL_BETA", "Beta"),
                ReleaseChannel.Alpha => MainCore.Tr.Get("UPDATE_CHANNEL_ALPHA", "Alpha"),
                _ => ch.ToString(),
            },
            ch => {
                MainCore.Conf.UpdateChannel = (int)ch;
                MainCore.ConfMgr.RequestSave();
                Update.UpdateLaunchPrefs.Write();
            },
            "update_channel"
        );
        channelDropdown.Rect.AddToolTip(
            "DESC_UPDATE_CHANNEL",
            "Which builds to receive when updating. Alpha gets new builds first and each step up is more stable, with Stable being only final releases. Updates stay in the channel you pick, so Alpha offers the newest alpha rather than a beta of the same version."
        );
        var updateCheckRow = GenerateUI.Row(content.transform);
        updateCheckButton = GenerateUI.Button(
            updateCheckRow,
            () => UpdateService.Check(),
            "Check for Updates",
            "update_check"
        );
        updateCheckButton.Label.gameObject.AddComponent<TextLocalization>().Init("CHECK_FOR_UPDATES", "Check for Updates");
        var updateStatusRow = GenerateUI.Row(content.transform);
        updateStatusText = GenerateUI.AddText(updateStatusRow, noPad: true);
        updateStatusText.text = "";
        {
            var progressRect = GenerateUI.Row(content.transform, 32f);
            updateProgressRow = progressRect.gameObject;
            GameObject track = new("ProgressTrack");
            track.transform.SetParent(progressRect, false);
            RectTransform trackRect = track.AddComponent<RectTransform>();
            trackRect.anchorMin = new(0f, 0.5f);
            trackRect.anchorMax = new(1f, 0.5f);
            trackRect.pivot = new(0f, 0.5f);
            trackRect.offsetMin = new(0f, -7f);
            trackRect.offsetMax = new(-250f, 7f);
            Image trackImg = track.AddComponent<Image>();
            trackImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            trackImg.type = Image.Type.Sliced;
            trackImg.color = UIColors.ObjectBG;
            trackImg.raycastTarget = false;
            GameObject fill = new("ProgressFill");
            fill.transform.SetParent(track.transform, false);
            updateProgressFill = fill.AddComponent<RectTransform>();
            updateProgressFill.anchorMin = Vector2.zero;
            updateProgressFill.anchorMax = new(0f, 1f);
            updateProgressFill.offsetMin = Vector2.zero;
            updateProgressFill.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = UIColors.ObjectActive;
            fillImg.raycastTarget = false;
            GameObject pctObj = new("ProgressPercent");
            pctObj.transform.SetParent(progressRect, false);
            RectTransform pctRect = pctObj.AddComponent<RectTransform>();
            pctRect.anchorMin = new(1f, 0f);
            pctRect.anchorMax = new(1f, 1f);
            pctRect.pivot = new(0f, 0.5f);
            pctRect.anchoredPosition = new(-238f, 0f);
            pctRect.sizeDelta = new(90f, 0f);
            updateProgressLabel = pctObj.AddComponent<TextMeshProUGUI>();
            updateProgressLabel.font = FontManager.Current;
            updateProgressLabel.fontSize = 18f;
            updateProgressLabel.color = Color.white;
            updateProgressLabel.alignment = TextAlignmentOptions.Left;
            updateProgressLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            updateProgressLabel.raycastTarget = false;
            updateProgressRow.SetActive(false);
        }
        var updateActionRect = GenerateUI.Row(content.transform);
        updateActionRow = updateActionRect.gameObject;
        GenerateUI.ButtonRow(updateActionRect, 0f);
        updateVersionText = GenerateUI.AddText(updateActionRect, true);
        updateVersionText.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement versionLe = updateVersionText.gameObject.AddComponent<LayoutElement>();
        versionLe.flexibleWidth = 1f;
        var updateButtonRect = GenerateUI.Row(content.transform);
        updateButtonRow = updateButtonRect.gameObject;
        GenerateUI.ButtonRow(updateButtonRect);
        updateNotesButton = GenerateUI.Button(
            updateButtonRect,
            () => {
                string url = UpdateService.Available?.Url;
                if(!string.IsNullOrEmpty(url)) Application.OpenURL(url);
            },
            "Notes",
            "update_notes"
        ).SetSecondary();
        GenerateUI.FixWidth(updateNotesButton, 100f);
        updateNotesButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_NOTES", "Notes");
        updateNotesButton.Rect.AddToolTip(
            "DESC_UPDATE_NOTES",
            "Opens this release's notes on GitHub."
        );
        updateSkipButton = GenerateUI.Button(
            updateButtonRect,
            () => UpdateService.Skip(UpdateService.Available),
            "Skip",
            "update_skip"
        ).SetSecondary();
        GenerateUI.FixWidth(updateSkipButton, 100f);
        updateSkipButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_SKIP", "Skip");
        updateSkipButton.Rect.AddToolTip(
            "DESC_UPDATE_SKIP",
            "Hides this version. You'll still be offered the next release."
        );
        updateInstallButton = GenerateUI.Button(
            updateButtonRect,
            () => UpdateService.Install(UpdateService.Available),
            "Install",
            "update_install"
        );
        GenerateUI.FixWidth(updateInstallButton, 130f);
        updateInstallButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_INSTALL", "Install");
        updateUndoButton = GenerateUI.Button(
            updateButtonRect,
            () => UpdateService.UndoSkip(),
            "Undo",
            "update_undo"
        ).SetSecondary();
        GenerateUI.FixWidth(updateUndoButton, 100f);
        updateUndoButton.Label.gameObject.AddComponent<TextLocalization>().Init("UPDATE_UNDO", "Undo");
        updateRestartButton = GenerateUI.Button(
            updateButtonRect,
            RestartGame,
            "Restart Game",
            "update_restart"
        );
        GenerateUI.FixWidth(updateRestartButton, 170f);
        updateRestartButton.Rect.AddToolTip(
            "DESC_UPDATE_RESTART",
            "Closes the game and starts it again — through Steam when the game was launched from Steam, otherwise by relaunching it directly."
        );
        if(!updateHooked) {
            UpdateService.OnChanged += RefreshUpdates;
            MainCore.Tr.OnLanguageChanged += _ => RefreshUpdates();
            updateHooked = true;
        }
        RefreshUpdates();
    }
}
