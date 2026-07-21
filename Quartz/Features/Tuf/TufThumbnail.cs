#nullable enable
namespace Quartz.Features.Tuf;
public static class TufThumbnail {
    public const string MediumRes = "mqdefault";
    public const string Host = "i.ytimg.com";
    public const string BilibiliApiHost = "api.bilibili.com";
    public const string BilibiliImageHostSuffix = ".hdslb.com";
    public const string BilibiliResize = "@320w_180h_1c.jpg";
    public enum TufVideoKind { None, YouTube, Bilibili }
    public readonly struct TufVideoRef {
        public readonly TufVideoKind Kind;
        public readonly string Id;
        public TufVideoRef(TufVideoKind kind, string id) { Kind = kind; Id = id; }
        public static readonly TufVideoRef None = new(TufVideoKind.None, "");
        public bool HasThumbnail => Kind != TufVideoKind.None;
    }
    public static TufVideoRef Resolve(string? videoLink) {
        string? youtube = ExtractYouTubeId(videoLink);
        if(youtube != null) return new TufVideoRef(TufVideoKind.YouTube, youtube);
        string? bilibili = ExtractBilibiliId(videoLink);
        if(bilibili != null) return new TufVideoRef(TufVideoKind.Bilibili, bilibili);
        return TufVideoRef.None;
    }
    public static string? ThumbnailUrl(string? videoLink, string quality = MediumRes) {
        string? id = ExtractYouTubeId(videoLink);
        return id == null ? null : ThumbnailUrlForId(id, quality);
    }
    public static string ThumbnailUrlForId(string id, string quality) =>
        $"https://{Host}/vi/{id}/{quality}.jpg";
    public static string? ExtractYouTubeId(string? videoLink) {
        if(string.IsNullOrWhiteSpace(videoLink)) return null;
        foreach(string token in videoLink!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) {
            string? id = ExtractOne(token);
            if(id != null) return id;
        }
        return null;
    }
    private static string? ExtractOne(string token) {
        if(!Uri.TryCreate(token, UriKind.Absolute, out Uri? uri)) return null;
        if(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        string host = uri.Host.ToLowerInvariant();
        if(host.StartsWith("www.", StringComparison.Ordinal)) host = host.Substring(4);
        if(host.StartsWith("m.", StringComparison.Ordinal)) host = host.Substring(2);
        string? candidate = host switch {
            "youtu.be" => uri.AbsolutePath.Trim('/'),
            "youtube.com" or "youtube-nocookie.com" => FromYouTubeComUri(uri),
            _ => null
        };
        return IsValidId(candidate) ? candidate : null;
    }
    private static string? FromYouTubeComUri(Uri uri) {
        string path = uri.AbsolutePath;
        if(path is "/watch" or "/watch/") {
            foreach(string pair in uri.Query.TrimStart('?').Split('&')) {
                int eq = pair.IndexOf('=');
                if(eq > 0 && pair.Substring(0, eq) == "v") return Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            return null;
        }
        foreach(string prefix in new[] { "/embed/", "/shorts/", "/v/", "/live/" })
            if(path.StartsWith(prefix, StringComparison.Ordinal)) {
                string rest = path.Substring(prefix.Length);
                int slash = rest.IndexOf('/');
                return slash < 0 ? rest : rest.Substring(0, slash);
            }
        return null;
    }
    public static string? ExtractBilibiliId(string? videoLink) {
        if(string.IsNullOrWhiteSpace(videoLink)) return null;
        foreach(string token in videoLink!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) {
            string? bv = BilibiliOne(token);
            if(bv != null) return bv;
        }
        return null;
    }
    private static string? BilibiliOne(string token) {
        if(!Uri.TryCreate(token, UriKind.Absolute, out Uri? uri)) return null;
        if(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        string host = uri.Host.ToLowerInvariant();
        if(host.StartsWith("www.", StringComparison.Ordinal)) host = host.Substring(4);
        if(host.StartsWith("m.", StringComparison.Ordinal)) host = host.Substring(2);
        if(host != "bilibili.com") return null;
        foreach(string segment in uri.AbsolutePath.Split('/'))
            if(IsValidBv(segment)) return segment;
        return null;
    }
    private static bool IsValidBv(string? id) {
        if(string.IsNullOrEmpty(id) || id!.Length is < 10 or > 14) return false;
        if(id[0] != 'B' || id[1] != 'V') return false;
        for(int i = 2; i < id.Length; i++) {
            char c = id[i];
            if(!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))) return false;
        }
        return true;
    }
    private static bool IsValidId(string? id) {
        if(string.IsNullOrEmpty(id) || id!.Length is < 8 or > 20) return false;
        foreach(char c in id)
            if(!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
        return true;
    }
}
