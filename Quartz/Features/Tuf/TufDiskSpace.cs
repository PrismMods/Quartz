#nullable enable

namespace Quartz.Features.Tuf;

// Free space on the volume holding a given path, and the size wording the TUF
// errors use.
//
// This replaces the fixed byte ceilings downloads and extraction used to carry. A
// 512 MB download cap rejected real levels (5350 is 631 MB of PNG backgrounds) while
// buying almost nothing: the archives come only from the allowlisted TUF CDN, and the
// guard that actually stops a lying zip is the per-entry declared-size check in
// TufArchive.CopyBounded. What genuinely bounds an install is the drive, so that is
// what gets checked.
//
// Every probe fails open — an unknown amount of free space must never block a
// download, only a known-insufficient one.
public static class TufDiskSpace {
    // Never fill a volume to the last byte; leave the OS somewhere to breathe.
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
            // Longest matching mount wins: on Unix every path matches "/", but a
            // library on an external volume lives under "/Volumes/<name>" and it is
            // that volume's free space we care about.
            if(best == null) {
                string? root = Path.GetPathRoot(full);
                if(string.IsNullOrEmpty(root)) return null;
                best = new DriveInfo(root);
            }
            long available = best.AvailableFreeSpace;
            return available < 0 ? null : available;
        } catch {
            // No DriveInfo for this mount, or the platform would not answer. Unknown
            // is not "full" — let the write proceed and fail naturally if it must.
            return null;
        }
    }

    // True only when we know the space and know it is not enough.
    public static bool IsKnownInsufficient(string? path, long needBytes, out long freeBytes) {
        freeBytes = 0;
        if(needBytes < 0) return false;
        long? available = AvailableBytes(path);
        if(available == null) return false;
        freeBytes = available.Value;
        // Saturating add. A doctored zip directory can claim long.MaxValue, and
        // needBytes + Headroom would then wrap to a negative that every volume
        // compares as "fits" — turning the guard into a rubber stamp for exactly the
        // archive it exists to stop.
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
