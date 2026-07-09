using System.Text.Json;
using static Asserts;
static class LocalizationParityTests {
    public static void TestLocalizationParity() {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string lang = Path.Combine(repo, "Quartz", "Resource", "Export", "Lang");
        HashSet<string> english = ReadLanguageKeys(Path.Combine(lang, "en-US.json"), "en-US");
        HashSet<string> korean = ReadLanguageKeys(Path.Combine(lang, "ko-KR.json"), "ko-KR");
        string[] missingKorean = english.Except(korean).OrderBy(x => x).ToArray();
        string[] missingEnglish = korean.Except(english).OrderBy(x => x).ToArray();
        Assert(missingKorean.Length == 0, "missing ko-KR: " + string.Join(", ", missingKorean));
        Assert(missingEnglish.Length == 0, "missing en-US: " + string.Join(", ", missingEnglish));
    }
    static HashSet<string> ReadLanguageKeys(string path, string language) {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement block = doc.RootElement.GetProperty(language);
        return block.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    }
}
