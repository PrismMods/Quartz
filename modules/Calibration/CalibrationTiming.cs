using System;
using System.Collections.Generic;
using HarmonyLib;
using MonsterLove.StateMachine;
using System.Reflection;
using Quartz.Compat.Game;
namespace Quartz.Features.Calibration;
internal static class CalibrationTiming {
    private static readonly List<float> timings = [];
    private static float lastTooEarly = float.NaN;
    private static float lastTooLate = float.NaN;
    internal static bool HasSamples => timings.Count > 0;
    internal static float Average() {
        if(timings.Count == 0) return 0f;
        float sum = 0f;
        foreach(float t in timings) sum += t;
        return sum / timings.Count;
    }
    private static void ResetLastTooJudge() {
        lastTooEarly = float.NaN;
        lastTooLate = float.NaN;
    }
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class ChangeStatePatch {
        private static void Postfix(Enum newState) {
            if(!Calibration.Enabled) return;
            if(newState is not States state) return;
            if(state != States.Fail2) ResetLastTooJudge();
            if(state == States.Start) timings.Clear();
        }
    }
    [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
    private static class TogglePauseGamePatch {
        private static void Postfix() {
            if(Calibration.Enabled) ResetLastTooJudge();
        }
    }
    private static void Record(float timing, HitMargin result) {
        HitKind kind = HitKinds.Of(result);
        if(!Calibration.Enabled || RDC.auto || kind == HitKind.Auto) return;
        switch(kind) {
            case HitKind.TooEarly:
                lastTooEarly = timing;
                break;
            case HitKind.TooLate:
                lastTooLate = timing;
                break;
            default:
                timings.Add(timing);
                ResetLastTooJudge();
                break;
        }
    }
    private static float AngleTiming(float hitAngle, float refAngle, bool clockwise, float bpmTimesSpeed, float conductorPitch) =>
        (hitAngle - refAngle) * (clockwise ? 1f : -1f) * 57.29578f / 180f / bpmTimesSpeed / conductorPitch * 60000f;
    [HarmonyPatch]
    private static class LegacyGetHitMarginPatch {
        private static MethodBase TargetMethod() => Refl.Method(typeof(scrMisc), "GetHitMargin", 6);
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, HitMargin __result) =>
            Record(AngleTiming(hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch), __result);
    }
    [HarmonyPatch]
    private static class GetHitMarginInDegPatch {
        private static MethodBase TargetMethod() => Refl.Method(typeof(scrMisc), "GetHitMarginInDeg", 7);
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix(float hitAngle, float refAngle, bool clockwise, float floorBpm, float conductorPitch, HitMargin __result) =>
            Record(AngleTiming(hitAngle, refAngle, clockwise, floorBpm, conductorPitch), __result);
    }
    [HarmonyPatch]
    private static class GetHitMarginInSecPatch {
        private static MethodBase TargetMethod() => Refl.Method(typeof(scrMisc), "GetHitMarginInSec", 5);
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix(double timeDiff, HitMargin __result) => Record((float)(timeDiff * 1000.0), __result);
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(HitMargin hit) {
            if(!Calibration.Enabled || HitKinds.Of(hit) != HitKind.FailMiss) return;
            if(float.IsNaN(lastTooEarly) || float.IsNaN(lastTooLate)) return;
            timings.Add(lastTooLate);
            timings.Add(lastTooEarly);
            ResetLastTooJudge();
        }
    }
}
