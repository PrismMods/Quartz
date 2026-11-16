using Quartz.UI;
using Quartz.UI.Nav;
namespace Quartz.Features.Calibration;
[Feature("calibration", "overlay", 80, "Calibration", "SECTION_CALIBRATION")]
public sealed class CalibrationModule : QuartzFeature {
    private Action<bool> reorganizeHandler;
    private readonly Action<bool> dragHandler = _ => CalibrationPopupUI.SyncDragSurface();
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(CalibrationModule), $"Quartz.Features.Calibration.Lang.{lang}.json");
        Calibration.EnsureConf();
        reorganizeHandler = entering => {
            if(entering) CalibrationPopupUI.BeginReorganize();
            else CalibrationPopupUI.EndReorganize();
        };
        Context.AddPage(new NavPage {
            Key = "overlay.calibration",
            CategoryKey = "overlay",
            Order = 80,
            Title = "Calibration",
            LocaleKey = "SECTION_CALIBRATION",
#if QUARTZ_CLASSIC_UI
            Build = PageCalibration.Create,
#endif
            OwnScroll = true,
        });
        Context.PatchAll();
        // Hooked only while Quartz is on, so reorganizing with Quartz off doesn't bring up the popup.
        Context.OnModEnable("CalibrationPopup", () => {
            CalibrationPopupUI.Initialize();
            UICore.OnArrangingChanged += reorganizeHandler;
#if QUARTZ_CLASSIC_UI
            UICore.OnReorganizeChanged += dragHandler;
#endif
            if(UICore.IsArranging) CalibrationPopupUI.BeginReorganize();
        });
        Context.OnModDisable("CalibrationPopup", () => {
            UICore.OnArrangingChanged -= reorganizeHandler;
#if QUARTZ_CLASSIC_UI
            UICore.OnReorganizeChanged -= dragHandler;
#endif
            CalibrationPopupUI.EndReorganize();
            CalibrationPopupUI.Dispose();
        });
    }
    public override void OnTick() => CalibrationTimingLogger.Tick();
    public override void OnUnload() {
        if(reorganizeHandler != null) {
            UICore.OnArrangingChanged -= reorganizeHandler;
            reorganizeHandler = null;
        }
#if QUARTZ_CLASSIC_UI
        UICore.OnReorganizeChanged -= dragHandler;
#endif
        CalibrationTimingLogger.FlushIfDirty();
        CalibrationPopupUI.Dispose();
    }
}
