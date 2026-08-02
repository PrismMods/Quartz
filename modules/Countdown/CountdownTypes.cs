using System;
namespace Quartz.Features.Countdown;
public enum CountdownMode {
    Haywire,
    Metronome,
}
internal enum FrozenStartPhase {
    Idle,
    WaitingForScrub,
    WaitingForSchedule,
    Preparing,
    Frozen,
    Releasing,
}
internal readonly struct AudioRuntimeSnapshot(
    float timeScale,
    bool listenerPaused,
    bool conductorEnabled,
    double songPosition
) {
    internal float TimeScale { get; } = timeScale;
    internal bool ListenerPaused { get; } = listenerPaused;
    internal bool ConductorEnabled { get; } = conductorEnabled;
    internal double SongPosition { get; } = songPosition;
}
internal readonly struct StretchedFloorState(int index, float speed, float extraBeats, int holdLength) {
    internal int Index { get; } = index;
    internal float Speed { get; } = speed;
    internal float ExtraBeats { get; } = extraBeats;
    internal int HoldLength { get; } = holdLength;
}
internal readonly struct ScheduledHitSound(string soundName, double time, float volume) {
    internal string SoundName { get; } = soundName;
    internal double Time { get; } = time;
    internal float Volume { get; } = volume;
}
internal readonly struct MetronomePlayback(
    double originalBpm,
    double clickBpm,
    double startedRealtime,
    double dspStartTime,
    double clickInterval,
    int clickFrames
) {
    internal double OriginalBpm { get; } = originalBpm;
    internal double ClickBpm { get; } = clickBpm;
    internal double StartedRealtime { get; } = startedRealtime;
    internal double DspStartTime { get; } = dspStartTime;
    internal double ClickInterval { get; } = clickInterval;
    internal int ClickFrames { get; } = clickFrames;
    internal double OriginalBeatInterval => 60.0 / OriginalBpm;
}
internal readonly struct MetronomeSettings : IEquatable<MetronomeSettings> {
    internal const double MinimumClickBpm = 20.0;
    internal const double MaximumClickBpm = 999.0;
    internal const int MinimumMeterValue = 1;
    internal const int MaximumMeterValue = 16;
    internal MetronomeSettings(double clickBpm, int numerator, int denominator) {
        ClickBpm = NormalizeBpm(clickBpm);
        Numerator = Math.Clamp(numerator, MinimumMeterValue, MaximumMeterValue);
        Denominator = Math.Clamp(denominator, MinimumMeterValue, MaximumMeterValue);
    }
    internal double ClickBpm { get; }
    internal int Numerator { get; }
    internal int Denominator { get; }
    internal MetronomeSettings WithClickBpm(double value) => new(value, Numerator, Denominator);
    internal MetronomeSettings WithNumerator(int value) => new(ClickBpm, value, Denominator);
    internal MetronomeSettings WithDenominator(int value) => new(ClickBpm, Numerator, value);
    public bool Equals(MetronomeSettings other) =>
        ClickBpm.Equals(other.ClickBpm) && Numerator == other.Numerator && Denominator == other.Denominator;
    public override bool Equals(object obj) => obj is MetronomeSettings other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(ClickBpm, Numerator, Denominator);
    public static bool operator ==(MetronomeSettings left, MetronomeSettings right) => left.Equals(right);
    public static bool operator !=(MetronomeSettings left, MetronomeSettings right) => !left.Equals(right);
    private static double NormalizeBpm(double value) {
        if(double.IsNaN(value) || double.IsInfinity(value)) return MinimumClickBpm;
        return Math.Clamp(Math.Round(value, 1, MidpointRounding.AwayFromZero), MinimumClickBpm, MaximumClickBpm);
    }
}
internal readonly struct PerfectTimingInput(
    double lastHit,
    double targetExitAngle,
    double snappedLastAngle,
    bool isClockwise,
    double crotchet,
    double speed
) {
    internal double LastHit { get; } = lastHit;
    internal double TargetExitAngle { get; } = targetExitAngle;
    internal double SnappedLastAngle { get; } = snappedLastAngle;
    internal bool IsClockwise { get; } = isClockwise;
    internal double Crotchet { get; } = crotchet;
    internal double Speed { get; } = speed;
}
internal static class FrozenStartTiming {
    internal static double CalculatePerfectSongPosition(PerfectTimingInput input) {
        if(input.Speed == 0.0)
            throw new InvalidOperationException("A non-zero planet speed is required to calculate PP time.");
        double direction = input.IsClockwise ? 1.0 : -1.0;
        return input.LastHit
            + (input.TargetExitAngle - input.SnappedLastAngle) * direction / Math.PI * input.Crotchet / input.Speed;
    }
    internal static double CalculateCalibratedAudioSongPosition(
        double logicalSongPosition,
        double calibration,
        float pitch
    ) => logicalSongPosition + calibration * pitch;
}
