using System;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features.Countdown;
public static class CountdownFeature {
    public static SettingsFile<CountdownSettings> ConfMgr { get; private set; }
    public static CountdownSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<CountdownSettings>.Loaded("Countdown.json");
    public static void Save() => ConfMgr?.RequestSave();
    internal static bool Active => MainCore.IsModEnabled && ConfMgr != null && Conf.Enabled;
    public static void Initialize() => EnsureConf();
    public static void EnabledChanged() {
        if(!Active) CountdownHaywire.RestoreSpeeds();
    }
    public static void Dispose() {
        try {
            CountdownHaywire.RestoreSpeeds();
        } catch(Exception e) {
            Diag.Warn(e, "Countdown/RestoreSpeeds");
        }
    }
}
