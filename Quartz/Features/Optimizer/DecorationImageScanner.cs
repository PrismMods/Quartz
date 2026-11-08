using System.Text;
namespace Quartz.Features.Optimizer;
// Unity-free: pulls every "decorationImage" string out of raw .adofai text without a
// full parse, so the preloader knows what to fetch before the game reads the level.
// The game's lenient JSON (trailing commas, comments) never matters here because only
// the key/value pair itself is read. Over-inclusion is harmless: an image nobody ends
// up using is just a cache entry the game marks unused.
public static class DecorationImageScanner {
    private const string Key = "\"decorationImage\"";
    public static List<string> Scan(string text) {
        List<string> found = [];
        if(string.IsNullOrEmpty(text)) return found;
        HashSet<string> seen = new(StringComparer.Ordinal);
        int at = 0;
        while((at = text.IndexOf(Key, at, StringComparison.Ordinal)) >= 0) {
            int i = at + Key.Length;
            at = i;
            i = SkipSpace(text, i);
            if(i >= text.Length || text[i] != ':') continue;
            i = SkipSpace(text, i + 1);
            if(i >= text.Length || text[i] != '"') continue;
            if(!TryReadString(text, i + 1, out string value, out int end)) continue;
            at = end;
            value = value.Trim();
            if(value.Length == 0) continue;
            if(value.StartsWith("prefab:", StringComparison.OrdinalIgnoreCase)) continue;
            if(seen.Add(value)) found.Add(value);
        }
        return found;
    }
    private static int SkipSpace(string s, int i) {
        while(i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i;
    }
    // Reads a JSON string body starting just past the opening quote.
    private static bool TryReadString(string s, int i, out string value, out int end) {
        StringBuilder sb = new();
        while(i < s.Length) {
            char c = s[i];
            if(c == '"') {
                value = sb.ToString();
                end = i + 1;
                return true;
            }
            if(c == '\n' || c == '\r') break;
            if(c != '\\') {
                sb.Append(c);
                i++;
                continue;
            }
            if(i + 1 >= s.Length) break;
            char e = s[i + 1];
            switch(e) {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if(i + 5 < s.Length && int.TryParse(s.Substring(i + 2, 4),
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out int code)) {
                        sb.Append((char)code);
                        i += 6;
                        continue;
                    }
                    value = null;
                    end = i;
                    return false;
                default: sb.Append(e); break;
            }
            i += 2;
        }
        value = null;
        end = i;
        return false;
    }
    // file:// URI for UnityWebRequest. Built by hand so '#', '?' and '%' in file
    // names stay part of the path instead of becoming a fragment/query.
    public static string ToFileUri(string fullPath) {
        string path = fullPath.Replace('\\', '/');
        string[] parts = path.Split('/');
        StringBuilder sb = new("file://");
        for(int p = 0; p < parts.Length; p++) {
            if(p > 0) sb.Append('/');
            string part = parts[p];
            bool drive = p == 0 && part.Length == 2 && part[1] == ':';
            if(drive) sb.Append('/').Append(part);
            else sb.Append(Uri.EscapeDataString(part));
        }
        return sb.ToString();
    }
}
