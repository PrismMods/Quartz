#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.Prism;
/// <summary>The arranging state: overlays show while any editor arranges them, and only one editor runs at a time. Names no PrismLib type.</summary>
internal static partial class PrismSelfTestChecks {
    // n frames, each one in which an editor read Quartz's boxes.
    private static IEnumerator Session(int n) {
        for(int i = 0; i < n; i++) {
            PrismBridge.TouchLayout();
            yield return null;
        }
    }
    internal static IEnumerator Arrange(Action<string, bool, string> check) {
        var flips = new List<bool>();
        Action<bool> rec = flips.Add;
        UICore.OnArrangingChanged += rec;
        try {
            check("arrange: the hotkey works outside a session", UICore.ToggleHotkeyAllowed(true, true), null);
            UICore.EnterReorganize();
            yield return Frames(3);
            check("arrange: reorganize mode is unchanged", UICore.IsArranging && Reorganizer.LiveHandleCount > 0, $"handles={Reorganizer.LiveHandleCount}");
            UICore.ExitReorganize();
            yield return Frames(3);
            yield return Session(3);
            check("arrange: a shared session shows the overlays", UICore.SharedLayoutOpen && GameObject.Find("ComboRoot") != null, null);
            check("arrange: but leaves every drag surface off", Reorganizer.LiveHandleCount == 0, $"handles={Reorganizer.LiveHandleCount}");
            check("arrange: the hotkey is ignored during a session", !UICore.ToggleHotkeyAllowed(true, true), null);
            UICore.EnterReorganize();
            check("arrange: reorganize is refused during a session", !UICore.IsReorganizing, null);
            yield return Frames(4);
            check("arrange: the session ends Grace frames after the last read", !UICore.SharedLayoutOpen, null);
            check("arrange: overlays hide again outside gameplay", GameObject.Find("ComboRoot") == null, null);
            UICore.EnterReorganize();
            yield return Frames(2);
            yield return Session(3);
            check("arrange: a session ends reorganize mode and closes the hub",
                !UICore.IsReorganizing && !UICore.IsOpen && UICore.SharedLayoutOpen && Reorganizer.LiveHandleCount == 0,
                $"reorg={UICore.IsReorganizing} open={UICore.IsOpen} shared={UICore.SharedLayoutOpen} handles={Reorganizer.LiveHandleCount}");
            yield return Frames(4);
        } finally {
            UICore.OnArrangingChanged -= rec;
        }
        check("arrange: OnArrangingChanged fires only on a change", flips.Count >= 4 && flips.Zip(flips.Skip(1), (a, b) => a != b).All(x => x), string.Join(",", flips));
    }
}
#endif
