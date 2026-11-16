namespace Quartz.Localization;
/// <summary>Lang keys made from a control's id or a display text: upper case, runs of anything else one underscore.</summary>
public static class LocaleKeys {
    public static string FromId(string id, string suffix = null) {
        if(string.IsNullOrWhiteSpace(id)) return null;
        string key = NormalizeLocaleKey(id);
        key = StripIndexedPrefix(key, "PANEL");
        key = StripIndexedPrefix(key, "PRACTICE");
        key = StripIndexedPrefix(key, "JRSEC");
        if(key.StartsWith("PANEL_PICK_")) {
            key = "PANEL_STAT_" + key["PANEL_PICK_".Length..];
        }
        if(!string.IsNullOrEmpty(suffix)) key += "_" + NormalizeLocaleKey(suffix);
        return key;
    }
    public static string FromText(string prefix, string text) {
        string key = NormalizeLocaleKey(text);
        return string.IsNullOrEmpty(prefix) ? key : NormalizeLocaleKey(prefix) + "_" + key;
    }
    private static string StripIndexedPrefix(string key, string prefix) {
        if(key == null || !key.StartsWith(prefix)) return key;
        int i = prefix.Length;
        while(i < key.Length && char.IsDigit(key[i])) i++;
        return i > prefix.Length && i < key.Length && key[i] == '_' ? prefix + key[i..] : key;
    }
    private static string NormalizeLocaleKey(string value) {
        if(string.IsNullOrWhiteSpace(value)) return null;
        List<char> chars = [];
        bool lastUnderscore = false;
        foreach(char raw in value.Trim().ToUpperInvariant()) {
            char c = char.IsLetterOrDigit(raw) ? raw : '_';
            if(c == '_') {
                if(lastUnderscore) continue;
                lastUnderscore = true;
            } else {
                lastUnderscore = false;
            }
            chars.Add(c);
        }
        while(chars.Count > 0 && chars[^1] == '_') chars.RemoveAt(chars.Count - 1);
        return chars.Count == 0 ? null : new string(chars.ToArray());
    }
}
