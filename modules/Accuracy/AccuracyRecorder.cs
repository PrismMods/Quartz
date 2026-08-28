using System.Collections.Generic;
namespace Quartz.Features.Accuracy;
public static class AccuracyRecorder {
    private static readonly List<AccuracyRecord> records = [];
    public static IReadOnlyList<AccuracyRecord> Records => records;
    public static void Clear() => records.Clear();
    public static void Capture(
        int tile, double timestamp, double deviationMs, HitMargin margin,
        double score, long accuracy, int combo
    ) => records.Add(new AccuracyRecord {
        Tile = tile,
        Timestamp = timestamp,
        DeviationMs = deviationMs,
        Margin = margin,
        Score = score,
        Accuracy = accuracy,
        Combo = combo,
    });
    public static void RevertTo(int hitCount) {
        if(hitCount < 0) hitCount = 0;
        while(records.Count > hitCount) records.RemoveAt(records.Count - 1);
    }
}
