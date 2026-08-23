namespace Quartz.Features.Accuracy;
public sealed class AccuracyRecord {
    public int Tile;
    public double Timestamp;
    public double DeviationMs;
    public HitMargin Margin;
    public double JeaScore;
    public long JeaAccuracy;
    public long NeaScore;
    public long NeaAccuracy;
}
