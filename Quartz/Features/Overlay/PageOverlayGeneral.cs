using Quartz.Core;
using Quartz.Localization;
using Quartz.UI.Generator;
using TMPro;
using UnityEngine;
using Quartz.Overlay;
using GTweens.Easings;
namespace Quartz.UI.Factory.Page;
internal static class PageOverlayGeneral {
    public static void Create(RectTransform parent) {
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var headerRow = GenerateUI.Row(content.transform);
        var headerText = GenerateUI.AddTextH1(headerRow);
        headerText.gameObject.AddComponent<TextLocalization>().Init("OVERLAY_GENERAL", "General");
        GenerateUI.Button(
            GenerateUI.Row(content.transform),
            () => UICore.EnterReorganize(),
            "Reorganize",
            "overlay_reorganize"
        );
        OverlaySwitch.EnsureConf();
        GenerateUI.ToggleTip(
            content.transform,
            true,
            OverlaySwitch.Enabled,
            v => OverlaySwitch.Enabled = v,
            "Enable Overlays",
            "overlay_enabled",
            "Master switch for every overlay HUD — panels, progress bar, combo and judgement."
        );
        BuildPop(content);
    }
    private static void BuildPop(RectTransform content) {
        OverlaySettings conf = OverlaySwitch.Conf;
        GenerateUI.CollapsibleSection popSec = null;
        popSec = GenerateUI.Collapsible(
            content.transform, "Pop-In Animation", startExpanded: false,
            v => {
                conf.PopEnabled = v;
                OverlaySwitch.Save();
                SetHeaderEnabled(v, popSec);
            },
            conf.PopEnabled
        );
        SetHeaderEnabled(conf.PopEnabled, popSec);
        popSec.HeaderObj.transform.Find("Bar").AddToolTip(
            "DESC_OVERLAY_POP",
            "Slide every overlay in from the edge of the screen when a run starts, and back out when you die or clear."
        );
        GenerateUI.DropDown(
            GenerateUI.Row(popSec.Body),
            OverlayPopEdge.Nearest,
            conf.PopEdge,
            new[] {
                OverlayPopEdge.Nearest, OverlayPopEdge.Left, OverlayPopEdge.Right,
                OverlayPopEdge.Top, OverlayPopEdge.Bottom,
            },
            EdgeName,
            v => {
                conf.PopEdge = v;
                OverlaySwitch.Save();
            },
            "overlay_pop_edge",
            260f,
            "Direction"
        );
        GenerateUI.DropDown(
            GenerateUI.Row(popSec.Body),
            Easing.OutCubic,
            conf.PopEase,
            OverlayMotion.Eases,
            static e => e.ToString(),
            v => {
                conf.PopEase = v;
                OverlaySwitch.Save();
            },
            "overlay_pop_ease",
            260f,
            "Easing"
        );
        GenerateUI.SnapSlider(popSec.Body, "Duration", "overlay_pop_seconds",
            0.5f, OverlayMotion.MinSeconds, 2f, conf.PopSeconds, "0.00 s", 0.05f,
            v => conf.PopSeconds = v,
            null,
            OverlaySwitch.Save);
        GenerateUI.ToggleTip(
            popSec.Body,
            true,
            conf.PopExit,
            v => {
                conf.PopExit = v;
                OverlaySwitch.Save();
            },
            "Slide Out on Death or Clear",
            "overlay_pop_exit",
            "Slide the overlays back off screen when the run ends. Turn this off to leave them on screen."
        );
    }
    private static string EdgeName(OverlayPopEdge edge) => edge switch {
        OverlayPopEdge.Left => MainCore.Tr.Get("OVERLAY_POP_EDGE_LEFT", "Left"),
        OverlayPopEdge.Right => MainCore.Tr.Get("OVERLAY_POP_EDGE_RIGHT", "Right"),
        OverlayPopEdge.Top => MainCore.Tr.Get("OVERLAY_POP_EDGE_TOP", "Top"),
        OverlayPopEdge.Bottom => MainCore.Tr.Get("OVERLAY_POP_EDGE_BOTTOM", "Bottom"),
        _ => MainCore.Tr.Get("OVERLAY_POP_EDGE_NEAREST", "Nearest Edge"),
    };
    private static void SetHeaderEnabled(bool enabled, GenerateUI.CollapsibleSection section) {
        if(section.HeaderObj.transform.Find("Bar/Label") is Transform labelTr
            && labelTr.TryGetComponent(out TextMeshProUGUI label))
            label.alpha = enabled ? 1f : 0.5f;
    }
}
