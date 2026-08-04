using Quartz.Core;
using Quartz.Features.OttoIcon;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageOttoIcon {
    public static void Create(RectTransform parent) {
        Transform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        OttoIcon.EnsureConf();
        OttoIconSettings conf = OttoIcon.Conf;
        OttoIconSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Otto Icon",
            v => {
                conf.Enabled = v;
                if(v) OttoIcon.Refresh();
                else OttoIcon.Restore();
                OttoIcon.Save();
            },
            conf.Enabled,
            "Enable Otto Icon", "ottoicon_enable", def.Enabled
        );
        TextMeshProUGUI imageStatus = GenerateUI.AddMutedText(GenerateUI.Row(sec.Body, 30f), 17f, 0.45f);
        void RefreshImageStatus() => imageStatus.text = OttoIcon.HasCustomImage
            ? Path.GetFileName(conf.ImagePath)
            : MainCore.Tr.Get("OTTO_IMAGE_DEFAULT", "Using the built-in cat");
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => { OttoIcon.ImportImage(out _); RefreshImageStatus(); },
            "Custom Otto Image",
            "otto_image"
        ).Rect.AddToolTip(
            "DESC_OTTO_IMAGE",
            "Pick any PNG or JPG to stand in for Otto. Without one, the cat that ships with the mod is used."
        );
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => { OttoIcon.ClearImage(); RefreshImageStatus(); },
            "Use Built-in Image",
            "otto_image_clear"
        ).SetSecondary();
        RefreshImageStatus();
        GenerateUI.ToggleTip(
            sec.Body,
            def.TintImage,
            conf.TintImage,
            v => { conf.TintImage = v; OttoIcon.Refresh(); OttoIcon.Save(); },
            "Tint Custom Image",
            "otto_tint_image",
            "On: your own image is painted with the colors below, like the built-in one. Off: it keeps its own colors and only dims while auto is off."
        );
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetColor(),
            conf.GetColor(),
            c => { conf.SetColor(c); OttoIcon.Refresh(); },
            c => { conf.SetColor(c); OttoIcon.Refresh(); OttoIcon.Save(); },
            "Otto Color",
            "otto_color"
        );
        RectTransform highBpmColorRow = null;
        GenerateUI.ToggleTip(
            sec.Body,
            def.UseHighBpmColor,
            conf.UseHighBpmColor,
            v => {
                conf.UseHighBpmColor = v;
                highBpmColorRow?.gameObject.SetActive(v);
                OttoIcon.Refresh();
                OttoIcon.Save();
            },
            "Separate High BPM Color",
            "otto_highbpm_on",
            "On: Otto uses the color below while the level's top BPM is 300+ (where vanilla turns him red). Off: the normal color is always used."
        );
        highBpmColorRow = GenerateUI.Row(sec.Body);
        GenerateUI.ColorPicker(
            highBpmColorRow,
            def.GetHighBpmColor(),
            conf.GetHighBpmColor(),
            c => { conf.SetHighBpmColor(c); OttoIcon.Refresh(); },
            c => { conf.SetHighBpmColor(c); OttoIcon.Refresh(); OttoIcon.Save(); },
            "High BPM Color",
            "otto_highbpm_color"
        );
        highBpmColorRow.gameObject.SetActive(conf.UseHighBpmColor);
        UISlider offsetX = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.OffsetX, -100f, 100f, conf.OffsetX,
            v => Mathf.Round(v), null, null,
            "Offset X",
            "otto_offset_x"
        );
        offsetX.Format = "0";
        offsetX.OnChanged = v => { conf.OffsetX = v; OttoIcon.Refresh(); };
        offsetX.OnComplete = v => { conf.OffsetX = v; OttoIcon.Refresh(); OttoIcon.Save(); };
        UISlider offsetY = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.OffsetY, -100f, 100f, conf.OffsetY,
            v => Mathf.Round(v), null, null,
            "Offset Y",
            "otto_offset_y"
        );
        offsetY.Format = "0";
        offsetY.OnChanged = v => { conf.OffsetY = v; OttoIcon.Refresh(); };
        offsetY.OnComplete = v => { conf.OffsetY = v; OttoIcon.Refresh(); OttoIcon.Save(); };
    }
}
