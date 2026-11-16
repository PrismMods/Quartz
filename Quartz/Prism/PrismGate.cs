#if !IL2CPP && !QUARTZ_KEYVIEWER
namespace Quartz.Prism;
/// <summary>
/// Quartz's door to PrismLib's shared gate adapter (vendor/PrismGate.cs): InputGate and FontFix
/// patched through Quartz's own Harmony id when Quartz is the first mod to tick. Reached only through
/// PrismBridge.
/// </summary>
internal static class PrismGate {
    internal const string HarmonyId = "Quartz.PrismGate";
#if DEBUG
    internal static int Ticks; // the self-test sees the gate keep ticking
#pragma warning disable CS0649 // set by the self-test, which a stubs build leaves out
    internal static bool ThrowInTick; // the self-test's stand-in for any failure in the UI tick
#pragma warning restore CS0649
#endif
    internal static void Tick() {
#if DEBUG
        Ticks++;
        if(ThrowInTick) throw new InvalidOperationException("prism self-test: UI tick failure");
#endif
        global::PrismLib.Bootstrap.PrismGate.Tick(PrismBridge.Id, HarmonyId);
    }
    internal static void Shutdown() => global::PrismLib.Bootstrap.PrismGate.Shutdown(PrismBridge.Id);
}
#endif
