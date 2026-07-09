using Quartz.Core;
using Quartz.IO;
using UnityEngine;

namespace Quartz.Features.Calibration;

// Port of Jongye0l's BetterCalibration (https://github.com/Jongye0l/BetterCalibration).
// Adds: a death-triggered "change your offset?" popup, decimal (sub-millisecond)
// input offset, a detailed avg/max/min readout on the in-game calibration
// screen, calibration-song pitch/repeat/minimum-value tweaks, and a persisted
// per-map timing history. Split across CalibrationTiming (live per-run
// tracker), CalibrationPopup(+UI), CalibrationDetail, CalibrationSong,
// CalibrationFloatOffset and CalibrationTimingLogger — this file is just the
// shared settings gate, same shape as Nostalgia.cs/Judgement.cs.
public static class Calibration {
    public static SettingsFile<CalibrationSettings> ConfMgr { get; private set; }
    public static CalibrationSettings Conf => ConfMgr?.Data;

    public static void EnsureConf() => ConfMgr ??= SettingsFile<CalibrationSettings>.Loaded("Calibration.json");

    public static void Save() => ConfMgr?.RequestSave();

    public static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }

    // True while the in-game calibration screen (scnCalibration) is loaded —
    // set by CalibrationDetail's Start/scene-unload hooks. The calibration
    // planet "dying" is part of its normal loop and fires the same
    // StateBehaviour.ChangeState(States.Fail2) signal a real gameplay death
    // does (scnCalibration.conductor IS the same ADOBase.conductor singleton,
    // so the death-popup's isGameWorld/paused guards don't exclude it on their
    // own). CalibrationPopup checks this to only trigger on a real death.
    public static bool InCalibrationScreen { get; internal set; }

    // === Per-toggle gates (read by the patches) ===
    public static bool ShouldShowPopupOnDeath => Enabled && Conf.ShowPopupOnDeath;
    public static bool ShouldShowDetail => Enabled && Conf.DetailedDisplay;
    public static bool FloatOffsetEnabled => Enabled && Conf.FloatOffsetEnabled;

    // The effective input offset, decimal-aware. Shared by the popup, the
    // timing logger, and the settings page instead of each re-implementing
    // the float-vs-int branch (BetterCalibration duplicated this in 3 files).
    public static float GetOffsetMs() {
        EnsureConf();
        if(Conf.FloatOffsetEnabled
            && Conf.FloatOffsetByDevice.TryGetValue(scrConductor.currentPreset.outputName, out float f))
            return f;
        return scrConductor.currentPreset.inputOffset;
    }

    public static void SetOffsetMs(float value) {
        EnsureConf();

        int rounded = Mathf.RoundToInt(value);
        if(scrConductor.currentPreset.inputOffset != rounded) {
            scrConductor.currentPreset.inputOffset = rounded;
            scrConductor.SaveCurrentPreset();
            Persistence.WriteSaveToDisk();
        }

        if(!Conf.FloatOffsetEnabled) return;

        string device = scrConductor.currentPreset.outputName;
        if(Conf.FloatOffsetByDevice.TryGetValue(device, out float existing) && existing == value) return;
        Conf.FloatOffsetByDevice[device] = value;
        Save();
    }

    // Shared display formatting: decimals when float offset is on, whole ms
    // otherwise. Used by the popup, the detail readout, and the timing log.
    public static string FormatMs(float ms) =>
        Conf.FloatOffsetEnabled ? ms.ToString("0.##") : Mathf.RoundToInt(ms).ToString();
}
