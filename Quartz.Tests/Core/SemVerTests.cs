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
        // ToString round-trips the parseable forms.
        Assert(SemVer.TryParse("2.0.0", out SemVer stable), "stable parse");
        Assert(stable.ToString() == "2.0.0", "stable omits channel + build");
        Assert(SemVer.TryParse("2.0.0-alpha.17", out SemVer alpha), "alpha parse");
        Assert(alpha.ToString() == "2.0.0-alpha-17", "prerelease includes channel + build");

        // Channel parsing accepts the documented aliases and tolerates junk.
        Assert(SemVer.ParseChannel("rc") == ReleaseChannel.ReleaseCandidate, "rc alias");
        Assert(SemVer.ParseChannel("release-candidate") == ReleaseChannel.ReleaseCandidate, "release-candidate alias");
        Assert(SemVer.ParseChannel("releasecandidate") == ReleaseChannel.ReleaseCandidate, "run-together alias");
        Assert(SemVer.ParseChannel("dev") == ReleaseChannel.Dev, "dev alias");
        Assert(SemVer.ParseChannel("") == ReleaseChannel.Stable, "empty defaults to stable");
        Assert(SemVer.ParseChannel("  BETA  ") == ReleaseChannel.Beta, "trim + case-insensitive");
        Assert(SemVer.ParseChannel("nonsense") == ReleaseChannel.Stable, "unknown defaults to stable");

        // A stable release ignores its build component when ordering.
        SemVer stableA = new(2, 0, 0, ReleaseChannel.Stable, 99);
        Assert(stableA.CompareTo(stable) == 0, "stable build number is ignored");

        // Equal versions compare as equal; unparseable input sorts oldest.
        Assert(alpha.CompareTo(new SemVer(2, 0, 0, ReleaseChannel.Alpha, 17)) == 0, "equal versions compare 0");
        Assert(SemVer.Compare("not-a-version", "2.0.0") < 0, "unparseable sorts oldest");
        Assert(SemVer.Compare("2.0.0", "2.0.0") == 0, "identical strings compare equal");
    }
}
