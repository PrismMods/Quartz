#nullable enable
using Quartz.Core;
namespace Quartz.Features.Tuf;
public static partial class TufRollback {
    public const string FolderName = "rollback";
    public const int KeepPerLevel = 3;
    public static string Root(string levelsRoot) => Path.Combine(Path.GetFullPath(levelsRoot), FolderName);
    public static string StampFolder(string levelsRoot, long stamp) => Path.Combine(Root(levelsRoot), stamp.ToString());
    public static string SnapshotFolder(string levelsRoot, long stamp, int id) =>
        Path.Combine(StampFolder(levelsRoot, stamp), id.ToString());
    public static string MetaFile(string levelsRoot, long stamp, int id) =>
        Path.Combine(StampFolder(levelsRoot, stamp), id + ".json");
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static string DescribeStamp(long stamp) =>
        DateTimeOffset.FromUnixTimeSeconds(stamp).ToLocalTime().DateTime.ToString("yyyy-MM-dd HH:mm");
    public static bool IsStampName(string? name, out long stamp) {
        stamp = 0;
        if(string.IsNullOrEmpty(name) || name.Length > 12) return false;
        foreach(char c in name) if(c is < '0' or > '9') return false;
        return long.TryParse(name, out stamp) && stamp > 0;
    }
    public static bool IsOwnedSnapshotFolder(string? folder, string? levelsRoot, int id) {
        if(string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(levelsRoot) || id <= 0) return false;
        try {
            string full = Path.GetFullPath(folder);
            if(!Directory.Exists(full)) return false;
            if((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) return false;
            if(!TufInstallPaths.IsLevelFolderName(Path.GetFileName(full), out int folderId) || folderId != id) return false;
            string? stampDir = Path.GetDirectoryName(full);
            if(stampDir == null || !IsStampName(Path.GetFileName(stampDir), out _)) return false;
            string? rollbackDir = Path.GetDirectoryName(stampDir);
            return rollbackDir != null
                && string.Equals(Path.GetFullPath(rollbackDir), Root(levelsRoot), PathComparison);
        } catch(Exception e) { Diag.Ignore(e); return false; }
    }
    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
