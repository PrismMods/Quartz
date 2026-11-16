#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using Quartz.Features.Calibration;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.Prism;
internal static partial class PrismSelfTestChecks {
    // The calibration popup sits on a canvas above the shared editor: in a session its graphics let the pointer through (else
    // the editor could not drag or resize it), and a resize scales it and saves the scale.
    private static IEnumerator CalibrationPointer(Action<string, bool, string> check, Action<string> info) {
        if(MineById(LayoutIds.Calibration) is not { } t) {
            info("layout calibration: no target here (feature off)");
            yield break;
        }
        yield return Session(3);
        if(t.GetBox() == null) {
            info("layout calibration: not shown in this session");
            yield break;
        }
        bool through = !CalibrationPopupUI.TakesPointer;
        float s0 = t.GetScale();
        t.SetScale(1.5f);
        yield return Session(2);
        float conf = Calibration.Conf.PopupScale, shown = CalibrationPopupUI.ShownScale;
        t.SetScale(s0);
        yield return Session(1);
        check("layout calibration: in a session the popup lets the pointer through to the editor, and a resize scales it and saves the scale",
            through && t.MaxScale > t.MinScale && Mathf.Abs(conf - 1.5f) < 0.001f && Mathf.Abs(shown - 1.5f) < 0.001f,
            $"through={through} range={t.MinScale}-{t.MaxScale} saved={conf} shown={shown}");
    }
}
#endif
