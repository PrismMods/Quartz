#nullable enable
using System.Reflection;
using Quartz.Core;
using VoltRpc.Types;
namespace Quartz.Features.Minecraft;
internal static class McReadWriters {
    // UnityWebBrowser's ReadWriterUtils is internal, so Quartz cannot ask it to
    // register the DTO serializers. The serializers themselves derive from VoltRpc's
    // public TypeReadWriter<T>, so instantiating them reflectively and handing them
    // to the public AddType<T> overload reuses upstream's exact wire format instead
    // of reimplementing it — the one thing that must not drift.
    private static readonly string[] Names = [
        "VoltstroStudios.UnityWebBrowser.Shared.ReadWriters.KeyboardEventTypeReadWriter",
        "VoltstroStudios.UnityWebBrowser.Shared.ReadWriters.MouseClickEventTypeReadWriter",
        "VoltstroStudios.UnityWebBrowser.Shared.ReadWriters.MouseMoveEventTypeReadWriter",
        "VoltstroStudios.UnityWebBrowser.Shared.ReadWriters.MouseScrollEventTypeReadWriter",
        "VoltstroStudios.UnityWebBrowser.Shared.ReadWriters.ResolutionTypeReadWriter",
        "VoltstroStudios.UnityWebBrowser.Shared.ReadWriters.ExecuteJsMethodTypeReadWriter",
    ];
    public static bool AddBaseTypeReadWriters(TypeReaderWriterManager manager) {
        Assembly shared = typeof(VoltstroStudios.UnityWebBrowser.Shared.Resolution).Assembly;
        MethodInfo? generic = null;
        foreach(MethodInfo candidate in typeof(TypeReaderWriterManager).GetMethods()) {
            if(candidate.Name != "AddType" || !candidate.IsGenericMethodDefinition) continue;
            ParameterInfo[] parameters = candidate.GetParameters();
            if(parameters.Length == 1) { generic = candidate; break; }
        }
        if(generic == null) {
            MainCore.Log.Err("[Minecraft] VoltRpc has no generic AddType overload; engine IPC cannot start.");
            return false;
        }
        foreach(string name in Names) {
            Type? type = shared.GetType(name, false);
            if(type == null) {
                MainCore.Log.Err("[Minecraft] missing UnityWebBrowser type reader: " + name);
                return false;
            }
            Type? readWriter = type.BaseType;
            if(readWriter == null || !readWriter.IsGenericType) {
                MainCore.Log.Err("[Minecraft] unexpected type reader shape: " + name);
                return false;
            }
            try {
                object? instance = Activator.CreateInstance(type, true);
                if(instance == null) return false;
                generic.MakeGenericMethod(readWriter.GetGenericArguments()[0]).Invoke(manager, [instance]);
            } catch(Exception e) {
                MainCore.Log.Err($"[Minecraft] could not register {name}: {e.Message}");
                return false;
            }
        }
        return true;
    }
}
