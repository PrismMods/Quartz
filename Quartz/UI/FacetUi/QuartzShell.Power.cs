#if !IL2CPP && !QUARTZ_KEYVIEWER
using Facet;
using Quartz.Async;
using Quartz.Core;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
internal static partial class QuartzShell {
    private static bool powerPending;

    /// <summary>Quartz's power, the classic rail's power button, pinned in the title bar. The switch shows the stored
    /// choice at once; the re-patch follows a frame later.</summary>
    internal static readonly Bind<bool> Power = Bind.To(() => MainCore.Conf.Active, SetPower);

    private static void SetPower(bool on) {
        if(powerPending || on == MainCore.Conf.Active) return;
        MainCore.Conf.Active = on;
        powerPending = true;
        // Toggling re-patches every feature: let the switch draw first, then FeatureService spreads the patching over frames.
        MainThread.Enqueue(() => MainThread.Enqueue(() => {
            try {
                Quartz.Features.FeatureService.SliceNext = true;
                MainCore.SetModEnabled(on);
            } finally {
                Quartz.Features.FeatureService.SliceNext = false;   // unused when the runtime was already there
                powerPending = false;
                Changed();
            }
        }));
    }

    /// <summary>Powered off, every page but Settings gives way to this note (unless Settings allows editing while off);
    /// its switch turns Quartz back on. Gated on the runtime, not the stored choice, so a self-test that powers on for
    /// its run sees the pages.</summary>
    internal static bool PoweredOff => !MainCore.IsModEnabled && !MainCore.Conf.EditWhilePoweredOff;

    internal static void Inactive(VisualElement p) =>
        Header(p, QuartzHost.Text("INACTIVE_TITLE", "Quartz is inactive"), Power,
            () => QuartzHost.Text("INACTIVE_HINT", "Every feature is off. Turn Quartz back on to change settings."));
}
#endif
