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
}
