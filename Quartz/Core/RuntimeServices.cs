using System.Diagnostics;
using Quartz.Compat;
using Quartz.Compat.Interface;
namespace Quartz.Core;
public sealed class RuntimeServices {
    private readonly List<IRuntimeService> services = [];
    public void Add(IRuntimeService service) => services.Add(service);
    public void Initialize(QuartzLogger log = null) {
        Stopwatch sw = new();
        foreach(var service in services) {
            sw.Restart();
            service.Initialize();
            log?.Msg($"[Startup] {service.GetType().Name} took {sw.ElapsedMilliseconds} ms");
        }
    }
    public void Dispose() {
        for(int i = services.Count - 1; i >= 0; i--) services[i].Dispose();
    }
}
