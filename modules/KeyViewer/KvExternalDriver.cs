using UnityEngine;
namespace Quartz.Features.KeyViewer;
/// <summary>
/// Interop surface for external replay drivers — TUFReplay-Renderer's offline capture in
/// particular. While a driver is active the key viewer runs entirely on the driver's timeline:
/// its clock replaces the wall clock (so rain lengths and animation speeds match the capture,
/// however fast or slow it encodes), driver-injected key edges replace hook input, and the
/// machine's real keyboard is ignored.
/// </summary>
public static class KvExternalDriver {
    private static readonly object gate = new();
    public static bool Active { get; private set; }
    /// <summary>
    /// Enters driver mode. <paramref name="clockSeconds"/> is the driver's monotonic clock in
    /// seconds; it is rebased internally, so any origin works. Call from the main thread.
    /// </summary>
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
