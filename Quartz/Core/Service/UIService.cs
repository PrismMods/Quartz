using Quartz.Compat.Interface;
using Quartz.Prism;
using Quartz.UI;
namespace Quartz.Core.Service;
public sealed class UIService : IRuntimeService, IRuntimeTick {
    public void Initialize() => UICore.Initialize();
    public void Dispose() => UICore.Dispose();
    // Quartz's own key polls run past PrismLib's input gate, like every Prism mod's.
    public void Tick() {
        PrismBridge.Exempt(UICore.HandleUpdate);
        PrismBridge.Tick();
    }
}
