#if !QUARTZ_UMM
using System.IO;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using Quartz.Bootstrap;
[assembly: MelonInfo(typeof(LoaderMl), BootstrapInfo.ModName, BootstrapInfo.Version, BootstrapInfo.Author, BootstrapInfo.GithubLink)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]
namespace Quartz.Bootstrap;
public class LoaderMl : MelonMod {
    private const string BridgeType = "Quartz.PayloadBridge";
    private Action tick;
    private Action unload;
    public override void OnInitializeMelon() {
        string dataRoot = Path.Combine(MelonEnvironment.UserDataDirectory, BootstrapInfo.ModName);
        string runtimeRoot = Path.Combine(dataRoot, "Runtime");
        try {
            if(LegacyCleanup.RetireMelonLeftovers(MelonEnvironment.ModsDirectory, MelonEnvironment.UserLibsDirectory, Wrn)) {
                // MelonLoader already registered the pre-bootstrap mod this
                // launch. Loading the payload too would put two same-identity
                // assemblies in the process, so the old build runs its final
                // session and the retired DLL makes the next launch ours.
                Msg($"the previous {BootstrapInfo.ModName} install runs this session — the updated runtime takes over on the next launch");
                return;
            }
            BootstrapCore.Run(runtimeRoot, dataRoot, Msg, Wrn, TryStart);
        } catch(Exception e) {
            MelonLogger.Error($"the {BootstrapInfo.ModName} bootstrap failed: {e}");
        }
    }
    private Exception TryStart(Assembly assembly) {
        try {
            Type bridge = assembly.GetType(BridgeType, throwOnError: true);
            PayloadLoader.Invoke(assembly, BridgeType, "Load", Type.EmptyTypes, Array.Empty<object>());
            tick = (Action)bridge.GetMethod("Tick", BindingFlags.Public | BindingFlags.Static).CreateDelegate(typeof(Action));
            unload = (Action)bridge.GetMethod("Unload", BindingFlags.Public | BindingFlags.Static).CreateDelegate(typeof(Action));
            return null;
        } catch(Exception e) {
            tick = null;
            unload = null;
            return e;
        }
    }
    public override void OnUpdate() => tick?.Invoke();
    public override void OnDeinitializeMelon() {
        Action stop = unload;
        unload = null;
        tick = null;
        try {
            stop?.Invoke();
        } catch(Exception e) {
            MelonLogger.Error($"the {BootstrapInfo.ModName} runtime failed to shut down: {e}");
        }
    }
    private static void Msg(string message) => MelonLogger.Msg(message);
    private static void Wrn(string message) => MelonLogger.Warning(message);
}
#endif
