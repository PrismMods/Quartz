#nullable disable
namespace Quartz.IO;
public static class ProfileNames {
    public static string Sanitize(string name) {
        if(string.IsNullOrWhiteSpace(name)) return null;
        char[] invalid = Path.GetInvalidFileNameChars();
        const string portableInvalid = "<>:\"/\\|?*";
        string clean = new([.. name.Trim().Where(c => !invalid.Contains(c) && !portableInvalid.Contains(c))]);
        clean = clean.Trim().Trim('.');
        if(clean.Length > 32) clean = clean[..32].Trim();
        return clean.Length == 0 ? null : clean;
    }
    public static string Unique(string name, Func<string, bool> exists) {
        name = Sanitize(name) ?? "Imported";
        if(exists == null || !exists(name)) return name;
        for(int i = 2; ; i++) {
            string suffix = $" ({i})";
            string stem = name;
            if(stem.Length + suffix.Length > 32) stem = stem[..(32 - suffix.Length)].Trim();
            string candidate = stem + suffix;
            if(!exists(candidate)) return candidate;
        }
    }
    public static string ImportedModName(string label) {
        string clean = Sanitize(label) ?? "Imported";
        return Sanitize("Imported - " + clean) ?? "Imported";
    }
    internal static bool TryResolveDirectory(string root, string name, out string path) {
        path = null;
        if(string.IsNullOrWhiteSpace(root)) return false;
        string clean = Sanitize(name);
        if(clean == null || !string.Equals(clean, name, StringComparison.Ordinal)) return false;
        try {
            string rootFull = Path.GetFullPath(root);
            string prefix = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || rootFull.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(rootFull, name));
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if(!candidate.StartsWith(prefix, comparison)) return false;
            path = candidate;
            return true;
        } catch(Exception e) {
            Quartz.Core.Diag.Ignore(e);
            return false;
        }
    }
}
