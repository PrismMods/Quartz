using System.Text;
using Newtonsoft.Json.Linq;
using Quartz.IO;
using static Asserts;
static class ProfileBundleTests {
    // What ProfileManager passes into ProfileBundle: the config file name off
    // MainCore.Paths.ConfigPath, and the imposed list off nameof(CoreSettings.Language).
    // Both of those pull in Unity, so the values are restated here rather than linked.
    private const string Config = "Settings.json";
    private static readonly string[] Imposed = ["Language"];
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase) {
        "PlayCount.json",
        "Profiles.json",
    };
    private static JObject Bundle() => new() {
        [Config] = new JObject {
            ["Language"] = "ko-KR",
            ["UIScale"] = 0.85,
            ["FontName"] = "Pretendard",
        },
        ["KeyViewer.json"] = new JObject {
            ["Language"] = "ko-KR",
            ["Enabled"] = true,
        },
        ["Panels.json"] = new JObject { ["Enabled"] = false },
        ["PlayCount.json"] = new JObject { ["Total"] = 42 },
    };
    private static JObject Parse(byte[] bytes) => JObject.Parse(Encoding.UTF8.GetString(bytes));
    public static void TestPresetStripsImposedFields() {
        Dictionary<string, byte[]> preset = ProfileBundle.ReadFiles(Bundle(), Excluded, true, Config, Imposed);
        JObject settings = Parse(preset[Config]);
        Assert(settings["Language"] == null, "preset import drops the config Language key");
        Assert(settings["UIScale"] != null && settings["FontName"] != null, "preset import keeps the rest of the config");
        Assert(Parse(preset["KeyViewer.json"])["Language"] != null, "preset import strips the config file only");
        Assert(!preset.ContainsKey("PlayCount.json"), "preset import skips files a profile never owns");
    }
    public static void TestImportButtonKeepsLanguage() {
        Dictionary<string, byte[]> normal = ProfileBundle.ReadFiles(Bundle(), Excluded, false, Config, Imposed);
        Assert(Parse(normal[Config])["Language"]?.Value<string>() == "ko-KR", "the Import button restores the exported Language");
    }
    public static void TestPresetLeavesOtherFilesByteIdentical() {
        // One bundle for both reads, preset first: that also pins ReadFiles to leaving
        // its input alone, since a preset that stripped in place would strip the second read too.
        JObject files = Bundle();
        Dictionary<string, byte[]> preset = ProfileBundle.ReadFiles(files, Excluded, true, Config, Imposed);
        Dictionary<string, byte[]> normal = ProfileBundle.ReadFiles(files, Excluded, false, Config, Imposed);
        Assert(preset.Count == normal.Count, "preset import writes the same file set");
        Assert(!preset[Config].SequenceEqual(normal[Config]), "preset import rewrites the config file");
        foreach(KeyValuePair<string, byte[]> file in normal) {
            if(file.Key.Equals(Config, StringComparison.OrdinalIgnoreCase)) continue;
            Assert(preset.TryGetValue(file.Key, out byte[]? bytes) && bytes.SequenceEqual(file.Value),
                $"preset import passes {file.Key} through byte-identical");
        }
    }
}
