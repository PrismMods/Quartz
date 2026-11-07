#if !IL2CPP && !QUARTZ_KEYVIEWER
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using PrismLib.UI.Toolkit;
namespace Quartz.Prism;
/// <summary>
/// PrismLib.UI's InputGate (keys, wheel and the game's mouse reads withheld while a Prism window is
/// open) and FontFix work only once some mod applies their patches with its own Harmony. One install
/// per process, whoever comes first; standalone that is Quartz. Reached only through PrismBridge.
/// </summary>
internal static class PrismGate {
    internal const string HarmonyId = "Quartz.PrismGate";
    private static HarmonyLib.Harmony harmony;   // qualified: MelonLoader also has a "Harmony" namespace
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
        harmony ??= new HarmonyLib.Harmony(HarmonyId);
        InputGate.EnsureInstalled(PrismBridge.Id, Patch);
        FontFix.EnsureInstalled(PrismBridge.Id, Patch);
    }
    // The patches stay (they only read PrismLib.UI's shared state); clearing the owner lets another
    // mod's next EnsureInstalled take over.
    internal static void Shutdown() {
        InputGate.Uninstalled(PrismBridge.Id);
        FontFix.Uninstalled(PrismBridge.Id);
    }
    // PrismLib.UI says what to patch and how.
    private static void Patch(MethodBase target, MethodInfo method, string kind) {
        PatchProcessor proc = harmony.CreateProcessor(target);
        if(kind == "redirect")
            proc.AddTranspiler(new HarmonyMethod(typeof(PrismGate).GetMethod(nameof(RedirectMouse), BindingFlags.NonPublic | BindingFlags.Static)));
        else if(kind == "prefix") proc.AddPrefix(new HarmonyMethod(method));
        else if(kind == "finalizer") proc.AddFinalizer(new HarmonyMethod(method));
        else proc.AddPostfix(new HarmonyMethod(method));
        proc.Patch();
    }
    // The game's mouse-button reads are engine calls Harmony cannot patch here, so the gate redirects
    // their callers: each call becomes InputGate's gated equivalent.
    private static IEnumerable<CodeInstruction> RedirectMouse(IEnumerable<CodeInstruction> code) {
        foreach(CodeInstruction ins in code) {
            if((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.operand is MethodBase called) {
                MethodInfo to = InputGate.RedirectFor(called);
                if(to != null) {
                    ins.opcode = OpCodes.Call;
                    ins.operand = to;
                }
            }
            yield return ins;
        }
    }
}
#endif
