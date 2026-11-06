using Quartz.Compat.Interface;
namespace Quartz.Compat;
public sealed class QuartzLogger(IQuartzLogger logger, string prefix = null) {
    internal const int RecentMax = 1000;
    // Shared by every QuartzLogger: the debug window's log tab reads it, so it never touches the loader's file.
    private static readonly Queue<string> recent = new();
    private string F(string msg) => string.IsNullOrEmpty(prefix) ? msg : $"[{prefix}] {msg}";
    public void Msg(string msg) {
        string line = F(msg);
        Keep(line);
        logger.QuartzMsg(line);
    }
    public void Wrn(string msg) {
        string line = F(msg);
        Keep("[warn] " + line);
        logger.QuartzWrn(line);
    }
    public void Err(string msg) {
        string line = F(msg);
        Keep("[error] " + line);
        logger.QuartzErr(line);
    }
    /// <summary>The newest lines, oldest first, at most <see cref="RecentMax"/>.</summary>
    internal static string[] Recent() {
        lock(recent) return recent.ToArray();
    }
    internal static void ClearRecent() {
        lock(recent) recent.Clear();
    }
    private static void Keep(string line) {
        lock(recent) {
            recent.Enqueue(line);
            if(recent.Count > RecentMax) recent.Dequeue();
        }
    }
}
