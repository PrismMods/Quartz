using Quartz.Modules;
using Quartz.UI;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Calibration;
public sealed class CalibrationModule : QuartzModule {
    private Action<bool> reorganizeHandler;
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(CalibrationModule), $"Quartz.Features.Calibration.Lang.{lang}.json");
        Calibration.EnsureConf();
        reorganizeHandler = entering => {
            if(entering) CalibrationPopupUI.BeginReorganize();
            else CalibrationPopupUI.EndReorganize();
        };
        UICore.OnReorganizeChanged += reorganizeHandler;
        Context.AddPage(new NavPage {
            Key = "overlay.calibration",
            CategoryKey = "overlay",
            Order = 80,
            Title = "Calibration",
            LocaleKey = "SECTION_CALIBRATION",
            Build = PageCalibration.Create,
            OwnScroll = true,
        });
        Context.PatchAll(typeof(CalibrationModule));
        Context.OnModEnable("CalibrationPopup", CalibrationPopupUI.Initialize);
        Context.OnModDisable("CalibrationPopup", CalibrationPopupUI.Dispose);
    }
    public override void OnTick() => CalibrationTimingLogger.Tick();
    public override void OnUnload() {
        if(reorganizeHandler != null) {
            UICore.OnReorganizeChanged -= reorganizeHandler;
            reorganizeHandler = null;
        }
        CalibrationTimingLogger.FlushIfDirty();
        CalibrationPopupUI.Dispose();
    }
}
