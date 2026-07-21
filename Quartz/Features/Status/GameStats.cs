using System.Globalization;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Status;
public static class GameStats {
    private struct FrameCached<T> {
        private int stamp;
        private T value;
        internal T Get(Func<T> compute) {
            int s = Time.frameCount + 1;
            if(stamp == s) return value;
            stamp = s;
            return value = compute();
        }
    }
    private static FrameCached<bool> inGameCache;
    private static FrameCached<float> progressCache;
    private static FrameCached<float> accuracyCache;
    private static FrameCached<float> xAccuracyCache;
    private static FrameCached<float> maxXAccuracyCache;
    private static FrameCached<int> checkpointCache;
    private static FrameCached<float> pitchCache;
    private static FrameCached<float> musicRatioCache;
    private static FrameCached<float> mapRatioCache;
    private static FrameCached<float> mapTotalCache;
    private static FrameCached<string> songArtistCache;
    private static FrameCached<string> songTitleCache;
    private static FrameCached<string> songTitleRawCache;
    private static FrameCached<int> fpsCache;
    public static bool InGame => inGameCache.Get(static () => {
        try {
            scrController c = scrController.instance;
            if (c == null || !c.gameworld || c.paused) return false;
            if (ADOBase.isLevelEditor) {
                scnEditor ed = scnEditor.instance;
                if (ed != null && ed.inStrictlyEditingMode) return false;
            }
            return true;
        } catch { return false; }
    });
    public static float Progress => progressCache.Get(static () => {
        try {
            scrController c = scrController.instance;
            if (c == null || c.currentSeqID == 0) return 0f;
            return c.percentComplete;
        } catch { return 0f; }
    });
    public static float Accuracy => accuracyCache.Get(static () => {
        try { return MistakesAccess.PercentAcc(MistakesAccess.Get()); }
        catch { return 1f; }
    });
    public static float XAccuracy => xAccuracyCache.Get(static () => {
        try { return MistakesAccess.PercentXAcc(MistakesAccess.Get()); }
        catch { return 1f; }
    });
    public static float MaxXAccuracy => maxXAccuracyCache.Get(static () => {
        try { return XAccuracyCalc.MaxRatio(); }
        catch { return 1f; }
    });
    public static int CheckpointCount => checkpointCache.Get(static () => {
        try { return scnGame.instance != null ? scnGame.instance.checkpointsUsed : 0; }
        catch { return 0; }
    });
    public static void GetBpm(out float tileBpm, out float currentBpm) => Bpm.GetBpmValues(out tileBpm, out currentBpm);
    public static string HoldBehaviorLabel => Hold.GetHoldBehaviorLabel();
    public static int AutoKps => Bpm.GetAutoKps();
    public static float MarginScale => TimingScale.CurrentMarginScale;
    public static float Pitch => pitchCache.Get(static () => {
        try {
            scrConductor c = scrConductor.instance;
            return c != null && c.song != null ? c.song.pitch : 1f;
        } catch { return 1f; }
    });
    public static int XPerfectX => Interop.XPerfectBridge.XCount();
    public static int XPerfectPlus => Interop.XPerfectBridge.PlusCount();
    public static int XPerfectMinus => Interop.XPerfectBridge.MinusCount();
    public static string SongArtist => songArtistCache.Get(static () => {
        try {
            var g = scnGame.instance;
            return g != null && g.levelData != null ? g.levelData.artist ?? "" : "";
        } catch { return ""; }
    });
    public static string SongTitle => songTitleCache.Get(static () => {
        try {
            var g = scnGame.instance;
            return g != null && g.levelData != null ? g.levelData.song ?? "" : "";
        } catch { return ""; }
    });
    public static string SongTitleRaw => songTitleRawCache.Get(static () => {
        try {
            scrController c = scrController.instance;
            return c != null && c.txtLevelName != null ? c.txtLevelName.text ?? "" : "";
        } catch { return ""; }
    });
    public static bool RunHasStartProgress => !ProgressTracker.RunStartedFromFirstTile
        && ProgressTracker.RunStartProgress > 0f;
    public static float RunStartProgress => ProgressTracker.RunStartProgress;
    public static float RunStartMapTimeRatio => ProgressTracker.RunStartMapTimeRatio;
    public static int SessionAttempts => PlayCount.PlayCount.SessionAttempts;
    public static int TotalAttempts => PlayCount.PlayCount.TotalAttemptsForCurrentMap();
    public static float Best => PlayCount.PlayCount.BestForCurrentMap();
    public static float BestStart => PlayCount.PlayCount.BestStartForCurrentMap();
    private static int musicTimeCurSec = -1, musicTimeLenSec = -1;
    private static string musicTimeCache = "0:00 / 0:00";
    private static int mapTimeSec = -1, mapTimeTotalSec = int.MinValue;
    private static string mapTimeCache = "0:00";
    public static string MusicTimeText {
        get {
            try {
                AudioSource a = scrConductor.instance != null ? scrConductor.instance.song : null;
                if(a == null || a.clip == null) return "0:00 / 0:00";
                int curSec = (int)Mathf.Max(0f, a.time);
                int lenSec = (int)Mathf.Max(0f, a.clip.length);
                if(curSec != musicTimeCurSec || lenSec != musicTimeLenSec) {
                    musicTimeCurSec = curSec;
                    musicTimeLenSec = lenSec;
                    bool hour = a.clip.length >= 3600f;
                    musicTimeCache = FormatTime(a.time, hour) + " / " + FormatTime(a.clip.length, hour);
                }
                return musicTimeCache;
            } catch { return "0:00 / 0:00"; }
        }
    }
    public static float MusicTimeRatio => musicRatioCache.Get(static () => {
        try {
            AudioSource song = scrConductor.instance != null ? scrConductor.instance.song : null;
            if(song == null || song.clip == null || song.clip.length <= 0f) return 0f;
            return Mathf.Clamp01(song.time / song.clip.length);
        } catch { return 0f; }
    });
    public static float MapTimeRatio => mapRatioCache.Get(static () => {
        try {
            scrConductor cd = scrConductor.instance;
            if(cd == null) return 0f;
            float time = (float)(cd.addoffset + cd.songposition_minusi);
            float total = MapTotalSeconds();
            if(total <= 0f) return 0f;
            return Mathf.Clamp01(time / total);
        } catch { return 0f; }
    });
    public static string MapTimeText {
        get {
            try {
                scrConductor cd = scrConductor.instance;
                if(cd == null) return "0:00";
                float t = (float)(cd.addoffset + cd.songposition_minusi);
                float total = MapTotalSeconds();
                if(t < 0f) t = 0f;
                if(total > 0f && t > total) t = total;
                int tSec = (int)t;
                int totalSec = total > 0f ? (int)total : -1;
                if(tSec != mapTimeSec || totalSec != mapTimeTotalSec) {
                    mapTimeSec = tSec;
                    mapTimeTotalSec = totalSec;
                    if(total > 0f) {
                        bool hour = total >= 3600f;
                        mapTimeCache = FormatTime(t, hour) + " / " + FormatTime(total, hour);
                    } else {
                        mapTimeCache = FormatTime(t);
                    }
                }
                return mapTimeCache;
            } catch { return "0:00"; }
        }
    }
    public static int Fps => fpsCache.Get(static () => {
        float dt = Time.unscaledDeltaTime;
        if(dt > 0f) {
            float fps = 1f / dt;
            if(smoothedFps <= 0f) {
                smoothedFps = fps;
            } else {
                float diff = Mathf.Abs(fps - smoothedFps);
                float t = Mathf.Clamp01(diff * fpsSensitivity);
                float smooth = Mathf.Lerp(fpsMinSmooth, fpsMaxSmooth, t);
                float factor = 1f - Mathf.Exp(-smooth * dt);
                smoothedFps += (fps - smoothedFps) * factor;
            }
        }
        float interval = MainCore.Conf.FpsRefreshInterval;
        if(interval > 0f) {
            float now = Time.unscaledTime;
            if(fpsDisplayed == 0 || now >= fpsNextRefresh) {
                fpsDisplayed = Mathf.RoundToInt(smoothedFps);
                fpsNextRefresh = now + interval;
            }
            return fpsDisplayed;
        }
        return Mathf.RoundToInt(smoothedFps);
    });
    private static float smoothedFps;
    private static float fpsNextRefresh;
    private static int fpsDisplayed;
    private const float fpsMinSmooth = 2f;
    private const float fpsMaxSmooth = 12f;
    private const float fpsSensitivity = 0.08f;
    private static float MapTotalSeconds() => mapTotalCache.Get(static () => {
        try {
            scrLevelMaker lm = scrLevelMaker.instance;
            if(lm == null || lm.listFloors == null || lm.listFloors.Count == 0) return 0f;
            scrFloor last = lm.listFloors[lm.listFloors.Count - 1];
            return last != null ? (float)last.entryTime : 0f;
        } catch { return 0f; }
    });
    private static string FormatTime(float seconds, bool forceHour = false) {
        if(seconds < 0f) seconds = 0f;
        int total = (int)seconds;
        if(forceHour || total >= 3600) {
            return (total / 3600).ToString(CultureInfo.InvariantCulture)
                + ":" + ((total % 3600) / 60).ToString("00", CultureInfo.InvariantCulture)
                + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }
        int m = total / 60;
        int s = total % 60;
        return m.ToString(CultureInfo.InvariantCulture)
            + ":" + s.ToString("00", CultureInfo.InvariantCulture);
    }
}
