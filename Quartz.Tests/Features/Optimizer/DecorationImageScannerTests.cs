#nullable enable
using Quartz.Features.Optimizer;
using static Asserts;
static class DecorationImageScannerTests {
    public static void TestScansDecorationImages() {
        List<string> found = DecorationImageScanner.Scan("""
            {"actions":[
              { "floor": 1, "eventType": "AddDecoration", "decorationImage": "bg/a.png", },
              { "floor": 2, "eventType": "AddDecoration", "decorationImage" :  "b.png" },
              { "floor": 3, "eventType": "AddParticle", "decorationImage": "a \"quoted\" \u00e9.png" },
              { "floor": 4, "eventType": "MoveDecorations", "decorationImage": "bg/a.png" },
              { "floor": 5, "eventType": "AddDecoration", "decorationImage": "prefab:Cloud" },
              { "floor": 6, "eventType": "AddDecoration", "decorationImage": "" },
              { "floor": 7, "eventType": "AddDecoration", "decorationImage": "sub\\c.png" },
            ]}
            """);
        Assert(found.Count == 4, "four distinct images, got " + found.Count);
        Assert(found[0] == "bg/a.png", "first image kept in file order");
        Assert(found[1] == "b.png", "spacing around the colon tolerated");
        Assert(found[2] == "a \"quoted\" \u00e9.png", "JSON escapes decoded");
        Assert(found[3] == "sub\\c.png", "escaped backslash decoded");
        Assert(!found.Contains("prefab:Cloud"), "prefab decorations skipped");
    }
    public static void TestIgnoresMalformedEntries() {
        Assert(DecorationImageScanner.Scan("").Count == 0, "empty text");
        Assert(DecorationImageScanner.Scan("\"decorationImage\": 5").Count == 0, "non-string value");
        Assert(DecorationImageScanner.Scan("\"decorationImage\": \"unterminated").Count == 0, "unterminated string");
    }
    public static void TestBuildsFileUris() {
        Assert(DecorationImageScanner.ToFileUri("/Users/me/My Level/#1 bg.png") == "file:///Users/me/My%20Level/%231%20bg.png",
            "unix path escaped segment by segment");
        Assert(DecorationImageScanner.ToFileUri("C:\\Levels\\a b.png") == "file:///C:/Levels/a%20b.png",
            "windows drive letter kept");
    }
}
