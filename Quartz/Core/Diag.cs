using System;
using System.Threading;
namespace Quartz.Core;
public static class Diag {
    private static Action<string> sink;
    private static int ignored;
    private static int warned;
    private static int sinkFailures;
    public static bool Verbose { get; set; } = ReadVerboseEnv();
    public static int Ignored => Volatile.Read(ref ignored);
    public static int Warned => Volatile.Read(ref warned);
    public static int SinkFailures => Volatile.Read(ref sinkFailures);
    public static void Bind(Action<string> warn) => sink = warn;
    public static void Unbind() => sink = null;
    public static void Reset() {
        Interlocked.Exchange(ref ignored, 0);
        Interlocked.Exchange(ref warned, 0);
        Interlocked.Exchange(ref sinkFailures, 0);
    }
    public static void Ignore(Exception e) {
        Interlocked.Increment(ref ignored);
        if(Verbose) Emit("[Diag] ignored " + Describe(e));
    }
    public static void Warn(Exception e, string context) {
        Interlocked.Increment(ref warned);
        Emit("[" + (string.IsNullOrEmpty(context) ? "Diag" : context) + "] " + Describe(e));
    }
    private static string Describe(Exception e) =>
        e == null ? "(no exception)" : e.GetType().Name + ": " + e.Message;
    private static void Emit(string message) {
        Action<string> target = sink;
        if(target == null) return;
        try {
            target(message);
        } catch(Exception e) {
            Diag.Ignore(e);
            Interlocked.Increment(ref sinkFailures);
        }
    }
    private static bool ReadVerboseEnv() {
        try {
            string raw = Environment.GetEnvironmentVariable("QUARTZ_DIAG");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        } catch(Exception e) {
            Diag.Ignore(e);
            Interlocked.Increment(ref sinkFailures);
            return false;
        }
    }
}
