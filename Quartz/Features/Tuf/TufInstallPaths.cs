#nullable enable

namespace Quartz.Features.Tuf;

// Path rules shared by every operation that writes outside Quartz's own folder:
// choosing an install root, deleting a level, and moving the library. Kept free of
// Unity and of the download service so the guards can be tested directly.
//
// The delete guard is the load-bearing one. A level folder now lives wherever the
// user pointed the library, so "delete level 123" resolves to an absolute path from
// a JSON file — a corrupt or hand-edited index must never be able to aim
// Directory.Delete at a folder we did not create.
public static class TufInstallPaths {
    private const string LinkedPrefix = "tuf-";

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // A level folder is named "<id>" (Quartz's own cache) or "tuf-<id>" (the layout
    // TUFHelperLite recognizes). Nothing else is ours.
    public static bool IsLevelFolderName(string? name, out int id) {
        id = 0;
        if(string.IsNullOrEmpty(name)) return false;
        string digits = name.StartsWith(LinkedPrefix, StringComparison.Ordinal)
            ? name[LinkedPrefix.Length..] : name;
        if(digits.Length == 0 || digits.Length > 9) return false;
        foreach(char c in digits) if(c is < '0' or > '9') return false;
        id = int.Parse(digits);
        return id > 0;
    }

    public static string LevelFolderName(int id, bool linked) => linked ? LinkedPrefix + id : id.ToString();

    // True only when `folder` is a direct child of one of `roots`, is named like a
    // level folder we create, and neither it nor the root is a symlink. Callers must
    // treat a false here as "refuse", never as "fall back to something else".
    public static bool IsOwnedLevelFolder(string? folder, IEnumerable<string?> roots) {
        if(string.IsNullOrWhiteSpace(folder)) return false;
        string full;
        string? parent;
        try {
            full = Path.GetFullPath(folder);
            if(!Directory.Exists(full)) return false;
            if((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) return false;
            if(!IsLevelFolderName(Path.GetFileName(full), out _)) return false;
            parent = Path.GetDirectoryName(full);
        } catch { return false; }
        if(string.IsNullOrEmpty(parent)) return false;
        foreach(string? root in roots) {
            if(string.IsNullOrWhiteSpace(root)) continue;
            try {
                string rootFull = Path.GetFullPath(root);
                if(!string.Equals(parent, rootFull, PathComparison)) continue;
                // A symlinked root would let a "direct child" resolve anywhere.
                if(Directory.Exists(rootFull)
                    && (File.GetAttributes(rootFull) & FileAttributes.ReparsePoint) != 0) return false;
                return true;
            } catch { }
        }
        return false;
    }

    // Vets a folder the user picked as the library root. Rejects the obviously
    // catastrophic choices: a volume root, a path that is not a real directory, and
    // anything that already holds unrelated files. That last rule is what keeps a
    // pick of "Documents" or "Desktop" from becoming a folder we later sweep — we
    // only ever want to own a directory that is empty or already ours.
    public static bool IsUsableLibraryRoot(string? folder, out string reason) {
        reason = "";
        if(string.IsNullOrWhiteSpace(folder)) {
            reason = "empty";
            return false;
        }
        string full;
        try { full = Path.GetFullPath(folder); }
        catch { reason = "invalid"; return false; }
        if(!Directory.Exists(full)) {
            reason = "missing";
            return false;
        }
        try {
            if((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) {
                reason = "symlink";
                return false;
            }
        } catch { reason = "invalid"; return false; }
        if(string.Equals(Path.GetFullPath(Path.GetPathRoot(full) ?? ""), full, PathComparison)) {
            reason = "volume-root";
            return false;
        }
        try {
            foreach(string entry in Directory.EnumerateFileSystemEntries(full)) {
                string name = Path.GetFileName(entry);
                if(IsIgnorableRootEntry(name)) continue;
                if(Directory.Exists(entry) && IsLevelFolderName(name, out _)) continue;
                reason = "not-empty";
                return false;
            }
        } catch { reason = "unreadable"; return false; }
        return true;
    }

    private static bool IsIgnorableRootEntry(string name) =>
        name is ".DS_Store" or "Thumbs.db" or "desktop.ini"
        || name.StartsWith(".layout-v", StringComparison.Ordinal)
        || name.StartsWith(".quartz-tmp", StringComparison.Ordinal);

    // Two roots that resolve to the same directory, or where one contains the other.
    // Moving a library into its own subfolder (or into its current home) is a no-op
    // at best and a recursive copy at worst.
    public static bool IsSameOrNested(string? a, string? b) {
        if(string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try {
            string left = Path.GetFullPath(a);
            string right = Path.GetFullPath(b);
            if(string.Equals(left, right, PathComparison)) return true;
            return Contains(left, right) || Contains(right, left);
        } catch { return false; }
    }

    private static bool Contains(string outer, string inner) {
        string prefix = outer.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? outer : outer + Path.DirectorySeparatorChar;
        return inner.StartsWith(prefix, PathComparison);
    }
}
