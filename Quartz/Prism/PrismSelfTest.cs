#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
namespace Quartz.Prism;
/// <summary>
/// Debug-only body of the prism-selftest run (marker UserData/Quartz/prism-selftest, see FacetSelfTest).
/// Names no PrismLib type, so without PrismLib it fails its first check instead of failing to JIT.
/// </summary>
internal static class PrismSelfTest {
    internal static IEnumerator Run(Action<string, bool, string> check, Action<string> info) {
        if(!PrismBridge.Available) {
            check("prism: PrismLib wired", false, $"lib={PrismBridge.Available} ui={PrismBridge.UiAvailable}");
            yield break;
        }
        yield return PrismSelfTestChecks.Loading(check, info);
        yield return PrismSelfTestChecks.Windows(check, info);
        yield return PrismSelfTestChecks.Undo(check);
        yield return PrismSelfTestChecks.Settings(check, info);
        yield return PrismSelfTestChecks.Smooth(check);
        yield return PrismSelfTestChecks.NewMenu(check, info);
        yield return PrismSelfTestChecks.Arrange(check);
        yield return PrismSelfTestChecks.Layout(check, info);
        yield return PrismSelfTestChecks.UiOff(check);
        PrismSelfTestChecks.SmoothUiOff(check);
    }
}
#endif
