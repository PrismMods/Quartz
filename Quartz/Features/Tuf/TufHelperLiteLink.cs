#nullable enable
using Quartz.Core;
namespace Quartz.Features.Tuf;
public static class TufHelperLiteLink {
    private const string ModName = "TUFHelperLite";
    private static string? modDir;
    private static string? downloadsDir;
    private static bool resolved;
    public static bool Installed => ModDir() != null;
    public static void Reset() {
        resolved = false;
        modDir = null;
        downloadsDir = null;
    }
    public static string? DownloadsRoot() {
        if(downloadsDir != null) return downloadsDir;
        string? mod = ModDir();
        if(mod == null) return null;
        try {
            string downloads = Path.Combine(mod, "Downloads");
            Directory.CreateDirectory(downloads);
            downloadsDir = downloads;
            return downloads;
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not prepare the TUFHelperLite Downloads folder: " + e.Message);
            return null;
        }
    }
    public static string FolderName(int id) => "tuf-" + id;
    private static string? ModDir() {
        if(resolved) return modDir;
        resolved = true;
        modDir = FindModDir();
        MainCore.Log.Msg(modDir == null
            ? "[TUF] TUFHelperLite not installed; its link setting is hidden."
            : "[TUF] found TUFHelperLite at " + modDir);
        return modDir;
    }
    private static string? FindModDir() {
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
            DirectoryInfo? dir = Directory.GetParent(Path.GetFullPath(MainCore.Host.ModsPath));
            for(int depth = 0; dir != null && depth < 4; depth++, dir = dir.Parent) {
                if(Directory.Exists(Path.Combine(dir.FullName, "UMMMods"))
                    || Directory.Exists(Path.Combine(dir.FullName, "Mods"))) return dir.FullName;
            }
        } catch { }
        return null;
    }
}
