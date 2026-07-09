using System.Text;
using Quartz.Core;
namespace Quartz.Addons;
public static class AddonTags {
    private static readonly Dictionary<string, Func<string>> tags =
        new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyCollection<string> Names => tags.Keys;
    public static bool IsValidName(string name) {
        if(string.IsNullOrEmpty(name)) return false;
        foreach(char c in name)
            if(!char.IsLetterOrDigit(c) && c != '_') return false;
        return true;
    }
    public static void Register(string name, Func<string> value) {
        if(!IsValidName(name))
            throw new ArgumentException($"tag name '{name}' must be non-empty letters/digits/underscore");
        if(value == null) throw new ArgumentNullException(nameof(value));
        if(tags.ContainsKey(name))
            throw new InvalidOperationException($"tag '{name}' is already registered");
        tags[name] = value;
    }
    public static void Unregister(string name) {
        if(!string.IsNullOrEmpty(name)) tags.Remove(name);
    }
    public static bool TryGet(string name, out Func<string> value) => tags.TryGetValue(name, out value);
    public static string Interpolate(string template, Func<string, string> extraResolver = null) {
        if(string.IsNullOrEmpty(template) || template.IndexOf('{') < 0) return template;
        StringBuilder sb = new(template.Length + 16);
        int i = 0, n = template.Length;
        while(i < n) {
            char c = template[i];
            if(c == '{') {
                if(i + 1 < n && template[i + 1] == '{') { sb.Append('{'); i += 2; continue; } 
                int close = template.IndexOf('}', i + 1);
                if(close < 0) { sb.Append(template, i, n - i); break; } 
                string name = template.Substring(i + 1, close - i - 1).Trim();
                if(Resolve(name, extraResolver, out string resolved)) sb.Append(resolved);
                else sb.Append(template, i, close - i + 1); 
                i = close + 1;
                continue;
            }
            if(c == '}' && i + 1 < n && template[i + 1] == '}') { sb.Append('}'); i += 2; continue; } 
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }
    private static bool Resolve(string name, Func<string, string> extraResolver, out string value) {
        value = null;
        if(name.Length == 0) return false;
        if(tags.TryGetValue(name, out Func<string> tag)) {
            try {
                value = tag() ?? "";
            } catch(Exception e) {
                MainCore.Log.Err($"[Addons] tag '{name}' threw: {e.Message}");
                value = "";
            }
            return true;
        }
        if(extraResolver != null) {
            string extra = extraResolver(name);
            if(extra != null) { value = extra; return true; }
        }
        return false;
    }
    internal static void Clear() => tags.Clear();
}
