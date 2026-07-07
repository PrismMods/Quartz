using System.Text;
using Quartz.Core;

namespace Quartz.Addons;

// Named value-producers ("tags") addons can register, à la Overlayer's
// registerTag. A tag is a name → string-every-call delegate. Quartz has no
// text/tag engine of its own, so tags are consumed where free-form text is
// shown — currently the Panels custom-"text" stat, which interpolates
// {TagName} placeholders (see PanelsOverlay). Built-in panel stat ids resolve
// as tags too (e.g. {fps}, {accuracy}) via the extra resolver.
public static class AddonTags {
    private static readonly Dictionary<string, Func<string>> tags =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Names => tags.Keys;

    // Valid tag name: letters, digits, underscore (so {Name} parses cleanly).
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

    // Replaces every {Name} in the template with its tag value. Resolution
    // order: registered addon tags, then extraResolver (returns null if it
    // doesn't handle the name), else the literal {Name} is left untouched so
    // typos stay visible. `{{`/`}}` are literal braces. Single pass — a tag
    // value that itself contains braces is NOT re-interpolated. Cheap fast
    // path when there's no brace at all (the common case).
    public static string Interpolate(string template, Func<string, string> extraResolver = null) {
        if(string.IsNullOrEmpty(template) || template.IndexOf('{') < 0) return template;

        StringBuilder sb = new(template.Length + 16);
        int i = 0, n = template.Length;
        while(i < n) {
            char c = template[i];

            if(c == '{') {
                if(i + 1 < n && template[i + 1] == '{') { sb.Append('{'); i += 2; continue; } // escaped

                int close = template.IndexOf('}', i + 1);
                if(close < 0) { sb.Append(template, i, n - i); break; } // unterminated: literal tail

                string name = template.Substring(i + 1, close - i - 1).Trim();
                if(Resolve(name, extraResolver, out string resolved)) sb.Append(resolved);
                else sb.Append(template, i, close - i + 1); // unknown: keep {name} verbatim
                i = close + 1;
                continue;
            }

            if(c == '}' && i + 1 < n && template[i + 1] == '}') { sb.Append('}'); i += 2; continue; } // escaped

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

    // UMM in-process reload safety, mirroring AddonEvents.Clear.
    internal static void Clear() => tags.Clear();
}
