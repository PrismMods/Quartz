#if QUARTZ_KEYVIEWER
using System.Reflection;
namespace Quartz.Core;
/// <summary>
/// Keeps the standalone KeyViewer build dormant when a full Quartz is already
/// loaded in the same process.
/// </summary>
/// <remarks>
/// Both builds register the same uGUI menu on the same keybind, so two of them
/// at once is an unreadable double menu. The full build is the superset, so it
/// wins and this one steps aside rather than fighting it.
/// <para>
/// Both loader identities are checked, not just "Quartz": under a
/// MelonLoader-to-UnityModManager bridge the full build present in the process
/// is the UMM one, and a guard that only knew the MelonLoader name would wave it
/// straight through — which is the exact setup this most needs to catch.
/// </para>
/// </remarks>
internal static class FlavorGuard {
    // Deliberately says "remove", not "turn off": this runs once at load, and a
    // loader's off switch does not unload the assembly, so toggling Quartz off in
    // the panel would leave the standalone dormant anyway and look like a bug.
    internal const string Message =
        "A full Quartz install is already loaded — QuartzKeyViewer stays off to avoid a double menu. "
        + "To use the standalone Key Viewer, remove (or move aside) the Quartz mod folder and restart the game. "
        + "To keep Quartz, remove QuartzKeyViewer instead and use Quartz's own Key Viewer.";
    internal static bool FullQuartzLoaded() {
        foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            try {
                if(assembly.GetName().Name is "Quartz" or "QuartzUmm") return true;
            } catch(Exception e) { Diag.Ignore(e); }
        }
        return false;
    }
}
#endif
