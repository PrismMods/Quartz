using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.Accuracy;
internal static class AccuracyExport {
    public static string ExportLastRun() {
        JArray tiles = [];
        foreach(AccuracyRecord record in AccuracyRecorder.Records) {
            tiles.Add(new JObject {
                ["tile"] = record.Tile,
                ["timestamp"] = record.Timestamp,
                ["deviationMs"] = record.DeviationMs,
                ["margin"] = record.Margin.ToString(),
                ["score"] = record.Score,
                ["accuracy"] = record.Accuracy / 10000.0,
                ["combo"] = record.Combo,
            });
        }
        JObject root = new() {
            ["accuracy"] = TmaScore.CachedAccuracy / 10000.0,
            ["tiles"] = TmaScore.Tiles,
            ["maxCombo"] = TmaScore.MaxCombo,
            ["records"] = tiles,
        };
        string dir = Path.Combine(MainCore.Paths.RootPath, "AccuracyExports");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"run-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
        File.WriteAllText(path, root.ToString());
        return path;
    }
}
