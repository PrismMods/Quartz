using System;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
namespace Quartz.Compat;
public static class HarmonyCompat {
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
    private static readonly MethodInfo UnpatchSelfMethod =
        typeof(HarmonyLib.Harmony).GetMethod("UnpatchSelf", Instance, null, Type.EmptyTypes, null);
    private static readonly MethodInfo UnpatchAllMethod = UnpatchSelfMethod != null ? null
        : typeof(HarmonyLib.Harmony).GetMethod("UnpatchAll", Instance, null, [typeof(string)], null);
    public static void UnpatchOwn(this HarmonyLib.Harmony harmony) {
        if(harmony == null) return;
        if(UnpatchSelfMethod != null) {
            UnpatchSelfMethod.Invoke(harmony, null);
            return;
        }
        if(UnpatchAllMethod == null) {
            MainCore.Log.Wrn("[Harmony] this HarmonyX build exposes neither UnpatchSelf nor UnpatchAll — patches stay applied");
            return;
        }
        UnpatchAllMethod.Invoke(harmony, [harmony.Id]);
    }
}
