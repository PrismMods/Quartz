using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
namespace Quartz.Features.KeyLimiter;
public static partial class KeyLimiter {
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
    private static MethodBase FindGetStateKeys(bool returnsList) =>
        AccessTools.GetDeclaredMethods(typeof(RDInput)).FirstOrDefault(m => m.Name == "GetStateKeys" && (returnsList
            ? m.ReturnType == typeof(List<AnyKeyCode>)
            : m.ReturnType == typeof(void) && m.GetParameters() is { Length: > 0 } p && p[0].ParameterType == typeof(List<AnyKeyCode>)));
    [HarmonyPatch]
    private static class MenuBlockGetStateKeysPatch {
        private static MethodBase TargetMethod() => FindGetStateKeys(true);
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix(List<AnyKeyCode> __result) {
            if(IsMenuBlockActive()) __result?.Clear();
        }
    }
    [HarmonyPatch]
    private static class MenuBlockGetStateKeysIntoPatch {
        private static MethodBase TargetMethod() => FindGetStateKeys(false);
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix(List<AnyKeyCode> __0) {
            if(IsMenuBlockActive()) __0?.Clear();
        }
    }
}
