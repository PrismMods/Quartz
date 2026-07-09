namespace Quartz.Features.Status;
internal static class TimingScale {
    internal static float CurrentMarginScale {
        get {
            try {
                scrController c = scrController.instance;
                if(c != null && c.currFloor != null) return (float)c.currFloor.marginScale;
            } catch { }
            return 1f;
        }
    }
}
