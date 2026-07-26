namespace Quartz.Features.Status;
[Obsolete("Moved to Quartz.Game.Stats.GameStats. This forwarding shim is removed in a future release.")]
public static class GameStats {
    public static bool InGame => Quartz.Game.Stats.GameStats.InGame;
    public static float Progress => Quartz.Game.Stats.GameStats.Progress;
    public static float Accuracy => Quartz.Game.Stats.GameStats.Accuracy;
    public static float XAccuracy => Quartz.Game.Stats.GameStats.XAccuracy;
    public static float MaxXAccuracy => Quartz.Game.Stats.GameStats.MaxXAccuracy;
    public static int CheckpointCount => Quartz.Game.Stats.GameStats.CheckpointCount;
    public static void GetBpm(out float tileBpm, out float currentBpm) =>
        Quartz.Game.Stats.GameStats.GetBpm(out tileBpm, out currentBpm);
    public static string HoldBehaviorLabel => Quartz.Game.Stats.GameStats.HoldBehaviorLabel;
    public static int AutoKps => Quartz.Game.Stats.GameStats.AutoKps;
    public static float MarginScale => Quartz.Game.Stats.GameStats.MarginScale;
    public static float Pitch => Quartz.Game.Stats.GameStats.Pitch;
    public static int XPerfectX => Quartz.Game.Stats.GameStats.XPerfectX;
    public static int XPerfectPlus => Quartz.Game.Stats.GameStats.XPerfectPlus;
    public static int XPerfectMinus => Quartz.Game.Stats.GameStats.XPerfectMinus;
    public static string SongArtist => Quartz.Game.Stats.GameStats.SongArtist;
    public static string SongTitle => Quartz.Game.Stats.GameStats.SongTitle;
    public static string SongTitleRaw => Quartz.Game.Stats.GameStats.SongTitleRaw;
    public static bool RunCleared => Quartz.Game.Stats.GameStats.RunCleared;
    public static bool RunHasStartProgress => Quartz.Game.Stats.GameStats.RunHasStartProgress;
    public static float RunStartProgress => Quartz.Game.Stats.GameStats.RunStartProgress;
    public static float RunStartMapTimeRatio => Quartz.Game.Stats.GameStats.RunStartMapTimeRatio;
    public static int SessionAttempts => Quartz.Game.Stats.GameStats.SessionAttempts;
    public static int TotalAttempts => Quartz.Game.Stats.GameStats.TotalAttempts;
    public static float Best => Quartz.Game.Stats.GameStats.Best;
    public static float BestStart => Quartz.Game.Stats.GameStats.BestStart;
    public static string MusicTimeText => Quartz.Game.Stats.GameStats.MusicTimeText;
    public static float MusicTimeRatio => Quartz.Game.Stats.GameStats.MusicTimeRatio;
    public static float MapTimeRatio => Quartz.Game.Stats.GameStats.MapTimeRatio;
    public static float MapTimeSeconds => Quartz.Game.Stats.GameStats.MapTimeSeconds;
    public static float MapTotalTimeSeconds => Quartz.Game.Stats.GameStats.MapTotalTimeSeconds;
    public static int MapFloorCount => Quartz.Game.Stats.GameStats.MapFloorCount;
    public static float MapTimeAtProgress(float ratio) => Quartz.Game.Stats.GameStats.MapTimeAtProgress(ratio);
    public static string MapTimeText => Quartz.Game.Stats.GameStats.MapTimeText;
    public static int Fps => Quartz.Game.Stats.GameStats.Fps;
}
