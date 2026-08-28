namespace Quartz.Features.Accuracy;
public sealed class AccuracyRecord {
    public int Tile;
    public double Timestamp;
    public double DeviationMs;
    public HitMargin Margin;
    public double Score;
    public long Accuracy;
    public int Combo;
}
