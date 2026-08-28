namespace Quartz.Features.Countdown;
internal readonly struct StretchedFloorState(int index, float speed, float extraBeats, int holdLength) {
    internal int Index { get; } = index;
    internal float Speed { get; } = speed;
    internal float ExtraBeats { get; } = extraBeats;
    internal int HoldLength { get; } = holdLength;
}
