using System.Text.Json;
using System.Text.RegularExpressions;
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
    // A literal key handed to a call that translates it (or to QuartzShell.Label, a key for Facet to translate).
    static readonly Regex KeyCall = new(@"(?:QuartzHost\.Text|QuartzHost\.Translate|Tr\.Get|GenerateUI\.Tr|QuartzShell\.Label)\(\s*""([A-Z][A-Z0-9_]+)""\s*[,)]");
    // Facet translates the text of the Ui calls it makes; a hand-set .text is drawn as given, so a key there shows raw.
    static readonly Regex RawKeyText = new(@"\.text\s*\+?=.*QuartzShell\.Label\(");

    /// <summary>Every literal Lang key the mod's code asks for is in en-US and ko-KR (any Lang folder), and no hand-set text
    /// takes a key meant for Facet to translate.</summary>
    public static void TestCodeKeysExist() {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string src = Path.Combine(repo, "Quartz");
        HashSet<string> Keys(string language) => Directory.GetFiles(src, language + ".json", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "Lang")
            .SelectMany(f => ReadLanguageKeys(f, language)).ToHashSet(StringComparer.Ordinal);
        HashSet<string> english = Keys("en-US"), korean = Keys("ko-KR");
        List<string> missing = [], raw = [];
        foreach(string file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)) {
            if(file.Contains("SelfTest") || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            string[] lines = File.ReadAllLines(file);
            for(int i = 0; i < lines.Length; i++) {
                foreach(Match m in KeyCall.Matches(lines[i])) {
                    string key = m.Groups[1].Value;
                    if(!english.Contains(key) || !korean.Contains(key))
                        missing.Add($"{Path.GetRelativePath(repo, file)}:{i + 1} {key}{(english.Contains(key) ? "" : " en-US")}{(korean.Contains(key) ? "" : " ko-KR")}");
                }
                if(RawKeyText.IsMatch(lines[i])) raw.Add($"{Path.GetRelativePath(repo, file)}:{i + 1}");
            }
        }
        Assert(missing.Count == 0, "keys missing from Lang: " + string.Join("; ", missing));
        Assert(raw.Count == 0, "hand-set .text takes a Facet key (use QuartzHost.Text): " + string.Join("; ", raw));
    }
    static HashSet<string> ReadLanguageKeys(string path, string language) {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement block = doc.RootElement.GetProperty(language);
        return block.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    }
}
