using HarmonyLib;

namespace Quartz.Features.Calibration;

// Calibration-song tweaks (pitch, repeat count, minimum-offset floor), port of
// BetterCalibration's CalibrationSong. All three settings default to values
// that are no-ops against vanilla behaviour (Pitch 100 = 1.0x, Repeat 0 = no
// loop, Minimum 0 = the wraparound-floor fix with no extra margin), so this
// is always active once Calibration is enabled — no separate feature toggle.
internal static class CalibrationSong {
    private static float bpm;
    private static float lastSongTime;
    private static int attempt;

    [HarmonyPatch(typeof(scnCalibration), "Start")]
    private static class StartPatch {
        private static void Postfix(scrConductor ___conductor) {
            if(!Calibration.Enabled) return;

            ___conductor.song.pitch = Calibration.Conf.SongPitch / 100f;
            // 130 BPM at pitch 100 (the reference/default calibration speed) —
            // scale linearly so the 360°-tile math below stays in sync with
            // however fast the (possibly pitched) song is actually playing.
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

    // PutDataPoint's own tile-angle math assumes the real 130 BPM regardless
    // of the song's playback pitch — swap conductor.bpm to the derived value
    // just for that call, then straight back to 130 for everything else.
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

    // Fixes offsets reading smaller than the real calibrated value once the
    // calibration planet has wrapped a full turn — floors the raw ms offset at
    // Conf.SongMinimum (0 by default), adding a full 360° cycle at a time.
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
