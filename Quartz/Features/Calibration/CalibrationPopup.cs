using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using Quartz.UI;

namespace Quartz.Features.Calibration;

// Death-triggered "change your offset?" popup — the patch/logic half. The
// actual UI (built once, shown/hidden on demand) lives in
// Quartz.UI.CalibrationPopupUI, modeled on UpdateToast.cs for a polished,
// animated look instead of BetterCalibration's raw placeholder canvas.
internal static class CalibrationPopup {
    // scrController flips state through StateBehaviour.ChangeState(Enum) —
    // same signal PlayCount/CalibrationTiming key off of. Fail2 = death.
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class ChangeStatePatch {
        private static void Postfix(Enum newState) {
            if(!Calibration.Enabled) return;
            if(newState is not States state) return;

            if(state == States.Fail2 && Calibration.Conf.ShowPopupOnDeath && !Calibration.InCalibrationScreen
                && ADOBase.controller is { paused: false } && ADOBase.conductor is { isGameWorld: true }) {
                float current = Calibration.GetOffsetMs();
                CalibrationPopupUI.Show(current, current + CalibrationTiming.Average());
            } else {
                CalibrationPopupUI.Hide();
            }
        }
    }

    // Opening/closing the pause menu (including the auto-pause ADOFAI does
    // right after a death) always dismisses the popup — ported 1:1.
    [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
    private static class TogglePauseGamePatch {
        private static void Postfix() => CalibrationPopupUI.Hide();
    }
}
