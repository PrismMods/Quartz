using System.IO;
using System.Reflection;
namespace Quartz.Bootstrap;
public static class PayloadLoader {
    private static readonly object Sync = new();
    private static string payloadDirectory;
    private static bool resolverRegistered;
    public static Assembly Load(RuntimeCandidate candidate) {
        if(!File.Exists(candidate.PayloadPath))
            throw new FileNotFoundException("the runtime payload was not found", candidate.PayloadPath);
        ConfigureResolver(candidate.RuntimePath);
        return Assembly.LoadFrom(Path.GetFullPath(candidate.PayloadPath));
    }
    public static object Invoke(Assembly assembly, string typeName, string methodName, Type[] parameterTypes, object[] arguments) {
        Type type = assembly.GetType(typeName, throwOnError: true);
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            parameterTypes,
            null) ?? throw new MissingMethodException(typeName, methodName);
        try {
            return method.Invoke(null, arguments);
        } catch(TargetInvocationException e) when(e.InnerException != null) {
            throw e.InnerException;
        }
    }
    private static void ConfigureResolver(string directory) {
        lock(Sync) {
            payloadDirectory = directory;
            if(resolverRegistered) return;
            AppDomain.CurrentDomain.AssemblyResolve += ResolvePayloadDependency;
            resolverRegistered = true;
        }
    }
    private static Assembly ResolvePayloadDependency(object sender, ResolveEventArgs args) {
        string directory;
        lock(Sync) directory = payloadDirectory;
        if(string.IsNullOrEmpty(directory)) return null;
        string path = Path.Combine(directory, new AssemblyName(args.Name).Name + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
}
