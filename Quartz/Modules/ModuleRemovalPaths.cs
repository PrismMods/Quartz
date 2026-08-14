namespace Quartz.Modules;

internal static class ModuleRemovalPaths {
    internal const string BinaryExtension = ".qmod";
    internal const string ManifestExtension = ".qmod.json";

    internal static bool TryResolve(string root, string sourcePath, string id,
        out string binary, out string manifest) {
        binary = null;
        manifest = null;
        if(string.IsNullOrWhiteSpace(root)) return false;
        try {
            string rootFull = NormalizeRoot(root);
            string candidate = SafeBinary(rootFull, sourcePath);
            if(candidate == null && ModuleManifest.IsValidId(id))
                candidate = SafeBinary(rootFull, Path.Combine(rootFull, id + BinaryExtension));
            if(candidate == null) return false;
            string stem = Path.GetFileNameWithoutExtension(candidate);
            if(string.IsNullOrEmpty(stem)) return false;
            binary = candidate;
            manifest = Path.Combine(rootFull, stem + ManifestExtension);
            return true;
        } catch(Exception e) {
            Quartz.Core.Diag.Ignore(e);
            return false;
        }
    }

    private static string SafeBinary(string root, string candidate) {
        if(string.IsNullOrWhiteSpace(candidate)) return null;
        string full = Path.GetFullPath(candidate);
        string parent = Path.GetDirectoryName(full);
        if(!string.Equals(parent, root, PathComparison)) return null;
        if(!string.Equals(Path.GetExtension(full), BinaryExtension, PathComparison)) return null;
        return full;
    }

    private static string NormalizeRoot(string root) {
        string full = Path.GetFullPath(root);
        string volume = Path.GetPathRoot(full) ?? "";
        return full.Length > volume.Length
            ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : full;
    }

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
