namespace Quartz.Bootstrap;
public sealed class RuntimeState {
    public int SchemaVersion = 1;
    public string Current;
    public string Previous;
    public string Trial;
    public string Failed;
}
