using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Layout;
internal static partial class KvStore {
    internal const string QkvExtension = ".qkv";
    private const string DefaultQkvExportName = "quartz-keyviewer-preset.qkv";
    private const string QkvFilterName = "Quartz KeyViewer Preset";
    internal const string SettingsKey = "quartzSettings";
    private const string FormatKey = "quartzFormat";
    private const int FormatVersion = 1;
    private static readonly string[] TransferableSettings = [
        nameof(KeyViewerSettings.Enabled),
        nameof(KeyViewerSettings.ShowOutsideGame),
        nameof(KeyViewerSettings.SyncToKeyLimiter),
        nameof(KeyViewerSettings.CountFormatting),
        nameof(KeyViewerSettings.IndependentInput),
        nameof(KeyViewerSettings.DmOffsetX),
        nameof(KeyViewerSettings.DmOffsetY),
        nameof(KeyViewerSettings.DmScale),
        nameof(KeyViewerSettings.DmNoteEffect),
        nameof(KeyViewerSettings.DmNoteSpeed),
        nameof(KeyViewerSettings.DmTrackHeight),
        nameof(KeyViewerSettings.DmNoteReverse),
        nameof(KeyViewerSettings.DmShowCounter),
        nameof(KeyViewerSettings.DmFadePx),
        nameof(KeyViewerSettings.DmDelayedNoteEnabled),
        nameof(KeyViewerSettings.DmShortNoteThresholdMs),
        nameof(KeyViewerSettings.DmShortNoteMinLengthPx),
        nameof(KeyViewerSettings.DmKeyDisplayDelayMs),
        nameof(KeyViewerSettings.DmMinLitMs),
        nameof(KeyViewerSettings.DmCssEnabled),
        nameof(KeyViewerSettings.DmCssText),
        nameof(KeyViewerSettings.DmCssPath),
    ];
    internal static string BuildExportJson(KvExportFormat format) {
        JObject root = JObject.Parse(Current.ToJson(true));
        KvExportShaping.Shape(root, format);
        if(format == KvExportFormat.Quartz) AttachSettings(root);
        return root.ToString(Formatting.Indented);
    }
    internal static List<string> DmNoteGaps() {
        try {
            return KvExportShaping.DetectDmNoteGaps(JObject.Parse(Current.ToJson(true)));
        } catch(Exception e) {
            Msg("DM Note compatibility scan failed: " + e.Message);
            return [];
        }
    }
    private static void AttachSettings(JObject root) {
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        if(conf == null) return;
        try {
            if(conf.Serialize() is not JObject settings) return;
            root[SettingsKey] = settings;
            root[FormatKey] = FormatVersion;
        } catch(Exception e) {
            Msg("Could not attach settings to the export: " + e.Message);
        }
    }
    internal static int ApplyTransferSettings(JObject importedRoot) {
        if(importedRoot?[SettingsKey] is not JObject imported) return 0;
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        if(conf == null) return 0;
        try {
            if(conf.Serialize() is not JObject live) return 0;
            int applied = 0;
            foreach(string key in TransferableSettings) {
                JToken value = imported[key];
                if(value == null || value.Type == JTokenType.Null) continue;
                live[key] = value.DeepClone();
                applied++;
            }
            if(applied == 0) return 0;
            conf.Deserialize(live);
            KeyViewerOverlay.Save();
            return applied;
        } catch(Exception e) {
            Err("Could not apply the imported settings: " + e);
            return 0;
        }
    }
}
