using System;
using HarmonyLib;
using Quartz.Compat.Interface;

namespace Quartz.Core.Service;

// Owns the Harmony instance for the runtime. All [HarmonyPatch] classes in the
// mod assembly are applied on the first runtime tick (not inline in
// Initialize) and reversed by UnpatchSelf on Dispose. Patches stay applied for
// the lifetime of the mod regardless of SetModEnabled — individual
// prefixes/postfixes should gate on MainCore.IsModEnabled themselves when
// their behavior is feature-conditional. Mirrors the original
// KorenResourcePack's lifecycle model.
//
// Why defer: on vanilla UnityModManager, JALib (used by PACL2 and others) does
// invasive surgery on Harmony's own internals — reverse-patching
// PatchFunctions.UpdateWrapper and friends — from a background thread as part
// of its static initializer, racing every mod's synchronous Load()-time
// Harmony.Patch() calls. If Quartz patches a shared method (e.g. scnGame.Play,
// scrController.paused, scnEditor.SwitchToEditMode) before that takeover
// finishes, a JALib-based mod patching the SAME method afterward has to merge
// Quartz's foreign patch into its own custom re-emitter — and if that merge
// throws, JALib's patch loop silently aborts every attribute after the
// failing one in that mod's patch group. That can strand a mod's own
// registration (e.g. custom level-event type hooks) partway applied. Applying
// Quartz's patches on the first tick instead — after every mod's Load() has
// finished on both loaders — means any JALib-based mod has already patched
// its shared methods cleanly before Quartz gets there; if the merge still
// fails, it now throws on Quartz's own per-class try/catch (below) instead of
// inside the other mod's init.
public sealed class HarmonyService : IRuntimeService, IRuntimeTick {
    // Fully qualified: MelonLoader.dll ships a legacy `Harmony` shim namespace
    // that would otherwise shadow the HarmonyLib.Harmony type.
    public HarmonyLib.Harmony Harmony { get; private set; }

    private bool patchesApplied;

    public void Initialize() => Harmony = new HarmonyLib.Harmony(Info.Name);

    public void Tick() {
        if(patchesApplied) return;
        patchesApplied = true;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        PatchAllResilient();
        MainCore.Log.Msg($"[Harmony] applied patches (deferred to first tick) in {sw.ElapsedMilliseconds} ms");
    }

    // Apply each [HarmonyPatch] class on its own instead of Harmony.PatchAll.
    // On HarmonyX (MelonLoader) PatchAll already isolates per class, but on plain
    // Harmony 2.x (vanilla UnityModManager) PatchAll is ALL-OR-NOTHING: a single
    // class that can't apply (a target method removed by a game update, a bad
    // attribute) aborts the entire call and the mod fails to initialize. Patching
    // per class with a try/catch means one drifted patch is skipped (logged)
    // while the rest of the mod still loads. CreateClassProcessor /
    // GetTypesFromAssembly exist on both Harmony flavors.
    private void PatchAllResilient() {
        foreach(Type type in AccessTools.GetTypesFromAssembly(MainCore.Asm)) {
            try {
                Harmony.CreateClassProcessor(type).Patch();
            } catch(Exception e) {
                MainCore.Log.Wrn($"[Harmony] skipped patch class {type.FullName}: {e.Message}");
            }
        }
    }

    public void Dispose() {
        Harmony?.UnpatchSelf();
        Harmony = null;
    }
}
