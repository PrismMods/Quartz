using System;
using System.Collections.Generic;
using Quartz.Core;
namespace Quartz.Compat.Game;
public enum HitKind {
    TooEarly = 0,
    VeryEarly = 1,
    EarlyPerfect = 2,
    Perfect = 3,
    LatePerfect = 4,
    VeryLate = 5,
    TooLate = 6,
    Multipress = 7,
    FailMiss = 8,
    FailOverload = 9,
    Auto = 10,
    OverPress = 11,
    FailedFloor = 12,
    Unknown = 13,
}
public static class HitKinds {
    private static readonly HitKind[] ByGame;
    private static readonly HitMargin[] ToGameMap;
    private static readonly List<int>[] GameIndices;
    static HitKinds() {
        int kinds = (int)HitKind.Unknown + 1;
        ToGameMap = new HitMargin[kinds];
        GameIndices = new List<int>[kinds];
        for(int k = 0; k < kinds; k++) {
            GameIndices[k] = [];
            ToGameMap[k] = (HitMargin)(-1);
        }
        List<HitKind> byGame = [];
        try {
            foreach(object raw in Enum.GetValues(typeof(HitMargin))) {
                int value = Convert.ToInt32(raw);
                if(value < 0) continue;
                HitKind kind = Classify(Enum.GetName(typeof(HitMargin), raw));
                while(byGame.Count <= value) byGame.Add(HitKind.Unknown);
                byGame[value] = kind;
                GameIndices[(int)kind].Add(value);
                if((int)ToGameMap[(int)kind] < 0 || Enum.GetName(typeof(HitMargin), raw) == kind.ToString())
                    ToGameMap[(int)kind] = (HitMargin)value;
            }
        } catch(Exception e) {
            Diag.Warn(e, "mapping the game's HitMargin values");
        }
        ByGame = [.. byGame];
    }
    private static HitKind Classify(string name) => name switch {
        "Perfect" or "PerfectMinus" or "XPerfect" or "PerfectPlus" => HitKind.Perfect,
        "Midspin" => HitKind.Auto,
        _ => Enum.TryParse(name, out HitKind kind) ? kind : HitKind.Unknown,
    };
    public static HitKind Of(HitMargin margin) {
        int i = (int)margin;
        return i >= 0 && i < ByGame.Length ? ByGame[i] : HitKind.Unknown;
    }
    public static HitMargin ToGame(HitKind kind) => ToGameMap[(int)kind];
    public static int Count(int[] gameCounts, HitKind kind) {
        if(gameCounts == null) return 0;
        int sum = 0;
        foreach(int i in GameIndices[(int)kind])
            if(i < gameCounts.Length) sum += gameCounts[i];
        return sum;
    }
}
