using System.Reflection;
using HarmonyLib;
using Quartz.Core;
namespace Quartz.Features.Interop;
internal static class XPerfectRecursionGuard {
    [ThreadStatic] private static int depth;
    private static bool applied;
    public static void TryApply(HarmonyLib.Harmony harmony) {
        if(applied || harmony == null) return;
        try {
            Type patchType = AccessTools.TypeByName("XPerfect.HitMarginPatch");
            if(patchType == null) return;
            MethodInfo target = AccessTools.Method(patchType, "Postfix");
            if(target == null) {
                MainCore.Log.Msg("[XPerfectGuard] XPerfect.HitMarginPatch.Postfix not found; guard not installed.");
                applied = true;
                return;
            }
            MethodInfo prefix = typeof(XPerfectRecursionGuard).GetMethod(
                nameof(GuardPrefix), BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo finalizer = typeof(XPerfectRecursionGuard).GetMethod(
                nameof(GuardFinalizer), BindingFlags.Static | BindingFlags.NonPublic);
            PatchCompat(harmony, target, new HarmonyMethod(prefix), new HarmonyMethod(finalizer));
            applied = true;
            MainCore.Log.Msg("[XPerfectGuard] Installed reentry guard on XPerfect.HitMarginPatch.Postfix.");
        } catch (Exception ex) {
            MainCore.Log.Msg("[XPerfectGuard] Install failed: " + ex.Message);
        }
    }
    private static void PatchCompat(HarmonyLib.Harmony harmony, MethodBase target,
                                    HarmonyMethod prefix, HarmonyMethod finalizer) {
        MethodInfo patch = null;
        foreach(MethodInfo m in harmony.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
            ParameterInfo[] p = m.Name == "Patch" ? m.GetParameters() : null;
            if(p != null && p.Length >= 5 && typeof(MethodBase).IsAssignableFrom(p[0].ParameterType)) {
                patch = m;
                break;
            }
        }
        if(patch == null) throw new MissingMethodException("HarmonyLib.Harmony.Patch not found");
        ParameterInfo[] ps = patch.GetParameters();
        object[] args = new object[ps.Length];
        args[0] = target;
        HarmonyMethod[] ordered = { prefix, null, null, finalizer, null };
        for(int i = 1; i < ps.Length; i++) {
            args[i] = i - 1 < ordered.Length ? ordered[i - 1] : null;
        }
        patch.Invoke(harmony, args);
    }
    private static bool GuardPrefix(ref bool __state) {
        __state = false;
        if(depth > 0) return false;
        depth++;
        __state = true;
        return true;
    }
    private static Exception GuardFinalizer(bool __state, Exception __exception) {
        if(__state && depth > 0) depth--;
        return __exception;
    }
}
