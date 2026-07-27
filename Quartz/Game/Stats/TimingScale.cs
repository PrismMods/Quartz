using Quartz.Core;
namespace Quartz.Game.Stats;
internal static class TimingScale {
    internal static float CurrentMarginScale {
        get {
            try {
                scrController c = scrController.instance;
                if(c != null && c.currFloor != null) return (float)c.currFloor.marginScale;
            } catch(Exception e) { Diag.Ignore(e); }
            return 1f;
        }
    }
}
