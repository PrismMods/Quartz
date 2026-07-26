using Quartz.Core;
using Quartz.Features.InGameOverlay;
using Quartz.Features.Interop;
using Quartz.Features.Judgement;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageJudgement {
    public static void AppendTo(Transform content) {
        JudgementOverlay.EnsureConf();
        JudgementSettings conf = JudgementOverlay.Conf;
        JudgementSettings def = new();
        void Save() => JudgementOverlay.Save();
        void Apply() => JudgementOverlay.Apply();
        void SetHeaderEnabled(bool enabled, GenerateUI.CollapsibleSection section) {
            if(section.HeaderObj.transform.Find("Bar/Label") is Transform labelTr
                && labelTr.TryGetComponent(out TextMeshProUGUI label))
                label.alpha = enabled ? 1f : 0.5f;
        }
        GenerateUI.CollapsibleSection sec = null;
        sec = GenerateUI.Collapsible(
            content, "Judgement", startExpanded: false,
            v => { conf.Enabled = v; Apply(); Save(); SetHeaderEnabled(v, sec); },
            conf.Enabled
        );
        SetHeaderEnabled(conf.Enabled, sec);
        if(XPerfectBridge.Installed) {
            GenerateUI.ToggleTip(
                sec.Body,
                def.ShowXPerfect,
                conf.ShowXPerfect,
                v => { conf.ShowXPerfect = v; Apply(); Save(); },
                "Show XPerfect",
                "judgement_xperfect",
                "Split the Perfect count into +Perfect / X / -Perfect when the XPerfect mod is active."
            );
        }
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_LAYOUT", "Layout");
        GenerateUI.SnapSlider(sec.Body, "Size", "judgement_size",
            def.Size, 0.3f, 3f, conf.Size, "0.00 x", 0.01f,
            v => conf.Size = v, Apply, Save);
        GenerateUI.SnapSlider(sec.Body, "Spacing", "judgement_spacing",
            def.Spacing, -20f, 80f, conf.Spacing, "0 px", 1f,
            v => conf.Spacing = v, Apply, Save);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_SHADOW", "Shadow");
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.TextShadowEnabled,
            conf.TextShadowEnabled,
            v => { conf.TextShadowEnabled = v; Apply(); Save(); },
            "Text Shadow",
            "judgement_shadow_enabled"
        );
        GenerateUI.SnapSlider(sec.Body, "Shadow X", "judgement_shadow_x",
            def.TextShadowX, -20f, 20f, conf.TextShadowX, "0.0 px", 0.1f,
            v => conf.TextShadowX = v, Apply, Save);
        GenerateUI.SnapSlider(sec.Body, "Shadow Y", "judgement_shadow_y",
            def.TextShadowY, -20f, 20f, conf.TextShadowY, "0.0 px", 0.1f,
            v => conf.TextShadowY = v, Apply, Save);
        GenerateUI.SnapSlider(sec.Body, "Shadow Softness", "judgement_shadow_softness",
            def.TextShadowSoftness, 0f, 20f, conf.TextShadowSoftness, "0.0 px", 0.1f,
            v => conf.TextShadowSoftness = v, Apply, Save);
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetTextShadowColor(),
            conf.GetTextShadowColor(),
            c => { conf.SetTextShadowColor(c); Apply(); },
            c => { conf.SetTextShadowColor(c); Apply(); Save(); },
            "Shadow Color",
            "judgement_shadow_color"
        );
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => JudgementOverlay.ResetPosition(),
            "Reset Position",
            "judgement_resetpos"
        ).SetSecondary();
        GenerateUI.CollapsibleSection popupSec = null;
        popupSec = GenerateUI.Collapsible(
            content, "Judgement Popup Font", startExpanded: false,
            v => {
                MainCore.Conf.FontJudgement = v;
                MainCore.ConfMgr.RequestSave();
                InGameOverlayFont.Refresh();
                SetHeaderEnabled(v, popupSec);
            },
            MainCore.Conf.FontJudgement
        );
        SetHeaderEnabled(MainCore.Conf.FontJudgement, popupSec);
        popupSec.HeaderObj.transform.Find("Bar").AddToolTip(
            "DESC_FONT_JUDGEMENT",
            "Apply the selected font to the per-hit judgement popup (\"Perfect!\"/\"Early!\"). This is the rating text itself, not the counts above."
        );
        GenerateUI.SnapSlider(popupSec.Body, "Font Size", "font_judgement_popup_size",
            1f, 0.25f, 3f, MainCore.Conf.FontJudgementSize, "0.00 x", 0.01f,
            v => MainCore.Conf.FontJudgementSize = v,
            () => InGameOverlayFont.RefreshSizeOnly(InGameOverlayFont.Category.Judgement),
            () => MainCore.ConfMgr.RequestSave());
    }
    public static void Create(RectTransform parent) =>
        AppendTo(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
}
