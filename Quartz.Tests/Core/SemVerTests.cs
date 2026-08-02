using Quartz.Core;
using static Asserts;
static class SemVerTests {
    public static void TestSemVer() {
        Assert(SemVer.TryParse("v2.0.0-alpha.17", out SemVer alpha), "alpha parse");
        Assert(SemVer.TryParse("2.0.0-beta.1", out SemVer beta), "beta parse");
        Assert(SemVer.TryParse("2.0.0", out SemVer stable), "stable parse");
        Assert(beta.CompareTo(alpha) > 0, "beta must outrank alpha");
        Assert(stable.CompareTo(beta) > 0, "stable must outrank prerelease");
        Assert(SemVer.Compare("2.0.0-alpha.10", "2.0.0-alpha.2") > 0, "numeric build ordering");
        Assert(!SemVer.TryParse("2.0", out _), "short version rejection");
    }
    public static void TestSemVerFormatAndChannels() {
        Assert(SemVer.TryParse("2.0.0", out SemVer stable), "stable parse");
        Assert(stable.ToString() == "2.0.0", "stable omits channel + build");
        Assert(SemVer.TryParse("2.0.0-alpha.17", out SemVer alpha), "alpha parse");
        Assert(alpha.ToString() == "2.0.0-alpha-17", "prerelease includes channel + build");
        Assert(SemVer.ParseChannel("rc") == ReleaseChannel.ReleaseCandidate, "rc alias");
        Assert(SemVer.ParseChannel("release-candidate") == ReleaseChannel.ReleaseCandidate, "release-candidate alias");
        Assert(SemVer.ParseChannel("releasecandidate") == ReleaseChannel.ReleaseCandidate, "run-together alias");
        Assert(SemVer.ParseChannel("dev") == ReleaseChannel.Dev, "dev alias");
        Assert(SemVer.ParseChannel("") == ReleaseChannel.Stable, "empty defaults to stable");
        Assert(SemVer.ParseChannel("  BETA  ") == ReleaseChannel.Beta, "trim + case-insensitive");
        Assert(SemVer.ParseChannel("nonsense") == ReleaseChannel.Stable, "unknown defaults to stable");
        SemVer stableA = new(2, 0, 0, ReleaseChannel.Stable, 99);
        Assert(stableA.CompareTo(stable) == 0, "stable build number is ignored");
        Assert(alpha.CompareTo(new SemVer(2, 0, 0, ReleaseChannel.Alpha, 17)) == 0, "equal versions compare 0");
        Assert(SemVer.Compare("not-a-version", "2.0.0") < 0, "unparseable sorts oldest");
        Assert(SemVer.Compare("2.0.0", "2.0.0") == 0, "identical strings compare equal");
    }
    public static void TestSemVerChannelPreference() {
        SemVer alpha98 = new(2, 0, 0, ReleaseChannel.Alpha, 98);
        SemVer alpha99 = new(2, 0, 0, ReleaseChannel.Alpha, 99);
        SemVer beta1 = new(2, 0, 0, ReleaseChannel.Beta, 1);
        SemVer rc1 = new(2, 0, 0, ReleaseChannel.ReleaseCandidate, 1);
        SemVer stable = new(2, 0, 0, ReleaseChannel.Stable, 0);
        SemVer nextAlpha = new(2, 0, 1, ReleaseChannel.Alpha, 1);
        Assert(SemVer.CompareForChannel(beta1, alpha98, ReleaseChannel.Alpha) < 0,
            "alpha channel must not be offered a same-version beta");
        Assert(SemVer.CompareForChannel(alpha98, beta1, ReleaseChannel.Alpha) > 0,
            "alpha channel returns to its own lane from beta");
        Assert(SemVer.CompareForChannel(alpha99, alpha98, ReleaseChannel.Alpha) > 0,
            "newer build wins inside the selected lane");
        Assert(SemVer.CompareForChannel(beta1, alpha98, ReleaseChannel.Beta) > 0,
            "beta channel prefers beta over a same-version alpha");
        Assert(SemVer.CompareForChannel(stable, alpha98, ReleaseChannel.Alpha) > 0,
            "the final release supersedes its own prereleases");
        Assert(SemVer.CompareForChannel(stable, beta1, ReleaseChannel.Beta) > 0,
            "beta channel still takes the final release");
        Assert(SemVer.CompareForChannel(nextAlpha, stable, ReleaseChannel.Alpha) > 0,
            "a higher core version always wins");
        Assert(SemVer.CompareForChannel(rc1, beta1, ReleaseChannel.Stable) > 0,
            "unpreferred prereleases keep their normal order");
        Assert(SemVer.CompareForChannel(alpha98, alpha98, ReleaseChannel.Alpha) == 0,
            "the installed build is never an upgrade over itself");
    }
}
