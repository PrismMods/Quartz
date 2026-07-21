#nullable enable
namespace Quartz.Features.Tuf;
public static class TufDiskSpace {
    public const long Headroom = 64L * 1024 * 1024;
    public static long? AvailableBytes(string? path) {
        if(string.IsNullOrWhiteSpace(path)) return null;
        try {
            string full = Path.GetFullPath(path);
            DriveInfo? best = null;
            int bestLength = -1;
            foreach(DriveInfo drive in DriveInfo.GetDrives()) {
                string name;
                try { name = drive.Name; } catch { continue; }
                if(string.IsNullOrEmpty(name) || name.Length <= bestLength) continue;
                if(!full.StartsWith(name, PathComparison)) continue;
                best = drive;
                bestLength = name.Length;
            }
            if(best == null) {
                string? root = Path.GetPathRoot(full);
                if(string.IsNullOrEmpty(root)) return null;
                best = new DriveInfo(root);
            }
            long available = best.AvailableFreeSpace;
            return available < 0 ? null : available;
        } catch {
            return null;
        }
    }
    public static bool IsKnownInsufficient(string? path, long needBytes, out long freeBytes) {
        freeBytes = 0;
        if(needBytes < 0) return false;
        long? available = AvailableBytes(path);
        if(available == null) return false;
        freeBytes = available.Value;
        long required = long.MaxValue - Headroom < needBytes ? long.MaxValue : needBytes + Headroom;
        return freeBytes < required;
    }
    public static string Describe(long bytes) {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while(value >= 1024d && unit < units.Length - 1) {
            value /= 1024d;
            unit++;
        }
        return unit == 0
            ? bytes + " B"
            : value.ToString(value >= 100d ? "0" : "0.0",
                System.Globalization.CultureInfo.InvariantCulture) + " " + units[unit];
    }
    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
