using System;
using System.Collections.Generic;
namespace Quartz.Features.Accuracy;
public static class TmaScore {
    public static double TotalScore { get; private set; }
    public static int Tiles { get; private set; }
    public static int Combo { get; private set; }
    public static int MaxCombo { get; private set; }
    public static int ConsecutiveEmptyPresses { get; private set; }
    public static long CachedAccuracy { get; private set; }
    private static readonly List<double> totalLog = [];
    private static readonly List<int> tileLog = [];
    private static readonly List<int> comboLog = [];
    private static readonly List<int> emptyLog = [];
    public static void Reset() {
        totalLog.Clear();
        tileLog.Clear();
        comboLog.Clear();
        emptyLog.Clear();
        TotalScore = 0;
        Tiles = 0;
        Combo = 0;
        MaxCombo = 0;
        ConsecutiveEmptyPresses = 0;
        CachedAccuracy = 0;
    }
    public static double ScoreForDeviation(double absDeviationMs) {
        AccuracySettings conf = AccuracyOverlay.Conf;
        double window = Math.Max(0, conf.WindowMs);
        double max = Math.Max(window + 0.001, conf.MaxDeviationMs);
        if(absDeviationMs <= window) return 100.0;
        if(absDeviationMs >= max) return 0.0;
        double t = (absDeviationMs - window) / (max - window);
        double score = 100.0 * (1.0 - Math.Pow(t, Math.Max(0.1, conf.CurveExponent)));
        return Math.Clamp(score, 0.0, 100.0);
    }
    public static double AddTile(double absDeviationMs) {
        double score = ScoreForDeviation(absDeviationMs);
        Combo = score >= AccuracyOverlay.Conf.ComboThreshold ? Combo + 1 : 0;
        MaxCombo = Math.Max(MaxCombo, Combo);
        ConsecutiveEmptyPresses = 0;
        Commit(score, tile: true);
        return score;
    }
    public static double AddMiss() {
        Combo = 0;
        ConsecutiveEmptyPresses = 0;
        double penalty = AccuracyOverlay.Conf.MissPenalty;
        Commit(penalty, tile: true);
        return penalty;
    }
    public static double AddOverload() {
        Combo = 0;
        ConsecutiveEmptyPresses = 0;
        double penalty = AccuracyOverlay.Conf.OverloadPenalty;
        Commit(penalty, tile: true);
        return penalty;
    }
    public static double AddEmptyPress() {
        ConsecutiveEmptyPresses++;
        if(ConsecutiveEmptyPresses <= AccuracyOverlay.Conf.EmptyPressTolerance) {
            Commit(0, tile: false);
            return 0;
        }
        Combo = 0;
        double penalty = AccuracyOverlay.Conf.EmptyPressPenalty;
        Commit(penalty, tile: false);
        return penalty;
    }
    public static void AddNoop() => Commit(0, tile: false);
    private static void Commit(double delta, bool tile) {
        if(tile) Tiles++;
        TotalScore += delta;
        totalLog.Add(TotalScore);
        tileLog.Add(Tiles);
        comboLog.Add(Combo);
        emptyLog.Add(ConsecutiveEmptyPresses);
        CacheAccuracy();
    }
    private static void CacheAccuracy() =>
        CachedAccuracy = Tiles == 0 ? 0 : (long)Math.Round(TotalScore * 1_000_000.0 / (Tiles * 100.0));
    public static void RevertTo(int hitCount) {
        if(hitCount < 0) hitCount = 0;
        while(totalLog.Count > hitCount) {
            totalLog.RemoveAt(totalLog.Count - 1);
            tileLog.RemoveAt(tileLog.Count - 1);
            comboLog.RemoveAt(comboLog.Count - 1);
            emptyLog.RemoveAt(emptyLog.Count - 1);
        }
        TotalScore = totalLog.Count == 0 ? 0 : totalLog[^1];
        Tiles = tileLog.Count == 0 ? 0 : tileLog[^1];
        Combo = comboLog.Count == 0 ? 0 : comboLog[^1];
        ConsecutiveEmptyPresses = emptyLog.Count == 0 ? 0 : emptyLog[^1];
        MaxCombo = 0;
        foreach(int c in comboLog) MaxCombo = Math.Max(MaxCombo, c);
        CacheAccuracy();
    }
}
