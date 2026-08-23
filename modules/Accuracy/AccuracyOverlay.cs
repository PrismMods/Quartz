using System.Globalization;
using Quartz.IO;
using Quartz.Overlay;
namespace Quartz.Features.Accuracy;
internal static class AccuracyOverlay {
    public static SettingsFile<AccuracySettings> ConfMgr { get; private set; }
    public static AccuracySettings Conf => ConfMgr.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<AccuracySettings>.Loaded("Accuracy.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static string FormatAcc(long acc) => (acc / 10000m).ToString("0.0000", CultureInfo.InvariantCulture) + "%";
    public static void RegisterStats() {
        StatRegistry.Register(new StatSource {
            Id = "accuracy_jea", Category = "Accuracy", Label = "JEA Accuracy",
            Value = () => Conf.Enabled && Conf.JeaEnabled ? FormatAcc(JeaScore.CachedAccuracy) : null,
        });
        StatRegistry.Register(new StatSource {
            Id = "accuracy_nea", Category = "Accuracy", Label = "NEA Accuracy",
            Value = () => Conf.Enabled && Conf.NeaEnabled ? FormatAcc(NeaScore.CachedAccuracy) : null,
        });
    }
}
