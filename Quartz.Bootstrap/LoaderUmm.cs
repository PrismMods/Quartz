#if QUARTZ_UMM
using System.IO;
using System.Reflection;
using UnityModManagerNet;
namespace Quartz.Bootstrap;
public static class LoaderUmm {
    private const string PayloadEntryType = "Quartz.LoaderUmm";
    public static bool Load(UnityModManager.ModEntry modEntry) {
        string displayName = modEntry.Info.DisplayName;
        string runtimeRoot = Path.Combine(modEntry.Path, "Runtime");
        string dataRoot = Path.Combine(modEntry.Path, "UserData");
        modEntry.Info.DisplayName = modEntry.Info.Id + " <color=grey>[Checking for updates...]</color>";
        try {
            LegacyCleanup.RetireUmmLeftovers(modEntry.Path, modEntry.Logger.Warning);
            BootstrapResult result = BootstrapCore.Run(
                runtimeRoot,
                dataRoot,
                modEntry.Logger.Log,
                modEntry.Logger.Warning,
                assembly => TryStart(assembly, modEntry));
            modEntry.Info.DisplayName = result.UpdateFailed
                ? displayName + " <color=red>[Failed to update!]</color>"
                : displayName;
            if(result.Loaded != null) modEntry.Info.Version = result.Loaded.Version;
            return result.Loaded != null;
        } catch(Exception e) {
            modEntry.Info.DisplayName = displayName;
            modEntry.Logger.Error($"the {BootstrapInfo.ModName} bootstrap failed: {e}");
            return false;
        }
    }
    private static Exception TryStart(Assembly assembly, UnityModManager.ModEntry modEntry) {
        try {
            object loaded = PayloadLoader.Invoke(
                assembly,
                PayloadEntryType,
                "Load",
                new[] { typeof(UnityModManager.ModEntry) },
                new object[] { modEntry });
            return loaded is false
                ? new InvalidOperationException(PayloadEntryType + ".Load returned false")
                : null;
        } catch(Exception e) {
            return e;
        }
    }
}
#endif
