using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Bootstrap;
// Loads the CURRENT runtime's update engine and asks it whether a newer runtime
// exists (downloading and staging it if so). The engine ships beside each
// payload precisely so this frozen bootstrap never carries update logic that
// could go stale.
public static class EngineClient {
    private const string EntryType = "Quartz.UpdateEngine.EntryPoint";
    private const string EntryMethod = "Resolve";
    public static UpdateResolution Resolve(RuntimeCandidate current, string runtimeRoot, string dataRoot, string failedVersion) {
        if(!File.Exists(current.EnginePath))
            throw new FileNotFoundException("the versioned update engine is missing", current.EnginePath);
        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(current.EnginePath));
        Type type = assembly.GetType(EntryType, throwOnError: true);
        MethodInfo method = type.GetMethod(EntryMethod, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null)
            ?? throw new MissingMethodException(EntryType, EntryMethod);
        string request = JsonConvert.SerializeObject(new {
            CurrentVersion = current.Version,
            RuntimeRoot = runtimeRoot,
            DataRoot = dataRoot,
            FailedVersion = failedVersion,
        });
        object raw;
        try {
            raw = method.Invoke(null, new object[] { request });
        } catch(TargetInvocationException e) when(e.InnerException != null) {
            throw e.InnerException;
        }
        return UpdateResolution.Parse(raw as string);
    }
}
public sealed class UpdateResolution {
    public bool HasCandidate { get; private set; }
    public string Version { get; private set; }
    public string RuntimePath { get; private set; }
    public string Message { get; private set; }
    public static UpdateResolution None(string message = null) => new() { Message = message };
    public static UpdateResolution Parse(string json) {
        JObject root = JObject.Parse(json ?? throw new InvalidDataException("the update engine returned no result"));
        string outcome = root.Value<string>("Outcome");
        string message = root.Value<string>("Message");
        if(outcome == "none") return None(message);
        if(outcome == "error") throw new InvalidDataException(message ?? "the update engine failed");
        if(outcome != "candidate") throw new InvalidDataException("the update engine returned an invalid outcome");
        return new UpdateResolution {
            HasCandidate = true,
            Version = root.Value<string>("Version"),
            RuntimePath = root.Value<string>("RuntimePath"),
            Message = message,
        };
    }
}
