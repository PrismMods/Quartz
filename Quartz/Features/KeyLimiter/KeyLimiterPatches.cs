using HarmonyLib;
namespace Quartz.Features.KeyLimiter;
internal static partial class KeyLimiter {
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetState))]
    private static class MenuBlockGetStatePatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.WentDown))]
    private static class MenuBlockWentDownPatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.IsDown))]
    private static class MenuBlockIsDownPatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.WentUp))]
    private static class MenuBlockWentUpPatch {
        private static void Postfix(ref bool __result) {
            if(IsMenuBlockActive()) __result = false;
        }
    }
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetMain))]
    private static class MenuBlockGetMainPatch {
        private static void Postfix(ref int __result) {
            if(IsMenuBlockActive()) __result = 0;
        }
    }
    [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetStateKeys))]
    private static class MenuBlockGetStateKeysPatch {
        private static void Postfix(System.Collections.Generic.List<AnyKeyCode> __result) {
            if(IsMenuBlockActive()) __result.Clear();
        }
    }
}
