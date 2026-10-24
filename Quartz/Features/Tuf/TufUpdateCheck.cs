#nullable enable
using System.Globalization;
using Quartz.Core;
namespace Quartz.Features.Tuf;
public static class TufUpdateCheck {
    public static string FileIdOf(string? downloadUrl) {
        if(string.IsNullOrWhiteSpace(downloadUrl)) return "";
        if(!Uri.TryCreate(downloadUrl, UriKind.Absolute, out Uri? uri)) return "";
        string last = uri.Segments.Length == 0 ? "" : uri.Segments[^1].Trim('/');
        try { return Uri.UnescapeDataString(last); } catch(Exception e) { Diag.Ignore(e); return last; }
    }
    public static long ParseStamp(string? value) {
        if(string.IsNullOrWhiteSpace(value)) return 0;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? parsed.UtcDateTime.Ticks : 0;
    }
    public static string RemoteFileId(TufLevel? remote) {
        if(remote == null) return "";
        return remote.FileId.Length > 0 ? remote.FileId : FileIdOf(remote.DownloadUri?.ToString());
    }
    public static TufUpdateState Decide(TufInstallEntry? entry, TufLevel? remote) {
        if(entry == null || remote == null) return TufUpdateState.Unknown;
        string current = RemoteFileId(remote);
        if(entry.FileId.Length > 0 && current.Length > 0)
            return string.Equals(entry.FileId, current, StringComparison.Ordinal)
                ? TufUpdateState.UpToDate : TufUpdateState.Available;
        if(remote.UpdatedAtUtc > 0 && entry.InstalledAtUtc > 0)
            return remote.UpdatedAtUtc > entry.InstalledAtUtc ? TufUpdateState.Available : TufUpdateState.UpToDate;
        return TufUpdateState.Unknown;
    }
    public static long FolderSize(string? folder) {
        if(string.IsNullOrWhiteSpace(folder)) return 0;
        long total = 0;
        try {
            foreach(string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) {
                try {
                    FileInfo info = new(file);
                    if((info.Attributes & FileAttributes.ReparsePoint) == 0) total += info.Length;
                } catch(Exception e) { Diag.Ignore(e); }
            }
        } catch(Exception e) { Diag.Ignore(e); }
        return total;
    }
}
