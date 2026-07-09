using Newtonsoft.Json.Linq;
using static Quartz.Features.Interop.ReflectionHelpers;
using static Quartz.Features.Interop.Readers.KeyViewerImportShared;
namespace Quartz.Features.Interop.Readers;
internal static class JipperKeyViewerReader {
    public static int ImportJipperKeyViewer(
        SettingsImportOption option,
        SettingsImportReplaceMode mode,
        SettingsImportKeyViewerPart parts
    ) {
        if(mode == SettingsImportReplaceMode.KeepOld) return 0;
        ImportedKeyViewer imported = null;
        object runtime = GetStaticMember(SettingsImporter.FindType(option, "JipperKeyViewer.KeyViewer.KeyViewer"), "Settings");
        if(runtime != null) imported = ReadKeyViewerFromObject(runtime);
        if(imported == null || imported.Available == SettingsImportKeyViewerPart.None) {
            string json = ReadFirstText(JkvConfigPaths(option));
            if(!string.IsNullOrEmpty(json)) {
                try { imported = ReadKeyViewerFromJson(JObject.Parse(json)); } catch { }
            }
        }
        if(imported == null || imported.Available == SettingsImportKeyViewerPart.None) return 0;
        return ApplyKeyViewerImport(imported, mode, parts);
    }
    private static IEnumerable<string> JkvConfigPaths(SettingsImportOption option) {
        string dir = option.Directory;
        if(string.IsNullOrEmpty(dir)) yield break;
        string parent = Path.GetDirectoryName(dir);
        yield return Path.Combine(dir, "config", "settings.json");
        yield return Path.Combine(dir, "settings.json");
        if(!string.IsNullOrEmpty(parent)) {
            yield return Path.Combine(parent, "config", "settings.json");
            yield return Path.Combine(parent, "JipperKeyViewer", "config", "settings.json");
        }
    }
}
