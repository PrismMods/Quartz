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
                ["jeaScore"] = record.JeaScore,
                ["jeaAccuracy"] = record.JeaAccuracy / 10000.0,
                ["neaScore"] = record.NeaScore,
                ["neaAccuracy"] = record.NeaAccuracy / 10000.0,
            });
        }
        JObject root = new() {
            ["jeaAccuracy"] = JeaScore.CachedAccuracy / 10000.0,
            ["jeaTiles"] = JeaScore.Tiles,
            ["jeaMaxCombo"] = JeaScore.MaxCombo,
            ["neaAccuracy"] = NeaScore.CachedAccuracy / 10000.0,
            ["neaTiles"] = NeaScore.Tiles,
            ["tiles"] = tiles,
        };
        string dir = Path.Combine(MainCore.Paths.RootPath, "AccuracyExports");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"run-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
        File.WriteAllText(path, root.ToString());
        return path;
    }
}
