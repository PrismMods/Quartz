using Quartz.IO;
using static Asserts;
static class FaqDocumentTests {
    public static void TestShippedDefaultIsUsable() {
        List<FaqEntry> english = FaqDocument.Parse(FaqDocument.Default, "en-US");
        Assert(english.Count > 0, "default FAQ has entries");
        foreach(FaqEntry entry in english) {
            Assert(!string.IsNullOrWhiteSpace(entry.Question), "default entry has a question");
            Assert(!string.IsNullOrWhiteSpace(entry.Answer), "default entry has an answer");
            Assert(!string.IsNullOrWhiteSpace(entry.Category), "default entry has a category");
        }
        List<FaqEntry> korean = FaqDocument.Parse(FaqDocument.Default, "ko-KR");
        Assert(korean.Count == english.Count, "every language sees the same entries");
        for(int i = 0; i < korean.Count; i++)
            Assert(korean[i].Question != english[i].Question, "default entries are translated, not shared");
    }
    public static void TestLanguagePicksExactThenEnglishThenAnything() {
        const string json = """
        {
          "entries": [
            { "question": { "en-US": "en q", "ko-KR": "ko q" }, "answer": { "en-US": "en a", "ko-KR": "ko a" } },
            { "question": { "en-US": "only english" }, "answer": "shared" },
            { "question": { "zh-CN": "only chinese" }, "answer": "shared" }
          ]
        }
        """;
        List<FaqEntry> ko = FaqDocument.Parse(json, "ko-KR");
        Assert(ko[0].Question == "ko q" && ko[0].Answer == "ko a", "exact language wins");
        Assert(ko[1].Question == "only english", "missing language falls back to en-US");
        Assert(ko[2].Question == "only chinese", "no en-US falls back to whatever is there");
        Assert(ko[1].Answer == "shared", "plain strings apply to every language");
    }
    public static void TestShapesUsersAreLikelyToWrite() {
        const string topLevelArray = """
        [
          { "question": "q", "answer": ["line one", "line two"] },
          { "question": "  spaced  ", "answer": "  spaced  ", "category": "  Cat  " },
          { "answer": "orphan answer" },
          { "question": "", "answer": "" },
          "not an object"
        ]
        """;
        List<FaqEntry> entries = FaqDocument.Parse(topLevelArray, "en-US");
        Assert(entries.Count == 3, "empty entries and non-objects are skipped");
        Assert(entries[0].Answer == "line one\nline two", "a list of lines becomes a multi-line answer");
        Assert(entries[1].Question == "spaced" && entries[1].Answer == "spaced" && entries[1].Category == "Cat", "text is trimmed");
        Assert(entries[2].Question == "?", "an answer with no question still shows up");
        Assert(entries[0].Category == null, "category stays unset when absent");
    }
    public static void TestUnreadableFilesDoNotThrowPastTheParser() {
        Assert(FaqDocument.Parse("{}", "en-US").Count == 0, "an object with no entries is empty, not an error");
        Assert(FaqDocument.Parse("""{ "entries": {} }""", "en-US").Count == 0, "entries of the wrong type is empty");
        bool threw = false;
        try {
            FaqDocument.Parse("{ oh no", "en-US");
        } catch {
            threw = true;
        }
        Assert(threw, "malformed JSON throws so the page can report it");
    }
}
