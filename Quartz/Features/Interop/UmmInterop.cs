using System.Collections;
using System.Reflection;
namespace Quartz.Features.Interop;
public static class UmmInterop {
    private static bool resolved;
    private static Type ummType;        
    private static MethodInfo findMod;  
    private static FieldInfo modEntries; 
    private static PropertyInfo modsPathProp; 
    private static void Resolve() {
        if(resolved) return;
        resolved = true;
        try {
            ummType = Type.GetType("UnityModManagerNet.UnityModManager, UnityModManager");
            if(ummType == null) return;
            findMod = ummType.GetMethod("FindMod", BindingFlags.Public | BindingFlags.Static);
            modEntries = ummType.GetField("modEntries", BindingFlags.Public | BindingFlags.Static);
            modsPathProp = ummType.GetProperty("modsPath", BindingFlags.Public | BindingFlags.Static);
        } catch {
            ummType = null;
        }
    }
    public static bool IsPresent {
        get {
            Resolve();
            return ummType != null;
        }
    }
    public static object FindMod(string id) {
        Resolve();
        if(findMod == null || string.IsNullOrEmpty(id)) return null;
        try {
            return findMod.Invoke(null, [id]);
        } catch {
            return null;
        }
    }
    public static bool IsModActive(string id) {
        object entry = FindMod(id);
        return entry != null && ReadMember(entry, "Active") is bool b && b;
    }
    public static Assembly GetModAssembly(string id) {
        object entry = FindMod(id);
        return entry == null ? null : ReadMember(entry, "Assembly") as Assembly;
    }
    public static string GetModVersion(string id) {
        object entry = FindMod(id);
        object info = entry == null ? null : ReadMember(entry, "Info");
        return info == null ? null : ReadMember(info, "Version") as string;
    }
    public static List<string> ActiveModIds() {
        Resolve();
        List<string> ids = [];
        try {
            if(modEntries?.GetValue(null) is IEnumerable entries) {
                foreach(object entry in entries) {
                    if(ReadMember(entry, "Active") is not bool active || !active) continue;
                    object info = ReadMember(entry, "Info");
                    if(info != null && ReadMember(info, "Id") is string id && !string.IsNullOrEmpty(id)) ids.Add(id);
                }
            }
        } catch { }
        return ids;
    }
    public static string ModsPath() {
        Resolve();
        try {
            return modsPathProp?.GetValue(null, null) as string;
        } catch {
            return null;
        }
    }
    public static List<string> InstalledModIds() {
        Resolve();
        List<string> ids = [];
        try {
            if(modEntries?.GetValue(null) is IEnumerable entries) {
                foreach(object entry in entries) {
                    object info = ReadMember(entry, "Info");
                    if(info != null && ReadMember(info, "Id") is string id && !string.IsNullOrEmpty(id)) ids.Add(id);
                }
            }
        } catch { }
        return ids;
    }
    private static object ReadMember(object target, string name) {
        if(target == null) return null;
        try {
            Type t = target.GetType();
            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if(f != null) return f.GetValue(target);
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p?.GetValue(target);
        } catch {
            return null;
        }
    }
}
