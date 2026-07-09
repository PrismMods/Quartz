using HarmonyLib;
namespace Quartz.Features.Calibration;
internal static class CalibrationSong {
    private static float bpm;
    private static float lastSongTime;
    private static int attempt;
    [HarmonyPatch(typeof(scnCalibration), "Start")]
    private static class StartPatch {
        private static void Postfix(scrConductor ___conductor) {
            if(!Calibration.Enabled) return;
            ___conductor.song.pitch = Calibration.Conf.SongPitch / 100f;
            bpm = 1.3f * Calibration.Conf.SongPitch;
            ___conductor.song.loop = Calibration.Conf.SongRepeat > 0;
            attempt = 0;
        }
    }
    [HarmonyPatch(typeof(scnCalibration), "CleanSlate")]
    private static class CleanSlatePatch {
        private static void Postfix(scrConductor ___conductor) {
            if(!Calibration.Enabled) return;
            ___conductor.song.pitch = Calibration.Conf.SongPitch / 100f;
            ___conductor.song.loop = Calibration.Conf.SongRepeat > 0;
            attempt = 0;
        }
    }
    [HarmonyPatch(typeof(scnCalibration), "PutDataPoint")]
    private static class PutDataPointPatch {
        private static void Prefix(scrConductor ___conductor) {
            if(Calibration.Enabled) ___conductor.bpm = bpm;
        }
        private static void Postfix(scrConductor ___conductor) {
            if(Calibration.Enabled) ___conductor.bpm = 130f;
        }
    }
    [HarmonyPatch(typeof(scnCalibration), "Calibrated")]
    private static class CalibratedPatch {
        private static void Prefix(scrConductor ___conductor) {
            if(Calibration.Enabled) ___conductor.song.pitch = 1f;
        }
    }
    [HarmonyPatch(typeof(scnCalibration), "GetOffset")]
    private static class GetOffsetPatch {
        private static void Postfix(ref double __result) {
            if(!Calibration.Enabled) return;
            double time360 = 30000.0 / bpm;
            double result = __result * 1000.0;
            while(result < Calibration.Conf.SongMinimum) result += time360;
            __result = result / 1000.0;
        }
    }
    [HarmonyPatch(typeof(scnCalibration), "Update")]
    private static class UpdatePatch {
        private static void Postfix(scrConductor ___conductor) {
            if(!Calibration.Enabled || Calibration.Conf.SongRepeat <= 0) return;
            if(lastSongTime > ___conductor.song.time && ++attempt >= Calibration.Conf.SongRepeat)
                ___conductor.song.loop = false;
            lastSongTime = ___conductor.song.time;
        }
    }
}
