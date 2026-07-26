using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
    public static SettingsFile<NostalgiaSettings> ConfMgr { get; private set; }
    public static NostalgiaSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<NostalgiaSettings>.Loaded("Nostalgia.json");
    public static void Save() => ConfMgr?.RequestSave();
    public static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }
    public static bool ShouldLegacyResult => Enabled && Conf.LegacyResult;
    public static bool ShouldNoResult => Enabled && Conf.NoResult;
    public static bool ShouldHideDifficulty => Enabled && Conf.HideDifficulty;
    public static bool ShouldHideNoFail => Enabled && Conf.HideNoFail;
    public static bool ShouldOldPracticeMode => Enabled && Conf.OldPracticeMode;
    public static bool ShouldShowSmallSpeedChange => Enabled && Conf.ShowSmallSpeedChange;
    public static bool ShouldLegacyFlash => Enabled && Conf.LegacyFlash;
    public static bool ShouldNoJudgeAnimation => Enabled && Conf.NoJudgeAnimation;
    public static bool ShouldLateJudgement => Enabled && Conf.LateJudgement;
    public static bool ShouldForceJudgeCount => Enabled && Conf.ForceJudgeCount;
    public static bool ShouldLegacyTwirl => Enabled && Conf.LegacyTwirl;
    public static bool ShouldSpace360Tile => Enabled && Conf.Space360Tile;
    public static bool ShouldWeakAuto => Enabled && Conf.WeakAuto;
    public static bool ShouldWhiteAuto => Enabled && Conf.WhiteAuto;
    public static bool ShouldLegacyTexts => Enabled && Conf.LegacyTexts;
    public static bool ShouldDisablePurePerfectSound => Enabled && Conf.DisablePurePerfectSound;
    public static bool ShouldDisableWindSound => Enabled && Conf.DisableWindSound;
    public static bool ShouldDisableCountdownSound => Enabled && Conf.DisableCountdownSound;
    public static bool ShouldDisableEndingSound => Enabled && Conf.DisableEndingSound;
    public static bool ShouldDisableNewBestSound => Enabled && Conf.DisableNewBestSound;
    public static bool ShouldDisableAlphaWarning => Enabled && Conf.DisableAlphaWarning;
    public static bool ShouldDisableAnnounceSign => Enabled && Conf.DisableAnnounceSign;
    public static bool ShouldLegacyCLS => Enabled && Conf.LegacyCLS;
    public static void ApplyDeathSound() {
        try {
            if(Enabled) GCS.playDeathSound = !Conf.DisableDeathSound;
        } catch { }
    }
    public static void ApplyEditorFloors() {
        try {
            if(scnEditor.instance != null) scnEditor.instance.ApplyEventsToFloors();
        } catch { }
    }
    public static void Refresh() {
        EnsureConf();
        ApplyDeathSound();
        try { RDC.useOldAuto = ShouldWeakAuto; } catch { }
        ToggleDifficulty(!ShouldHideDifficulty);
        ToggleNoFail(!ShouldHideNoFail);
        ToggleSign(!ShouldDisableAnnounceSign);
        SetBackground();
        ChangeEditorButtons(Enabled && Conf.LegacyEditorButtonsPositions);
        RemoveShadowAddOutline(Enabled && Conf.LegacyEditorButtonsDesigns);
    }
    static partial void ToggleLegacyCLSImpl(bool active);
    public static void ToggleLegacyCLS(bool active) => ToggleLegacyCLSImpl(active);
    public static void Restore() {
        try { GCS.playDeathSound = true; } catch { }
        try { RDC.useOldAuto = false; } catch { }
        ToggleDifficulty(true);
        ToggleNoFail(true);
        ToggleSign(true);
        ChangeEditorButtons(false);
        RemoveShadowAddOutline(false);
        SetBackground(forceDefault: true);
    }
}
