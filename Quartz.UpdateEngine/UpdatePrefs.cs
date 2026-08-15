using System.IO;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.UpdateEngine;
// Runtime/update.json — written by the payload after each successful launch
// (channel + skipped release from the in-game settings), readable and editable
// by hand. Enabled=false is the kill switch for launch-time update checks.
public sealed class UpdatePrefs {
    public bool Enabled = true;
    public ReleaseChannel Channel = SemVer.ParseChannel(EngineInfo.Channel);
    public string Skipped = "";
    public static UpdatePrefs Load(string runtimeRoot) {
        UpdatePrefs prefs = new();
        try {
            string path = Path.Combine(runtimeRoot, "update.json");
            if(!File.Exists(path)) return prefs;
            JObject root = JObject.Parse(File.ReadAllText(path));
            prefs.Enabled = root.Value<bool?>("Enabled") ?? true;
            string channel = root.Value<string>("Channel");
            if(!string.IsNullOrWhiteSpace(channel)) prefs.Channel = SemVer.ParseChannel(channel);
            prefs.Skipped = root.Value<string>("Skipped") ?? "";
        } catch(Exception e) {
            prefs.Message = "update.json is unreadable, using defaults: " + e.Message;
        }
        return prefs;
    }
    public string Message;
}
