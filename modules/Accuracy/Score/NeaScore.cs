using System;
using System.Collections.Generic;
namespace Quartz.Features.Accuracy;
public static class NeaScore {
    private const long FullScore = 1_000_000;
    public static long TotalScore { get; private set; }
    public static int Tiles { get; private set; }
    public static long CachedAccuracy { get; private set; }
    public static long LastTileScore { get; private set; }
    private static readonly List<long> totalLog = [];
    private static readonly List<int> tileLog = [];
    public static void Reset() {
        totalLog.Clear();
        tileLog.Clear();
        TotalScore = 0;
        Tiles = 0;
        CachedAccuracy = 0;
    }
    public static long TileScoreFromMs(double absDeviationMs) {
        long ms = (long)Math.Round(absDeviationMs, MidpointRounding.ToEven);
        return Math.Clamp(100 - ms, 0, 100);
    }
    public static long AddTile(long score0To100) {
        LastTileScore = score0To100;
        Commit(score0To100, tile: true);
        return score0To100;
    }
    public static long AddFailMiss() {
        LastTileScore = -100;
        Commit(-100, tile: true);
        return -100;
    }
    public static long AddFailOverload() {
        LastTileScore = -100;
        Commit(-100, tile: false);
        return -100;
    }
    public static long AddEmptyPress() {
        LastTileScore = -50;
        Commit(-50, tile: false);
        return -50;
    }
    public static void AddNoop() {
        LastTileScore = 0;
        Commit(0, tile: false);
    }
    private static void Commit(long delta, bool tile) {
        if(tile) Tiles++;
        TotalScore += delta;
        totalLog.Add(TotalScore);
        tileLog.Add(Tiles);
        CacheAccuracy();
    }
    private static void CacheAccuracy() =>
        CachedAccuracy = Tiles == 0 ? 0 : (long)Math.Round(FullScore * TotalScore / (100.0 * Tiles));
    public static void RevertTo(int hitCount) {
        if(hitCount < 0) hitCount = 0;
        while(totalLog.Count > hitCount) {
            totalLog.RemoveAt(totalLog.Count - 1);
            tileLog.RemoveAt(tileLog.Count - 1);
        }
        TotalScore = totalLog.Count == 0 ? 0 : totalLog[^1];
        Tiles = tileLog.Count == 0 ? 0 : tileLog[^1];
        CacheAccuracy();
    }
}
