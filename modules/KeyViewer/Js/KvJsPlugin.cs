using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Js;
public sealed class KvJsPluginRecord {
    public string Name = "";
    public string Path = "";
    public string Content = "";
    public bool Enabled = true;
    private static readonly Regex IdPattern = new(@"//\s*@id(?:\s*:\s*|\s+)([a-z0-9-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public string PluginId {
        get {
            string content = Content ?? "";
            int line = 0, pos = 0;
            while(line < 20 && pos < content.Length) {
                int end = content.IndexOf('\n', pos);
                if(end < 0) end = content.Length;
                Match m = IdPattern.Match(content, pos, end - pos);
                if(m.Success) return m.Groups[1].Value.ToLowerInvariant();
                pos = end + 1;
                line++;
            }
            return NormalizeId(Name);
        }
    }
    public static string NormalizeId(string fileName) {
        string s = (fileName ?? "plugin").ToLowerInvariant();
        s = Regex.Replace(s, @"\.(js|mjs|ts)$", "");
        s = Regex.Replace(s, "[^a-z0-9-_]+", "-");
        s = Regex.Replace(s, "-{2,}", "-").Trim('-');
        return s.Length == 0 ? "plugin" : s;
    }
    public JObject Serialize() => new() {
        [nameof(Name)] = Name,
        [nameof(Path)] = Path,
        [nameof(Content)] = Content,
        [nameof(Enabled)] = Enabled,
    };
    public static KvJsPluginRecord Deserialize(JToken token) => new() {
        Name = token.Value<string>(nameof(Name)) ?? "",
        Path = token.Value<string>(nameof(Path)) ?? "",
        Content = token.Value<string>(nameof(Content)) ?? "",
        Enabled = token.Value<bool?>(nameof(Enabled)) ?? true,
    };
}
