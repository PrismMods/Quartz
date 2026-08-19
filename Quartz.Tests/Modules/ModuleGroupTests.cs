using System.Text.Json;
using static Asserts;
static class ModuleGroupTests {
    public static void TestEveryModuleGroupHasACategory() {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        HashSet<string> groups = GroupKeys(Path.Combine(repo, "tools", "module-groups.json"));
        HashSet<string> registered = CategoryKeys(Path.Combine(repo, "Quartz", "UI", "Nav", "CorePages.cs"));
        List<string> bad = [];
        foreach(string manifest in Directory.GetFiles(Path.Combine(repo, "modules"), "module.json", SearchOption.AllDirectories)) {
            string text = File.ReadAllText(manifest).Replace("__COREABI__", "0", StringComparison.Ordinal);
            using JsonDocument doc = JsonDocument.Parse(text);
            string group = doc.RootElement.GetProperty("group").GetString();
            string id = doc.RootElement.GetProperty("id").GetString();
            if(!groups.Contains(group)) bad.Add($"{id}: '{group}' is not in tools/module-groups.json");
            if(!registered.Contains(group)) bad.Add($"{id}: '{group}' has no togglable category in CorePages");
        }
        Assert(bad.Count == 0, string.Join("; ", bad));
    }
    static HashSet<string> GroupKeys(string path) {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateArray().Select(e => e.GetProperty("key").GetString()).ToHashSet(StringComparer.Ordinal);
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
