using Quartz.Core;
using static Asserts;
static class DiagTests {
    public static void TestIgnoreIsCountedAndSilentByDefault() {
        using DiagScope scope = new();
        Diag.Verbose = false;
        Diag.Ignore(new InvalidOperationException("quiet"));
        Diag.Ignore(new InvalidOperationException("also quiet"));
        Assert(Diag.Ignored == 2, "every deliberate swallow is counted");
        Assert(scope.Lines.Count == 0, "a deliberate swallow writes nothing by default");
    }
    public static void TestVerboseSurfacesIgnoredExceptions() {
        using DiagScope scope = new();
        Diag.Verbose = true;
        Diag.Ignore(new InvalidOperationException("noisy"));
        Assert(scope.Lines.Count == 1, "verbose mode surfaces the swallow");
        Assert(scope.Lines[0].Contains("InvalidOperationException") && scope.Lines[0].Contains("noisy"),
            "the surfaced line names the exception type and message");
    }
    public static void TestWarnAlwaysReachesTheSink() {
        using DiagScope scope = new();
        Diag.Verbose = false;
        Diag.Warn(new InvalidOperationException("lost the file"), "Profiles");
        Assert(Diag.Warned == 1, "a warned failure is counted separately from an ignored one");
        Assert(Diag.Ignored == 0, "warning does not inflate the ignore count");
        Assert(scope.Lines.Count == 1 && scope.Lines[0].StartsWith("[Profiles] "),
            "the context prefixes the logged line");
        Assert(scope.Lines[0].Contains("lost the file"), "the exception message is carried through");
    }
    public static void TestWarnWithoutContextStillIdentifiesItself() {
        using DiagScope scope = new();
        Diag.Warn(new InvalidOperationException("bare"), null);
        Diag.Warn(new InvalidOperationException("blank"), "");
        Assert(scope.Lines.Count == 2, "a null and an empty context both reach the sink");
        Assert(scope.Lines[0].StartsWith("[Diag] ") && scope.Lines[1].StartsWith("[Diag] "),
            "a missing context falls back to a usable prefix");
    }
    public static void TestNullExceptionIsDescribedNotDereferenced() {
        using DiagScope scope = new();
        Diag.Warn(null, "Startup");
        Assert(scope.Lines.Count == 1 && scope.Lines[0].Contains("no exception"),
            "a null exception is described rather than thrown on");
    }
    public static void TestAFailingSinkIsCountedNotPropagated() {
        using DiagScope scope = new(_ => throw new InvalidOperationException("sink down"));
        Diag.Warn(new InvalidOperationException("real problem"), "Net");
        Assert(Diag.SinkFailures == 1, "a logger that throws is counted");
        Assert(Diag.Warned == 1, "the warning itself is still counted");
    }
    public static void TestUnboundDiagIsSafe() {
        using DiagScope scope = new();
        Diag.Unbind();
        Diag.Verbose = true;
        Diag.Warn(new InvalidOperationException("before startup"), "Early");
        Diag.Ignore(new InvalidOperationException("before startup"));
        Assert(Diag.Warned == 1 && Diag.Ignored == 1, "counters work before a logger exists");
        Assert(Diag.SinkFailures == 0, "no sink is not a sink failure");
    }
    sealed class DiagScope : IDisposable {
        private readonly bool verbose = Diag.Verbose;
        public List<string> Lines { get; } = [];
        public DiagScope(Action<string> sink = null) {
            Diag.Reset();
            Diag.Bind(sink ?? Lines.Add);
        }
        public void Dispose() {
            Diag.Unbind();
            Diag.Reset();
            Diag.Verbose = verbose;
        }
    }
}
