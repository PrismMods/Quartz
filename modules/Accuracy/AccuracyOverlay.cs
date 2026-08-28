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
            Id = "accuracy_tma", Category = "Accuracy", Label = "TMA Accuracy",
            Value = () => Conf.Enabled ? FormatAcc(TmaScore.CachedAccuracy) : null,
        });
        StatRegistry.Register(new StatSource {
            Id = "accuracy_tma_combo", Category = "Accuracy", Label = "TMA Combo",
            Value = () => Conf.Enabled ? TmaScore.Combo.ToString(CultureInfo.InvariantCulture) : null,
        });
    }
}
