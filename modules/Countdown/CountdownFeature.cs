using System;
using Quartz.Core;
using Quartz.IO;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Quartz.Features.Countdown;
public static class CountdownFeature {
    public static SettingsFile<CountdownSettings> ConfMgr { get; private set; }
    public static CountdownSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<CountdownSettings>.Loaded("Countdown.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static CountdownCoordinator coordinator;
    private static GameObject host;
    internal static bool Active => MainCore.IsModEnabled && ConfMgr != null && Conf.Enabled;
    internal static bool HaywireActive => Active && Conf.Mode == CountdownMode.Haywire;
    internal static bool MetronomeActive => Active && Conf.Mode == CountdownMode.Metronome;
    internal static CountdownCoordinator Coordinator =>
        MainCore.IsModEnabled && ConfMgr != null ? coordinator : null;
    public static void Initialize() {
        EnsureConf();
        coordinator ??= new CountdownCoordinator();
        if(host != null) return;
        host = new GameObject("Quartz Countdown Host");
        host.AddComponent<CountdownHost>();
        Object.DontDestroyOnLoad(host);
    }
    public static void ModeChanged() {
        if(!MetronomeActive) coordinator?.Shutdown();
        if(!HaywireActive) CountdownHaywire.RestoreSpeeds();
    }
    public static void Dispose() {
        try {
            CountdownHaywire.RestoreSpeeds();
        } catch(Exception e) {
            Diag.Warn(e, "Countdown/RestoreSpeeds");
        }
        try {
            coordinator?.Shutdown();
        } catch(Exception e) {
            Diag.Warn(e, "Countdown/Shutdown");
        }
        coordinator = null;
        if(host != null) Object.Destroy(host);
        host = null;
    }
    [DefaultExecutionOrder(10000)]
    private sealed class CountdownHost : MonoBehaviour {
        private void Update() {
            try {
                Coordinator?.PumpAsyncInput();
            } catch(Exception e) {
                Diag.Warn(e, "Countdown/Update");
            }
        }
        private void LateUpdate() {
            try {
                Coordinator?.PumpFrozenVisuals();
            } catch(Exception e) {
                Diag.Warn(e, "Countdown/LateUpdate");
            }
        }
    }
}
