using Quartz.Features.OttoIcon;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
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
