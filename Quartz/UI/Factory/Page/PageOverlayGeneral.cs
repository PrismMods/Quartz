using Quartz.Core;
using Quartz.Features.InGameOverlay;
using Quartz.Features.Panels;
using Quartz.Localization;
using Quartz.UI.Generator;
using TMPro;
using UnityEngine;

namespace Quartz.UI.Factory.Page;

// First child of the collapsible "Overlay" sidebar group — the overlay-wide
// controls that don't belong to one specific feature (Reorganize, the master
// Enable Overlays switch). Extracted from the old monolithic PageOverlay.cs;
// the 6 feature-specific children (Key Viewer, Progress Bar, Combo,
// Judgement, Song Title, Panels) are now their own separate pages.
internal static class PageOverlayGeneral {
    public static void Create(RectTransform parent) {
        PanelsOverlay.EnsureConf();
        PanelsSettings conf = PanelsOverlay.Conf;
        PanelsSettings def = new();
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

        GenerateUI.ToggleTip(
            content.transform,
            def.Enabled,
            conf.Enabled,
            v => { conf.Enabled = v; PanelsOverlay.Save(); },
            "Enable Overlays",
            "overlay_enabled",
            "Master switch for every overlay HUD — panels, progress bar, combo and judgement."
        );

        // No dedicated overlay page owns the native countdown, so its font
        // toggle lives here. Fonts ADOFAI's own pre-level countdown
        // ("3, 2, 1, Go!") in place — no repositioning.
        GenerateUI.CollapsibleSection countdownSec = null;
        countdownSec = GenerateUI.Collapsible(
            content.transform, "Countdown Font", startExpanded: false,
            v => {
                MainCore.Conf.FontCountdown = v;
                MainCore.ConfMgr.RequestSave();
                InGameOverlayFont.Refresh();
                SetHeaderEnabled(v, countdownSec);
            },
            MainCore.Conf.FontCountdown
        );
        SetHeaderEnabled(MainCore.Conf.FontCountdown, countdownSec);
        countdownSec.HeaderObj.transform.Find("Bar").AddToolTip(
            "DESC_FONT_COUNTDOWN",
            "Apply the selected font to the pre-level countdown (\"3, 2, 1, Go!\")."
        );

        GenerateUI.SnapSlider(countdownSec.Body, "Font Size", "font_countdown_size",
            1f, 0.25f, 3f, MainCore.Conf.FontCountdownSize, "0.00 x", 0.01f,
            v => MainCore.Conf.FontCountdownSize = v,
            () => InGameOverlayFont.RefreshSizeOnly(InGameOverlayFont.Category.Countdown),
            () => MainCore.ConfMgr.RequestSave());
    }

    private static void SetHeaderEnabled(bool enabled, GenerateUI.CollapsibleSection section) {
        if(section.HeaderObj.transform.Find("Bar/Label") is Transform labelTr
            && labelTr.TryGetComponent(out TextMeshProUGUI label))
            label.alpha = enabled ? 1f : 0.5f;
    }
}
