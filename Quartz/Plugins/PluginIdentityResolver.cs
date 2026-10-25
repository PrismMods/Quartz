using System.Reflection;
using Quartz.Core;
namespace Quartz.Plugins;
public static class PluginIdentityResolver {
    private static ResolveEventHandler handler;
    private static int refCount;
    private static readonly Dictionary<string, Assembly> current = new(StringComparer.Ordinal);
    public static void Publish(Assembly assembly) {
        if(assembly == null) return;
        try {
            string name = assembly.GetName().Name;
            if(!string.IsNullOrEmpty(name)) current[name] = assembly;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public static void Withdraw(Assembly assembly) {
        if(assembly == null) return;
        try {
            string name = assembly.GetName().Name;
            if(string.IsNullOrEmpty(name)) return;
            if(current.TryGetValue(name, out Assembly held) && ReferenceEquals(held, assembly)) current.Remove(name);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public static void Hook() {
        if(refCount++ > 0) return;
        Assembly mod = typeof(PluginIdentityResolver).Assembly;
        handler = (_, args) => {
            string name = new AssemblyName(args.Name).Name;
            // Every core answers to "Quartz" so one .qaddon loads under all builds.
            if(name is "Quartz" or "QuartzUmm" or "QuartzKeyViewer" or "QuartzKeyViewerUmm") return mod;
            if(name == null) return null;
            if(current.TryGetValue(name, out Assembly live)) return live;
            // Old per-feature module assemblies map to the core; type names are unchanged.
            return name.StartsWith("Quartz.Module.", StringComparison.Ordinal) ? mod : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += handler;
    }
    public static void Unhook() {
        if(refCount == 0 || --refCount > 0) return;
        if(handler == null) return;
        AppDomain.CurrentDomain.AssemblyResolve -= handler;
        handler = null;
        current.Clear();
    }
}
