using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Overlay;
public static class OverlaySwitch {
    private const string FileName = "Overlay.json";
    private const string LegacyFileName = "OverlayPanels.json";
    public static SettingsFile<OverlaySettings> ConfMgr { get; private set; }
    public static OverlaySettings Conf => ConfMgr?.Data;
    public static bool Enabled {
        get {
            EnsureConf();
            return ConfMgr.Data.Enabled;
        }
        set {
            EnsureConf();
            if(ConfMgr.Data.Enabled == value) return;
            ConfMgr.Data.Enabled = value;
            ConfMgr.RequestSave();
        }
    }
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        bool fresh = !File.Exists(Path.Combine(MainCore.Paths.RootPath, FileName));
        ConfMgr = SettingsFile<OverlaySettings>.Loaded(FileName);
        if(fresh && TryReadLegacyEnabled(out bool legacy) && legacy != ConfMgr.Data.Enabled) {
            ConfMgr.Data.Enabled = legacy;
            ConfMgr.Save();
        }
    }
    public static void Save() => ConfMgr?.RequestSave();
    private static bool TryReadLegacyEnabled(out bool enabled) {
        enabled = true;
        string path = Path.Combine(MainCore.Paths.RootPath, LegacyFileName);
        try {
            if(!File.Exists(path)) return false;
            if(JToken.Parse(File.ReadAllText(path))[nameof(OverlaySettings.Enabled)] is not { } token) return false;
            if(token.Type != JTokenType.Boolean) return false;
            enabled = (bool)token;
            MainCore.Log.Msg($"[Overlay] adopted the master switch from {LegacyFileName} (enabled: {enabled})");
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Overlay] couldn't read the master switch from {LegacyFileName}: {e.Message}");
            return false;
        }
    }
}
