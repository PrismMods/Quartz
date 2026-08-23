using System;
using System.Collections.Generic;
namespace Quartz.Features.Accuracy;
public static class JeaScore {
    private static readonly (double Deg, double Score)[] Bands = [
        (1.7, 100), (2.0, 96), (2.4, 92), (2.8, 88), (3.2, 84), (3.6, 80),
        (4.0, 75), (4.5, 70), (5.0, 62), (5.5, 54), (6.0, 46), (6.6, 36),
        (7.2, 26), (8.0, 15),
    ];
    private const double ReferenceBpm = 100.0;
    private const int ComboThreshold = 50;
    private const int EmptyPressTolerance = 8;
    public static double TotalScore { get; private set; }
    public static int Tiles { get; private set; }
    public static int Combo { get; private set; }
    public static int MaxCombo { get; private set; }
    public static int ConsecutiveEmptyPresses { get; private set; }
    public static long CachedAccuracy { get; private set; }
    public static double LastTileScore { get; private set; }
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
    public static double NormalizedDeg(double absDeviationDeg, double bpm) {
        double effectiveBpm = bpm <= 0 ? ReferenceBpm : bpm;
        return absDeviationDeg * ReferenceBpm / effectiveBpm;
    }
    public static double ScoreForNormalizedDeg(double normalizedDeg) {
        if(normalizedDeg <= Bands[0].Deg) return Bands[0].Score;
        for(int i = 1; i < Bands.Length; i++) {
            if(normalizedDeg > Bands[i].Deg) continue;
            (double loDeg, double loScore) = Bands[i - 1];
            (double hiDeg, double hiScore) = Bands[i];
            double t = (normalizedDeg - loDeg) / (hiDeg - loDeg);
            return loScore + (hiScore - loScore) * t;
        }
        return 0.0;
    }
    public static double AddTile(double absDeviationDeg, double bpm) {
        double score = ScoreForNormalizedDeg(NormalizedDeg(absDeviationDeg, bpm));
        Combo = score >= ComboThreshold ? Combo + 1 : 0;
        MaxCombo = Math.Max(MaxCombo, Combo);
        ConsecutiveEmptyPresses = 0;
        LastTileScore = score;
        Commit(score, tile: true);
        return score;
    }
    public static double AddFail() {
        Combo = 0;
        ConsecutiveEmptyPresses = 0;
        LastTileScore = -100;
        Commit(-100, tile: true);
        return -100;
    }
    public static double AddEmptyPress() {
        ConsecutiveEmptyPresses++;
        if(ConsecutiveEmptyPresses <= EmptyPressTolerance) {
            LastTileScore = 0;
            Commit(0, tile: false);
            return 0;
        }
        Combo = 0;
        LastTileScore = -100;
        Commit(-100, tile: false);
        return -100;
    }
    public static void AddNoop() {
        LastTileScore = 0;
        Commit(0, tile: false);
    }
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
