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
    private static void CreateFontSection(RectTransform content, CoreSettings defSet) {
        var fontLabelRow = GenerateUI.Row(content.transform);
        var fontText = GenerateUI.AddTextH1(fontLabelRow);
        var fontTextTr = fontText.gameObject.AddComponent<TextLocalization>().Init("FONT", "Font");
        GameObject fontGroup = new("FontGroup");
        fontGroup.transform.SetParent(content.transform, false);
        fontGroup.AddComponent<RectTransform>();
        GenerateUI.FitVertical(fontGroup, 8f);
        var fontRow = GenerateUI.Row(fontGroup.transform);
        fontDropdown = GenerateUI.DropDown(
            fontRow,
            FontManager.DefaultName,
            FontManager.CurrentName,
            BuildFontValues(),
            DisplayFont,
            OnFontSelected,
            "font_dropdown"
        );
        fontDropdown.ItemFont = FontManager.GetFont;
        (fontScaleSlider, fontLineSpacingSlider) = CreateAdjustSliders(fontGroup.transform, () => FontManager.CurrentName);
        var manageRow = GenerateUI.Row(fontGroup.transform);
        fontManageRow = manageRow.gameObject;
        fontRenameInput = GenerateUI.Input(
            manageRow,
            null,
            FontManager.CurrentName,
            v => pendingFontName = v,
            "Font Name",
            MainCore.Spr.Get(UISprite.Text128),
            "font_rename"
        );
        fontRenameInput.Placeholder.gameObject.AddComponent<TextLocalization>().Init("FONT_NAME", "Font Name");
        fontRenameInput.InputField.characterLimit = 40;
        fontDeleteBtn = GenerateUI.Button(
            manageRow,
            () => DeleteCurrentFont(),
            "Delete",
            "font_delete"
        ).SetSecondary();
        fontDeleteRestColor = fontDeleteBtn.RestColor;
        {
            var br = fontDeleteBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(104f, 50f);
            br.anchoredPosition = Vector2.zero;
        }
        fontDeleteBtn.Label.gameObject.AddComponent<TextLocalization>().Init("FONT_DELETE", "Delete");
        UIButton fontRenameBtn = GenerateUI.Button(
            manageRow,
            RenameCurrentFont,
            "Rename",
            "font_rename_btn"
        );
        {
            var br = fontRenameBtn.Rect;
            br.pivot = new(1f, 1f);
            br.anchorMin = new(1f, 1f);
            br.anchorMax = new(1f, 1f);
            br.sizeDelta = new(104f, 50f);
            br.anchoredPosition = new(-112f, 0f);
        }
        fontRenameBtn.Label.gameObject.AddComponent<TextLocalization>().Init("FONT_RENAME", "Rename");
        var fontStatusRowRect = GenerateUI.Row(fontGroup.transform, 28f);
        fontStatusRow = fontStatusRowRect.gameObject;
        fontStatusText = GenerateUI.AddMutedText(fontStatusRowRect, 16f, 0.5f, true);
        fontStatusText.text = "";
        fontStatusRow.SetActive(false);
        RefreshFontManageRow();
        var settingsFontLabelRow = GenerateUI.Row(content.transform);
        var settingsFontText = GenerateUI.AddTextH1(settingsFontLabelRow);
        var settingsFontTextTr = settingsFontText.gameObject.AddComponent<TextLocalization>().Init("SETTINGS_FONT", "Settings Window Font");
        var settingsFontRow = GenerateUI.Row(content.transform);
        settingsFontDropdown = GenerateUI.DropDown(
            settingsFontRow,
            FontManager.UiDefault,
            CurrentSettingsFontValue(),
            BuildSettingsFontValues(),
            DisplaySettingsFont,
            OnSettingsFontSelected,
            "settings_font_dropdown"
        );
        settingsFontDropdown.ItemFont = name => name == FontManager.UiDefault ? FontManager.UiFont : FontManager.GetFont(name);
        (settingsFontScaleSlider, settingsFontLineSpacingSlider) = CreateAdjustSliders(content.transform, SettingsAdjustTarget);
        RefreshSettingsFontAdjust();
        settingsFontDropdown.Rect.AddToolTip(
            "DESC_SETTINGS_FONT",
            "Font for this mod's own settings window. Default is SUIT; \"Same as overlay font\" follows the Font picker above."
        );
    }
    private static (UISlider Scale, UISlider LineSpacing) CreateAdjustSliders(Transform parent, Func<string> target) {
        var (adjScale, adjLine) = FontManager.GetAdjust(target());
        UISlider scale = GenerateUI.SnapSlider(parent, "Font Size", "font_scale",
            1f, FontManager.MinAdjust, FontManager.MaxAdjust, adjScale, "0.00 x", 0.01f,
            v => FontManager.SetAdjust(target(), v, FontManager.GetAdjust(target()).LineSpacing),
            null, () => MainCore.ConfMgr.RequestSave());
        scale.Rect.AddToolTip(
            "DESC_FONT_SCALE",
            "Size multiplier for the selected font. Use it when a custom font renders smaller than others. Saved per font."
        );
        UISlider lineSpacing = GenerateUI.SnapSlider(parent, "Line Spacing", "font_line_spacing",
            1f, FontManager.MinAdjust, FontManager.MaxAdjust, adjLine, "0.00 x", 0.01f,
            v => FontManager.SetAdjust(target(), FontManager.GetAdjust(target()).Scale, v),
            null, () => MainCore.ConfMgr.RequestSave());
        lineSpacing.Rect.AddToolTip(
            "DESC_FONT_LINE_SPACING",
            "Line height multiplier for the selected font. Saved per font."
        );
        return (scale, lineSpacing);
    }
}
