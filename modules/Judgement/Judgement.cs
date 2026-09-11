using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Features.Judgement;
internal static class Judgement {
    internal const int Slots = 9;
    internal static readonly Color[] SlotColors = [
        new(0.78f, 0.35f, 1f, 1f),
        new(1f, 0.22f, 0.22f, 1f),
        new(1f, 0.44f, 0.31f, 1f),
        new(0.63f, 1f, 0.31f, 1f),
        new(0.38f, 1f, 0.31f, 1f),
        new(0.63f, 1f, 0.31f, 1f),
        new(1f, 0.44f, 0.31f, 1f),
        new(1f, 0.22f, 0.22f, 1f),
        new(0.78f, 0.35f, 1f, 1f),
    ];
    private static readonly int[] counts = new int[16];
    internal static void Reset() => System.Array.Clear(counts, 0, counts.Length);
    internal static int SlotCount(int slot) => slot switch {
        0 => Count(HitKind.FailOverload),
        1 => Count(HitKind.TooEarly),
        2 => Count(HitKind.VeryEarly),
        3 => Count(HitKind.EarlyPerfect),
        4 => Count(HitKind.Perfect) + Count(HitKind.Auto),
        5 => Count(HitKind.LatePerfect),
        6 => Count(HitKind.VeryLate),
        7 => Count(HitKind.TooLate),
        8 => Count(HitKind.FailMiss),
        _ => 0,
    };
    private static int Count(HitKind hit) {
        int idx = (int)hit;
        return idx >= 0 && idx < counts.Length ? counts[idx] : 0;
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) {
            int idx = (int)HitKinds.Of(hit);
            if(MainCore.IsModEnabled && idx >= 0 && idx < counts.Length) counts[idx]++;
        }
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class ResetOnRunStartPatch {
        private static void Postfix() {
            if(MainCore.IsModEnabled) Reset();
        }
    }
    [HarmonyPatch(typeof(scrController), "Start")]
    private static class ResetOnControllerStartPatch {
        private static void Postfix(scrController __instance) {
            if(MainCore.IsModEnabled && __instance.gameworld) Reset();
        }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ResetOnRunExitPatch {
        private static void Postfix() {
            if(MainCore.IsModEnabled) Reset();
        }
    }
}
