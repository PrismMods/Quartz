using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using Quartz.UI;
namespace Quartz.Features.Calibration;
internal static class CalibrationPopup {
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class ChangeStatePatch {
        private static void Postfix(Enum newState) {
            if(!Calibration.Enabled) return;
            if(newState is not States state) return;
            if(state == States.Fail2 && Calibration.Conf.ShowPopupOnDeath && !Calibration.InCalibrationScreen
                && ADOBase.controller is { paused: false } && ADOBase.conductor is { isGameWorld: true }) {
                float current = Calibration.GetOffsetMs();
                float suggested = current + CalibrationTiming.Average();
                if(!CalibrationTiming.HasSamples
                    || Calibration.FormatMs(current) == Calibration.FormatMs(suggested)) {
                    CalibrationPopupUI.Hide();
                    return;
                }
                CalibrationPopupUI.Show(current, suggested);
            } else {
                CalibrationPopupUI.Hide();
            }
        }
    }
    [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
    private static class TogglePauseGamePatch {
        private static void Postfix() => CalibrationPopupUI.Hide();
    }
}
