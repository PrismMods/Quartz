namespace Quartz.Prism;
/// <summary>The bridge's decisions that need neither Unity nor PrismLib, so the test project can pin them.</summary>
internal static class PrismRules {
    private const string Ui = "PrismLib.UI.dll";
    /// <summary>The folder that holds the mod folders: UMM hands Quartz its own mod folder, MelonLoader the Mods folder.</summary>
    public static string ModsRoot(string hostModsPath, bool umm) {
        if(string.IsNullOrEmpty(hostModsPath)) return null;
        string path = hostModsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return umm ? Path.GetDirectoryName(path) : path;
    }
    /// <summary>
    /// Every PrismLib.UI.dll a loader could load under this game root: mod folders (never the shared
    /// PrismLib folder), runtime stores in both layouts, and UserLibs.
    /// </summary>
    public static IReadOnlyList<string> UiCopies(string gameRoot) {
        var found = new List<string>();
        foreach(string root in new[] { "Mods", "UMMMods" }) {
            foreach(string mod in Dirs(Path.Combine(gameRoot, root))) {
                if(!string.Equals(Path.GetFileName(mod), "PrismLib", StringComparison.OrdinalIgnoreCase)) Add(found, Path.Combine(mod, Ui));
                AddStore(found, mod);
            }
        }
        Add(found, Path.Combine(gameRoot, "UserLibs", Ui));
        foreach(string data in Dirs(Path.Combine(gameRoot, "UserData"))) AddStore(found, data);
        return found;
    }
    /// <summary>Keybind.KeyModifier (None, Ctrl, Alt, Shift, Cmd) as PrismLib's KeyMods bits (Shift 1, Ctrl 2, Alt 4, Cmd 8).</summary>
    public static int ToPrismMods(int keybindModifier) => keybindModifier switch {
        1 => 2,
        2 => 4,
        3 => 1,
        4 => 8,
        _ => 0,
    };
    private static void AddStore(List<string> found, string owner) {
        foreach(string version in Dirs(Path.Combine(owner, "Runtime", "versions"))) Add(found, Path.Combine(version, Ui));
    }
    private static string[] Dirs(string dir) => Directory.Exists(dir) ? Directory.GetDirectories(dir) : [];
    private static void Add(List<string> found, string file) {
        if(File.Exists(file)) found.Add(file);
    }
}
