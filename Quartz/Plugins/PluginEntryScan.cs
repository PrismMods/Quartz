using System.Reflection;
namespace Quartz.Plugins;
public static class PluginEntryScan {
    public static Type[] Types(Assembly assembly) {
        try {
            return assembly.GetTypes();
        } catch(ReflectionTypeLoadException e) {
            return e.Types.Where(t => t != null).ToArray();
        }
    }
    public static List<Type> FindAll<T>(Assembly assembly) =>
        [.. Types(assembly).Where(t => !t.IsAbstract && typeof(T).IsAssignableFrom(t))];
    public static Type FindEntry<T>(Assembly assembly, string preferredTypeName, out string error) {
        error = null;
        if(!string.IsNullOrWhiteSpace(preferredTypeName)) {
            Type named = null;
            try {
                named = assembly.GetType(preferredTypeName, false);
            } catch {
            }
            if(named != null && !named.IsAbstract && typeof(T).IsAssignableFrom(named)) return named;
        }
        List<Type> found = FindAll<T>(assembly);
        if(found.Count == 0) {
            error = $"no {typeof(T).Name} subclass found";
            return null;
        }
        return found[0];
    }
}
