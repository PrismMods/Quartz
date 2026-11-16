using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using PrismLib.UI.Toolkit;

namespace PrismLib.Bootstrap
{
    /// <summary>
    /// PrismLib.UI's InputGate (keys, wheel and the game's mouse reads withheld while a Prism window is
    /// open) and FontFix work only once some mod applies their patches with its own Harmony; PrismLib.UI
    /// has none. One install per process, whoever comes first. Copy this file into the mod unchanged,
    /// beside PrismBootstrap.cs, and call Tick every frame and Shutdown when the mod switches off.
    /// </summary>
    internal static class PrismGate
    {
        private static HarmonyLib.Harmony _harmony;   // qualified: MelonLoader also has a "Harmony" namespace
        // Cached once: a method group passed every frame would allocate a delegate per call on older compilers.
        private static readonly Action<MethodBase, MethodInfo, string> PatchOne = Patch;

        /// <param name="owner">The mod's name, as InputGate and FontFix record who installed them.</param>
        /// <param name="harmonyId">The Harmony id that owns the patches (unpatching that id removes them).</param>
        internal static void Tick(string owner, string harmonyId)
        {
            if (_harmony == null) _harmony = new HarmonyLib.Harmony(harmonyId);
            InputGate.EnsureInstalled(owner, PatchOne);
            FontFix.EnsureInstalled(owner, PatchOne);
        }

        // The patches stay (they only read PrismLib.UI's shared state); clearing the owner lets another
        // mod's next EnsureInstalled take over.
        internal static void Shutdown(string owner)
        {
            InputGate.Uninstalled(owner);
            FontFix.Uninstalled(owner);
        }

        // PrismLib.UI says what to patch and how.
        private static void Patch(MethodBase target, MethodInfo method, string kind)
        {
            PatchProcessor proc = _harmony.CreateProcessor(target);
            if (kind == "redirect")
                proc.AddTranspiler(new HarmonyMethod(typeof(PrismGate).GetMethod(nameof(RedirectMouse), BindingFlags.NonPublic | BindingFlags.Static)));
            else if (kind == "prefix") proc.AddPrefix(new HarmonyMethod(method));
            else if (kind == "finalizer") proc.AddFinalizer(new HarmonyMethod(method));
            else proc.AddPostfix(new HarmonyMethod(method));
            proc.Patch();
        }

        // The game's mouse-button reads are engine calls Harmony cannot patch here, so the gate redirects
        // their callers: each call becomes InputGate's gated equivalent.
        private static IEnumerable<CodeInstruction> RedirectMouse(IEnumerable<CodeInstruction> code)
        {
            foreach (CodeInstruction ins in code)
            {
                if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.operand is MethodBase called)
                {
                    MethodInfo to = InputGate.RedirectFor(called);
                    if (to != null) { ins.opcode = OpCodes.Call; ins.operand = to; }
                }
                yield return ins;
            }
        }
    }
}
