using UnityEngine;
namespace Quartz.Features.KeyViewer;
/// <summary>For external replay drivers (TUFReplay-Renderer): while active, the driver's clock and key edges replace the wall clock and the real keyboard.</summary>
public static class KvExternalDriver {
    private static readonly object gate = new();
    public static bool Active { get; private set; }
    /// <summary>Enters driver mode. <paramref name="clockSeconds"/> is a monotonic clock in seconds, any origin. Main thread only.</summary>
    public static void Begin(Func<double> clockSeconds) {
        if(clockSeconds == null) return;
        lock(gate) {
            KvClock.SetExternal(clockSeconds);
            KvInputQueue.SetExternalDriver(true);
            Active = true;
        }
    }
    /// <summary>Leaves driver mode and returns the viewer to the wall clock and real input.</summary>
    public static void End() {
        lock(gate) {
            if(!Active) return;
            KvInputQueue.SetExternalDriver(false);
            KvClock.ClearExternal();
            Active = false;
        }
    }
    /// <summary>Injects one key edge, timestamped on the driver clock at the moment of the call.</summary>
    public static void Key(KeyCode key, bool down) {
        if(!Active) return;
        KvInputQueue.PushExternal(key, down);
    }
}
