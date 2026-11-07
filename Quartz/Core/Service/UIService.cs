using Quartz.Compat.Interface;
using Quartz.Prism;
using Quartz.UI;
namespace Quartz.Core.Service;
public sealed class UIService : IRuntimeService, IRuntimeTick {
    public void Initialize() => UICore.Initialize();
    public void Dispose() => UICore.Dispose();
    public void Tick() => Tick(UICore.HandleUpdate);
    // Quartz's own key polls run past PrismLib's input gate, like every Prism mod's. A throw from the menu's update still
    // lets the Prism tick (the gate, the windows, the layout editor) run this frame, then reaches the loader's log as before.
    internal static void Tick(Action menu) {
        try { PrismBridge.Exempt(menu); } finally { PrismBridge.Tick(); }
    }
}
