using System.IO;
namespace Quartz.Bootstrap;
public sealed class RuntimeCandidate {
    public RuntimeCandidate(string version, string runtimePath) {
        Version = version;
        RuntimePath = runtimePath;
    }
    public string Version { get; }
    public string RuntimePath { get; }
    public string PayloadPath => Path.Combine(RuntimePath, BootstrapInfo.PayloadFileName);
    public string EnginePath => Path.Combine(RuntimePath, BootstrapInfo.EngineFileName);
}
