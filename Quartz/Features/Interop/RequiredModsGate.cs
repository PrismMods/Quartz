using System.Collections;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
namespace Quartz.Features.Interop;
public static class RequiredModsGate {
    [HarmonyPatch(typeof(RDEditorUtils), "CheckModsDependency")]
    private static class CheckModsDependencyPatch {
        private static bool Prefix(object[] mods, ref bool __result) {
            try {
                __result = HasMissingMods(mods);
                return false;
            } catch {
                return true;
            }
        }
    }
    internal static bool HasMissingMods(object[] mods) {
        if(mods == null || mods.Length == 0) return false;
        HashSet<string> loaded = null;
        List<string> missing = null;
        foreach(object mod in mods) {
            if(mod is not string name || string.IsNullOrWhiteSpace(name)) continue;
            loaded ??= LoadedModNames();
            if(!loaded.Contains(Norm(name))) (missing ??= []).Add(name);
        }
        if(missing == null) {
            if(loaded != null) MainCore.Log.Msg("[RequiredMods] all required mods present, allowing level load");
            return false;
        }
        MainCore.Log.Msg($"[RequiredMods] missing: [{string.Join(", ", missing)}], keeping vanilla block");
        return true;
    }
    private static HashSet<string> LoadedModNames() {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach(string id in UmmInterop.ActiveModIds()) Add(names, id);
        foreach(string melon in MelonNames()) Add(names, melon);
        try {
            foreach(Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) Add(names, asm.GetName().Name);
        } catch { }
        return names;
    }
    private static void Add(HashSet<string> names, string raw) {
        string norm = Norm(raw);
        if(norm.Length != 0) names.Add(norm);
    }
    private static string Norm(string s) => s == null ? "" : s.Replace(" ", "").Trim().ToLowerInvariant();
    private static List<string> MelonNames() {
        List<string> names = [];
        try {
            Type melonBase = Type.GetType("MelonLoader.MelonBase, MelonLoader");
            object registered = melonBase?.GetProperty("RegisteredMelons", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? Type.GetType("MelonLoader.MelonHandler, MelonLoader")
                    ?.GetProperty("Mods", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if(registered is IEnumerable melons) {
                foreach(object melon in melons) {
                    object info = melon?.GetType().GetProperty("Info", BindingFlags.Public | BindingFlags.Instance)?.GetValue(melon);
                    if(info?.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(info) is string name) names.Add(name);
                }
            }
        } catch { }
        return names;
    }
}
