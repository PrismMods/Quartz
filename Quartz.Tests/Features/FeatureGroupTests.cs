using System.Text.RegularExpressions;
using static Asserts;
static class FeatureGroupTests {
    public static void TestEveryFeatureGroupHasACategory() {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        HashSet<string> registered = CategoryKeys(Path.Combine(repo, "Quartz", "UI", "Nav", "CorePages.cs"));
        List<string> bad = [];
        int found = 0;
        foreach(string file in Directory.GetFiles(Path.Combine(repo, "Quartz", "Features"), "*.cs", SearchOption.AllDirectories)) {
            Match m = Regex.Match(File.ReadAllText(file), @"\[Feature\(""([^""]+)"", ""([^""]+)""");
            if(!m.Success) continue;
            found++;
            string id = m.Groups[1].Value, group = m.Groups[2].Value;
            if(!registered.Contains(group)) bad.Add($"{id}: '{group}' has no togglable category in CorePages");
        }
        Assert(found > 0, "the scan found the feature entry classes");
        Assert(bad.Count == 0, string.Join("; ", bad));
    }
    static HashSet<string> CategoryKeys(string path) {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach(string line in File.ReadAllLines(path)) {
            string trimmed = line.TrimStart();
            if(!trimmed.StartsWith("Category(\"", StringComparison.Ordinal)) continue;
            if(!trimmed.Contains("togglable: true", StringComparison.Ordinal)) continue;
            int start = trimmed.IndexOf('"') + 1;
            int end = trimmed.IndexOf('"', start);
            keys.Add(trimmed[start..end]);
        }
        return keys;
    }
}
