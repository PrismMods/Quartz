using System.Reflection;
using Quartz.Core;
using Quartz.Plugins;
namespace Quartz.Features.KeyViewer.Js;
internal static class KvJsAssemblies {
    private static bool loaded;
    private static bool failed;
    private static readonly string[] Names = [
        "System.Runtime.CompilerServices.Unsafe",
        "Acornima",
        "Jint",
    ];
    public static bool EnsureLoaded() {
        if(loaded) return true;
        if(failed) return false;
        try {
            Assembly self = typeof(KvJsAssemblies).Assembly;
            foreach(string name in Names) {
                if(AlreadyLoaded(name)) continue;
                using Stream stream = self.GetManifestResourceStream("Quartz.KeyViewer.Js." + name + ".dll");
                if(stream == null) {
                    MainCore.Log.Msg("[KeyViewerJS] Missing embedded assembly: " + name);
                    failed = true;
                    return false;
                }
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while(read < bytes.Length) {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if(n <= 0) break;
                    read += n;
                }
                PluginIdentityResolver.Publish(Assembly.Load(bytes));
            }
            loaded = true;
            return true;
        } catch(Exception ex) {
            failed = true;
            MainCore.Log.Msg("[KeyViewerJS] Failed to load JS engine assemblies: " + ex.Message);
            return false;
        }
    }
    private static bool AlreadyLoaded(string name) {
        Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
        for(int i = 0; i < all.Length; i++) {
            try {
                if(string.Equals(all[i].GetName().Name, name, StringComparison.Ordinal)) {
                    PluginIdentityResolver.Publish(all[i]);
                    return true;
                }
            } catch(Exception e) { Diag.Ignore(e); }
        }
        return false;
    }
}
