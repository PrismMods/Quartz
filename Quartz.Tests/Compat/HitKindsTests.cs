using Quartz.Compat.Game;
using static Asserts;
public enum HitMargin {
    TooEarly, VeryEarly, EarlyPerfect, PerfectMinus, XPerfect, PerfectPlus, LatePerfect, VeryLate, TooLate,
    Multipress, FailMiss, FailOverload, Auto, OverPress, Midspin, FailedFloor,
}
static class HitKindsTests {
    public static void TestR150LayoutMapsByName() {
        Assert(HitKinds.Of(HitMargin.XPerfect) == HitKind.Perfect, "XPerfect is a Perfect");
        Assert(HitKinds.Of(HitMargin.PerfectMinus) == HitKind.Perfect, "PerfectMinus is a Perfect");
        Assert(HitKinds.Of(HitMargin.PerfectPlus) == HitKind.Perfect, "PerfectPlus is a Perfect");
        Assert(HitKinds.Of(HitMargin.LatePerfect) == HitKind.LatePerfect, "LatePerfect keeps its identity past the shift");
        Assert(HitKinds.Of(HitMargin.FailMiss) == HitKind.FailMiss, "FailMiss keeps its identity past the shift");
        Assert(HitKinds.Of(HitMargin.FailedFloor) == HitKind.FailedFloor, "FailedFloor maps");
        Assert(HitKinds.Of(HitMargin.Midspin) == HitKind.Auto, "Midspin is invisible like Auto");
        Assert(HitKinds.ToGame(HitKind.Auto) == HitMargin.Auto, "Auto prefers its own name over Midspin");
        Assert(HitKinds.Of((HitMargin)99) == HitKind.Unknown, "out-of-range value is Unknown");
        Assert(HitKinds.ToGame(HitKind.OverPress) == HitMargin.OverPress, "OverPress round-trips to the game value");
        Assert((int)HitKind.OverPress == 11, "stable numbering keeps saved bitmasks valid");
        int[] counts = new int[16];
        counts[(int)HitMargin.PerfectMinus] = 1;
        counts[(int)HitMargin.XPerfect] = 2;
        counts[(int)HitMargin.PerfectPlus] = 4;
        counts[(int)HitMargin.TooLate] = 8;
        Assert(HitKinds.Count(counts, HitKind.Perfect) == 7, "Perfect count sums all three sub-grades");
        Assert(HitKinds.Count(counts, HitKind.TooLate) == 8, "TooLate count reads the shifted slot");
    }
}
