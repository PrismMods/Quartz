using Quartz.Compat;
using Quartz.Compat.Interface;
using static Asserts;
static class QuartzLoggerTests {
    private sealed class NullLogger : IQuartzLogger {
        public void QuartzMsg(string msg) { }
        public void QuartzWrn(string msg) { }
        public void QuartzErr(string msg) { }
    }
    public static void TestRecentKeepsTheNewestThousand() {
        QuartzLogger.ClearRecent();
        var log = new QuartzLogger(new NullLogger());
        for(int i = 0; i < 1100; i++) log.Msg("line " + i);
        string[] r = QuartzLogger.Recent();
        Assert(r.Length == 1000 && r[0].EndsWith("line 100", StringComparison.Ordinal) && r[999].EndsWith("line 1099", StringComparison.Ordinal), "capped, oldest dropped, order kept");
    }
}
