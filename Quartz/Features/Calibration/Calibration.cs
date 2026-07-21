using Quartz.Core;
using Quartz.IO;
using UnityEngine;
namespace Quartz.Features.Calibration;
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
    public static bool InCalibrationScreen { get; internal set; }
    public static bool ShouldShowPopupOnDeath => Enabled && Conf.ShowPopupOnDeath;
    public static bool ShouldShowDetail => Enabled && Conf.DetailedDisplay;
    public static bool FloatOffsetEnabled => Enabled && Conf.FloatOffsetEnabled;
    public static float GetOffsetMs() {
        EnsureConf();
        int preset = scrConductor.currentPreset.inputOffset;
        if(!Conf.FloatOffsetEnabled) return preset;
        string device = scrConductor.currentPreset.outputName;
        if(!Conf.FloatOffsetByDevice.TryGetValue(device, out float f)) return preset;
        if(Mathf.RoundToInt(f) == preset) return f;
        Conf.FloatOffsetByDevice[device] = preset;
        Save();
        return preset;
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
    public static string FormatMs(float ms) =>
        Conf.FloatOffsetEnabled ? ms.ToString("0.##") : Mathf.RoundToInt(ms).ToString();
}
