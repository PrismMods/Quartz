#nullable enable
using Quartz.Core;

namespace Quartz.Features.Tuf;

// Locates a TUFHelperLite install so level downloads can be shared with that
// mod. The game root is found by walking up from the host's mods path; inside
// it UMMMods is preferred over Mods (setups running MelonLoader and UMM side
// by side keep UMM mods in UMMMods), matching where TUFHelperLite loads from.
public static class TufHelperLiteLink {
    private const string ModName = "TUFHelperLite";

    public static bool Installed => TufHelperLiteDir() != null;

    // <modsDir>/TUFHelperLite/Downloads, created on demand; null when the mod
    // is not installed so callers fall back to Quartz's own cache.
    public static string? DownloadsRoot() {
        string? mod = TufHelperLiteDir();
        if(mod == null) return null;
        try {
            string downloads = Path.Combine(mod, "Downloads");
            Directory.CreateDirectory(downloads);
            return downloads;
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not prepare the TUFHelperLite Downloads folder: " + e.Message);
            return null;
        }
    }

    // TUFHelperLite's on-disk naming for a downloaded level folder.
    public static string FolderName(int id) => "tuf-" + id;

    private static string? TufHelperLiteDir() {
        string? root = GameRoot();
        if(root == null) return null;
        foreach(string modsName in new[] { "UMMMods", "Mods" }) {
            string mod = Path.Combine(root, modsName, ModName);
            if(Directory.Exists(mod)) return mod;
        }
        return null;
    }

    private static string? GameRoot() {
        try {
            // ModsPath is <game>/Mods under MelonLoader and the mod's own folder
            // (<game>/Mods/Quartz) under UMM; the first ancestor holding a mods
            // directory is the game root either way.
            DirectoryInfo? dir = Directory.GetParent(Path.GetFullPath(MainCore.Host.ModsPath));
            for(int depth = 0; dir != null && depth < 4; depth++, dir = dir.Parent) {
                if(Directory.Exists(Path.Combine(dir.FullName, "UMMMods"))
                    || Directory.Exists(Path.Combine(dir.FullName, "Mods"))) return dir.FullName;
            }
        } catch { }
        return null;
    }
}
