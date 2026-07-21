#nullable disable
using System.Text;
using Newtonsoft.Json.Linq;
namespace Quartz.IO;
public static class ProfileBundle {
    public static Dictionary<string, byte[]> ReadFiles(
        JObject files, ISet<string> excluded,
        bool asPreset, string configFileName, string[] presetImposed
    ) {
        Dictionary<string, byte[]> imported = new(StringComparer.OrdinalIgnoreCase);
        foreach(JProperty prop in files.Properties()) {
            string fileName = Path.GetFileName(prop.Name);
            if(!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || excluded.Contains(fileName)) continue;
            JToken contents = asPreset
                ? StripPresetImposed(fileName, prop.Value, configFileName, presetImposed)
                : prop.Value;
            imported[fileName] = Encoding.UTF8.GetBytes(contents.ToString());
        }
        return imported;
    }
    public static JToken StripPresetImposed(string fileName, JToken contents, string configFileName, string[] presetImposed) {
        if(!fileName.Equals(configFileName, StringComparison.OrdinalIgnoreCase)
            || contents is not JObject settings) return contents;
        JObject stripped = (JObject)settings.DeepClone();
        foreach(string field in presetImposed) stripped.Remove(field);
        return stripped;
    }
}
