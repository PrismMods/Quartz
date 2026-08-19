using Quartz.Features.PlayCount;
using static Asserts;
static class LevelAngleSignatureTests {
    private const string Decorated = """
        {"angleData":[0,180,90.0,270],"settings":{"bgColor":"000000","zoom":100},"actions":[{"floor":3,"eventType":"MoveCamera"}],"decorations":[{"floor":1,"eventType":"AddDecoration"}]}
        """;
    private const string Restyled = """
        {"angleData":[0.0, 180, 90, 270.000],"settings":{"bgColor":"ff0000","zoom":250},"actions":[{"floor":3,"eventType":"MoveCamera"},{"floor":5,"eventType":"Pause"}],"decorations":[]}
        """;
    private const string Rechart = """
        {"angleData":[0,180,90,180],"settings":{"bgColor":"000000"},"actions":[]}
        """;
    public static void TestVisualEditsKeepTheSameSignature() {
        string sig = LevelAngleSignature.Extract(Decorated);
        Assert(sig != null, "angleData must produce a signature");
        Assert(sig == LevelAngleSignature.Extract(Restyled), "camera/decoration/settings edits must not change the signature");
    }
    public static void TestAngleEditsChangeTheSignature() =>
        Assert(LevelAngleSignature.Extract(Decorated) != LevelAngleSignature.Extract(Rechart), "a changed angle must change the signature");
    public static void TestOldFormatUsesPathData() {
        string sig = LevelAngleSignature.Extract("""{"pathData":"RRLLDU","settings":{"zoom":100}}""");
        Assert(sig == "path:RRLLDU", "pathData must drive the signature on old-format levels");
        Assert(sig == LevelAngleSignature.Extract("""{"pathData":"RRLLDU","settings":{"zoom":400},"decorations":[1]}"""), "old-format visual edits must not change the signature");
    }
    public static void TestUnparseableInputFallsBackToNull() {
        Assert(LevelAngleSignature.Extract("""{"angleData":[0,nope,90]}""") == null, "garbage angles must not yield a signature");
        Assert(LevelAngleSignature.Extract("{}") == null, "a file with neither field must not yield a signature");
        Assert(LevelAngleSignature.Extract("") == null, "empty input must not yield a signature");
    }
}
