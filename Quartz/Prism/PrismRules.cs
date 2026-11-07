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
    /// <summary>
    /// The runtime store folder holding Quartz.dll: where it loaded from, or for a byte-loaded Quartz.dll (empty location)
    /// the version's folder under the runtime root, which UMM keeps in the mod folder and MelonLoader in Quartz's data folder.
    /// </summary>
    public static string StoreDir(string location, bool umm, string modsPath, string quartzFilePath, string version) =>
        location.Length > 0 ? Path.GetDirectoryName(location) : Path.Combine(umm ? modsPath : quartzFilePath, "Runtime", "versions", version);
    /// <summary>
    /// Quartz answers the Prism window chords (Alt+D, Alt+P, Ctrl+Z) when it holds the debug claim, or when no PrismLib is
    /// loaded at all. A PrismLib Quartz could not use (another mod loaded an older one) has its own holder, who answers.
    /// </summary>
    public static bool OwnsChords(bool available, bool debugClaimed, bool foreignLib) => debugClaimed || !available && !foreignLib;
    /// <summary>Keybind.KeyModifier (None, Ctrl, Alt, Shift, Cmd) as PrismLib's KeyMods bits (Shift 1, Ctrl 2, Alt 4, Cmd 8).</summary>
    public static int ToPrismMods(int keybindModifier) => keybindModifier switch {
        1 => 2,
        2 => 4,
        3 => 1,
        4 => 8,
        _ => 0,
    };
    /// <summary>
    /// A menu hotkey Quartz won't take: no key, a modifier key or a mouse button (Unity's KeyCode 303-313 and 323-329),
    /// or one of Prism's own Alt chords (Alt+D debug, Alt+P settings, Alt+T Topaz, Alt+C CamDisplay).
    /// </summary>
    public static bool HotkeyRefused(int modifier, int keyCode) =>
        keyCode == 0 || keyCode >= 303 && keyCode <= 313 || keyCode >= 323 && keyCode <= 329
        || modifier == 2 && (keyCode == 100 || keyCode == 112 || keyCode == 116 || keyCode == 99);
    /// <summary>
    /// The English the bridge hands PrismLib for its Prism · Settings rows, and the Lang key that translates it. PrismLib.UI
    /// translates at draw time through Words, so the row text stays English in the registry.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> EnglishToKey = new Dictionary<string, string> {
        ["Menu"] = "PRISM_GROUP_MENU",
        ["Experimental"] = "PRISM_GROUP_EXPERIMENTAL",
        ["Menu hotkey"] = "PRISM_MENU_HOTKEY",
        ["The key that opens Quartz's menu, with the modifier below"] = "PRISM_MENU_HOTKEY_TIP",
        ["Menu hotkey modifier"] = "PRISM_MENU_MODIFIER",
        ["None"] = "PRISM_MODIFIER_NONE",
        ["Experimental: new menu"] = "PRISM_NEW_MENU",
        ["The menu hotkey opens the new menu. It is still being built; the classic menu stays available"] = "PRISM_NEW_MENU_TIP",
    };
    /// <summary>The English names of Quartz's debug log and fields in PrismLib's registry.</summary>
    public const string DebugLog = "log", DebugState = "State";
    /// <summary>
    /// Every holder of the debug window draws a tab as "&lt;owner&gt; · &lt;name&gt;" and PrismLib.UI translates that whole text
    /// through Words at draw time, so Quartz registers English names and answers the "Quartz · " form. A name translated at
    /// registration would stay in the language of that moment: the table loads after Register.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TabToKey = new Dictionary<string, string> {
        [DebugLog] = "PRISM_DEBUG_LOG",
        [DebugState] = "PRISM_DEBUG_STATE",
    };
    public const string TabPrefix = "Quartz · ";
    /// <summary>The translation of one of those English labels or tabs, or null when the text is not Quartz's.</summary>
    public static string Translate(string english, Func<string, string, string> get) {
        if(english == null) return null;
        if(EnglishToKey.TryGetValue(english, out string key)) return get(key, english);
        if(!english.StartsWith(TabPrefix, StringComparison.Ordinal)) return null;
        string name = english.Substring(TabPrefix.Length);
        return TabToKey.TryGetValue(name, out key) ? TabPrefix + get(key, name) : null;
    }
    /// <summary>
    /// PrismLib gives a mod no "the layout editor is open" signal, but the editor reads every target's box each
    /// frame it is open: a frame with a read opens the session, and it stays open Grace frames past the last one.
    /// </summary>
    public sealed class LayoutSession {
        public const int Grace = 2;
        private int last = int.MinValue;
        public void Touch(int frame) => last = frame;
        public bool Open(int frame) => last != int.MinValue && frame - last <= Grace;
    }
    private static void AddStore(List<string> found, string owner) {
        foreach(string version in Dirs(Path.Combine(owner, "Runtime", "versions"))) Add(found, Path.Combine(version, Ui));
    }
    private static string[] Dirs(string dir) => Directory.Exists(dir) ? Directory.GetDirectories(dir) : [];
    private static void Add(List<string> found, string file) {
        if(File.Exists(file)) found.Add(file);
    }
}
