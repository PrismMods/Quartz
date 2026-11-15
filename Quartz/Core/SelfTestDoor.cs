#if DEBUG
using Quartz.Features.Tuf;
namespace Quartz.Core;
/// <summary>
/// Debug-only stand-in for the network during a self-test run. Each call site reads its field first; null means the real
/// path. While installed, any request method a run still reaches is counted and throws instead of sending.
/// </summary>
internal static class SelfTestDoor {
    internal static bool Installed;
    internal static int Network;
    internal static Func<string, TufSort, bool, int, TufDifficultyFilter, CancellationToken, Task<TufPage>> Fetch;
    internal static Func<string, TufPackSort, bool, int, int, CancellationToken, Task<TufPacksPage>> FetchPacks;
    internal static Func<TufPack, CancellationToken, Task<IReadOnlyList<TufPackItem>>> FetchPackLevels;
    internal static Func<string, byte[]> Preview;
    // A level's rollback copies, by level id, read before the disk's.
    internal static Func<int, int> SnapshotCount;
    internal static Func<int, IReadOnlyList<TufSnapshot>> Snapshots;
    internal static bool NoAutoCheck;
    internal static int Fetches, Previews, UpdateChecks;

    internal static void Install() {
        Installed = true;
        NoAutoCheck = true;
        Defaults();
        Network = Fetches = Previews = UpdateChecks = 0;
    }

    internal static void Defaults() {
        Fetch = (_, _, _, _, _, _) => Task.FromResult(new TufPage([], false));
        FetchPacks = (_, _, _, _, _, _) => Task.FromResult(new TufPacksPage([], 0));
        FetchPackLevels = (_, _) => Task.FromResult<IReadOnlyList<TufPackItem>>([]);
        Preview = _ => null;
        SnapshotCount = null;
        Snapshots = null;
    }

    internal static void Reset() {
        Installed = NoAutoCheck = false;
        Fetch = null;
        FetchPacks = null;
        FetchPackLevels = null;
        Preview = null;
        SnapshotCount = null;
        Snapshots = null;
        Network = Fetches = Previews = UpdateChecks = 0;
    }

    /// <summary>The top of every request method: while installed, counts the request and throws instead of sending.
    /// May run off the main thread.</summary>
    internal static void Guard() {
        if(!Installed) return;
        Interlocked.Increment(ref Network);
        throw new InvalidOperationException("no network in a self-test run");
    }
}
#endif
