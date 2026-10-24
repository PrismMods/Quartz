namespace Quartz.Bootstrap;
public sealed class RuntimeState {
    public int SchemaVersion = 1;
    public string Current;
    public string Previous;
    public string Trial;
    public string Failed;
    // Set by the in-game updater: an unpacked runtime to launch as the next trial.
    public string Staged;
}
