using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Quartz.Core;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Quartz.Features.Calibration;

// Detailed avg/max/min readout on the in-game calibration screen (scnCalibration),
// port of BetterCalibration's CalibrationDetail. Only the current (r141+)
// scnCalibration API exists in the referenced game version — verified against
// the installed Assembly-CSharp.dll, no legacy scrCalibrationPlanet branch needed.
internal static class CalibrationDetail {
    private static Text text;
    private static float? max;
    private static float? min;

    [HarmonyPatch(typeof(scnCalibration), "Start")]
    private static class StartPatch {
        private static void Postfix(Text ___txtResults) {
            text = ___txtResults;

            // Marks "we're on the calibration screen" for CalibrationPopup —
            // see Calibration.InCalibrationScreen. Cleared on the next scene
            // unload (leaving calibration, whichever way).
            Calibration.InCalibrationScreen = true;
            SceneManager.sceneUnloaded += OnCalibrationSceneUnloaded;
        }
    }

    private static void OnCalibrationSceneUnloaded(Scene _) {
        Calibration.InCalibrationScreen = false;
        SceneManager.sceneUnloaded -= OnCalibrationSceneUnloaded;
    }

    [HarmonyPatch(typeof(scnCalibration), "GetOffset")]
    private static class GetOffsetPatch {
        private static void Postfix(double __result) {
            if(!text) return;

            float timing = (float)(__result * 1000);
            if(max == null || timing > max) max = timing;
            if(min == null || timing < min) min = timing;
        }
    }

    [HarmonyPatch(typeof(scnCalibration), "PutDataPoint")]
    private static class PutDataPointPatch {
        private static void Postfix(bool ___calibrated, List<scnCalibration.OffsetPair> ___listOffsets) {
            if(___calibrated || !text || !Calibration.ShouldShowDetail) return;

            float avg = ___listOffsets.Count == 0 ? 0f : (float)(___listOffsets.Sum(t => t.offset) / ___listOffsets.Count) * 1000f;
            text.text = string.Format(
                MainCore.Tr.Get("CALIBRATION_DETAIL_STATS", "Avg {0}ms / Max {1}ms / Min {2}ms"),
                Calibration.FormatMs(avg), Calibration.FormatMs(max ?? 0f), Calibration.FormatMs(min ?? 0f)
            );
        }
    }

    [HarmonyPatch(typeof(scnCalibration), "SetMessageNumber")]
    private static class SetMessageNumberPatch {
        private static void Postfix(int n, Text ___txtResults) {
            if(!___txtResults) return;

            ___txtResults.fontSize = n == 1 ? 30 : 40;
            max = null;
            min = null;
        }
    }
}
