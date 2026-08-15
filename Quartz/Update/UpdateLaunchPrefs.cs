using System.IO;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Update;
// Mirrors the in-game update settings into Runtime/update.json, where the
// launch-time bootstrap's update engine reads them before this assembly is
// even loaded. Enabled is user-owned (hand-edited to opt out of launch
// checks), so a rewrite preserves it.
public static class UpdateLaunchPrefs {
    public static string RuntimeRoot { get; private set; }
    public static void Bind(string runtimeRoot) {
        RuntimeRoot = runtimeRoot;
        Write();
    }
    public static void Write() {
        string root = RuntimeRoot;
        if(string.IsNullOrEmpty(root)) return;
        try {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "update.json");
            bool enabled = true;
            if(File.Exists(path)) {
                try {
                    enabled = JObject.Parse(File.ReadAllText(path)).Value<bool?>("Enabled") ?? true;
                } catch(Exception e) { Diag.Ignore(e); }
            }
            ReleaseChannel channel = MainCore.Conf.GetUpdateChannel();
            JObject root2 = new() {
                ["Enabled"] = enabled,
                ["Channel"] = channel == ReleaseChannel.Stable ? "stable" : SemVer.ChannelTag(channel),
                ["Skipped"] = MainCore.Conf.SkippedVersion ?? "",
            };
            AtomicFile.WriteAllText(path, root2.ToString());
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Update] couldn't write launch update prefs: {e.Message}");
        }
    }
}
