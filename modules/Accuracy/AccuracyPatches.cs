using System;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using Quartz.Compat.Game;
using Quartz.Game.Stats;
using UnityEngine;
namespace Quartz.Features.Accuracy;
internal static class AccuracyPatches {
    private static double pendingDeviationDeg;
    private static double pendingDeviationMs;
    private static double pendingEffectiveRate;
    private static double pendingJeaScore;
    private static long pendingNeaScore;
    private static bool pendingValid;
    private static readonly Type TrackerType = Refl.Type("scrMarginTracker") ?? Refl.Type("scrMistakesManager");
    private static MethodBase RevertTarget => Refl.Method(TrackerType, "RevertToLastCheckpoint", 0);
    private static bool IsMidspin() {
        object playerOne = GameApi.PlayerOne();
        return playerOne != null
            && Refl.TryRead(playerOne, "midspinInfiniteMargin", out object value)
            && value is bool b && b;
    }
    [HarmonyPatch]
    private static class SwitchChosenPatch {
        private static MethodBase TargetMethod() => GameApi.SwitchChosenTarget;
        private static void Postfix(scrPlanet __instance) {
            if(!MainCore.IsModEnabled) return;
            try {
                double rad = __instance.cachedAngle - __instance.targetExitAngle;
                if(!__instance.planetarySystem.isCW) rad = -rad;
                double effectiveRate = __instance.conductor.bpm * __instance.planetarySystem.speed * GameStats.Pitch;
                pendingDeviationDeg = Math.Abs(rad) * 180.0 / Math.PI;
                pendingDeviationMs = effectiveRate == 0 ? 0 : 60000.0 / Math.PI * rad / effectiveRate;
                pendingEffectiveRate = effectiveRate;
                pendingJeaScore = JeaScore.ScoreForNormalizedDeg(JeaScore.NormalizedDeg(pendingDeviationDeg, effectiveRate));
                pendingNeaScore = NeaScore.TileScoreFromMs(Math.Abs(pendingDeviationMs));
                pendingValid = true;
            } catch(Exception e) {
                Diag.Ignore(e);
                pendingValid = false;
            }
        }
    }
    [HarmonyPatch]
    private static class AddHitPatch {
        private static MethodBase TargetMethod() => GameApi.AddHitTarget;
        private static void Postfix(object __instance, HitMargin hit) {
            if(!MainCore.IsModEnabled || !AccuracyOverlay.Conf.Enabled) return;
            bool midspin = IsMidspin();
            double devDeg = pendingValid ? pendingDeviationDeg : 0;
            double signedDevMs = pendingValid ? pendingDeviationMs : 0;
            double devMs = Math.Abs(signedDevMs);
            double effectiveRate = pendingValid ? pendingEffectiveRate : 0;
            pendingValid = false;
            double jeaScore;
            long neaScore;
            if(midspin) {
                JeaScore.AddNoop();
                NeaScore.AddNoop();
                jeaScore = 0;
                neaScore = 0;
            } else {
                switch(hit) {
                    case HitMargin.Multipress:
                    case HitMargin.OverPress:
                    case HitMargin.TooEarly:
                    case HitMargin.TooLate:
                        jeaScore = JeaScore.AddEmptyPress();
                        neaScore = NeaScore.AddEmptyPress();
                        break;
                    case HitMargin.FailMiss:
                        jeaScore = JeaScore.AddFail();
                        neaScore = NeaScore.AddFailMiss();
                        break;
                    case HitMargin.FailOverload:
                        jeaScore = JeaScore.AddFail();
                        neaScore = NeaScore.AddFailOverload();
                        break;
                    case HitMargin.Auto:
                        JeaScore.AddNoop();
                        NeaScore.AddNoop();
                        jeaScore = 0;
                        neaScore = 0;
                        break;
                    default:
                        jeaScore = JeaScore.AddTile(devDeg, effectiveRate);
                        neaScore = NeaScore.AddTile(NeaScore.TileScoreFromMs(devMs));
                        break;
                }
            }
            int tile = scrController.instance != null ? scrController.instance.currentSeqID + 1 : 0;
            double timestamp = scrConductor.instance != null ? scrConductor.instance.songposition_minusi : 0;
            AccuracyRecorder.Capture(
                tile, timestamp, signedDevMs, hit,
                jeaScore, JeaScore.CachedAccuracy, neaScore, NeaScore.CachedAccuracy
            );
        }
    }
    [HarmonyPatch]
    private static class RevertPatch {
        private static MethodBase TargetMethod() => RevertTarget;
        private static void Postfix(object __instance) {
            if(!MainCore.IsModEnabled) return;
            int count = GameApi.HitMarginTotal(__instance);
            if(count < 0) return;
            JeaScore.RevertTo(count);
            NeaScore.RevertTo(count);
            AccuracyRecorder.RevertTo(count);
        }
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class ResetOnRunStartPatch {
        private static void Postfix() => ResetAll();
    }
    [HarmonyPatch(typeof(scrController), "Start")]
    private static class ResetOnControllerStartPatch {
        private static void Postfix(scrController __instance) {
            if(__instance.gameworld) ResetAll();
        }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ResetOnRunExitPatch {
        private static void Postfix() => ResetAll();
    }
    private static void ResetAll() {
        if(!MainCore.IsModEnabled) return;
        JeaScore.Reset();
        NeaScore.Reset();
        AccuracyRecorder.Clear();
        pendingValid = false;
    }
    [HarmonyPatch(typeof(scrHitTextMesh), nameof(scrHitTextMesh.Show))]
    private static class HitTextPatch {
        private static void Postfix(scrHitTextMesh __instance) {
            if(!MainCore.IsModEnabled || !AccuracyOverlay.Conf.Enabled || !AccuracyOverlay.Conf.ShowHitText) return;
            TMPro.TMP_Text label = GameApi.HitTextLabel(__instance);
            if(label == null) return;
            (long jeaDisplay, long neaDisplay) = __instance.hitMargin switch {
                HitMargin.FailMiss or HitMargin.FailOverload => (-100L, -100L),
                HitMargin.Multipress or HitMargin.OverPress or HitMargin.TooEarly or HitMargin.TooLate
                    => (-100L, -50L),
                _ => ((long)Math.Floor(pendingJeaScore), pendingNeaScore),
            };
            string suffix = "";
            if(AccuracyOverlay.Conf.JeaEnabled) suffix += $" J{jeaDisplay}";
            if(AccuracyOverlay.Conf.NeaEnabled) suffix += $" N{neaDisplay}";
            if(suffix.Length > 0) label.text += suffix;
        }
    }
    [HarmonyPatch(typeof(scrController), nameof(scrController.FailAction))]
    private static class FailActionPatch {
        private static void Postfix(scrController __instance) {
            if(!MainCore.IsModEnabled || !AccuracyOverlay.Conf.Enabled || !AccuracyOverlay.Conf.ShowDeathMarkers) return;
            try {
                System.Collections.Generic.List<Vector3> points = new();
                PlanetarySystem system = __instance != null ? GameApi.Planetary(__instance) : null;
                if(system?.planetList != null)
                    foreach(scrPlanet p in system.planetList)
                        if(p != null) points.Add(p.transform.position);
                DeathMarker.Mark(points);
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        }
    }
}
