using HarmonyLib;
using UnityEngine;
using Quartz.Compat.Game;
using Quartz.Core;
namespace Quartz.Features.Tweaks;
public static partial class Tweaks {
    [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
    private static class DisableAutoPauseTogglePatch {
        private static bool Prefix(scrController __instance, ref bool __result) {
            if(!ShouldDisableAutoPause || __instance == null) return true;
            bool autoOn;
            try { autoOn = RDC.auto; }
            catch(Exception e) { Diag.Ignore(e); return true; }
            if(!autoOn) return true;
            bool currentlyPaused;
            try { currentlyPaused = __instance.paused; }
            catch(Exception e) { Diag.Ignore(e); return true; }
            if(currentlyPaused) return true;
            if(IsSafePauseCallSite()) return true;
            ResetEditorPlayModePauseState();
            __result = false;
            return false;
        }
    }
    [HarmonyPatch(typeof(RDInput), "get_mouseScrollDelta")]
    private static class BlockMouseWheelScrollPatch {
        private static void Postfix(ref Vector2 __result) {
            if(ShouldBlockMouseWheelScroll) __result = Vector2.zero;
        }
    }
}
