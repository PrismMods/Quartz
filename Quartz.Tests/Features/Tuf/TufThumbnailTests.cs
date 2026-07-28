using Quartz.Features.Tuf;
using static Asserts;
static class TufThumbnailTests {
    public static void TestYouTubeLinkShapes() {
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/watch?v=dQw4w9WgXcQ") == "dQw4w9WgXcQ", "watch?v=");
        Assert(TufThumbnail.ExtractYouTubeId("https://youtu.be/dQw4w9WgXcQ") == "dQw4w9WgXcQ", "short host");
        Assert(TufThumbnail.ExtractYouTubeId("https://m.youtube.com/watch?v=dQw4w9WgXcQ") == "dQw4w9WgXcQ", "mobile host");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/embed/dQw4w9WgXcQ") == "dQw4w9WgXcQ", "embed");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/shorts/dQw4w9WgXcQ") == "dQw4w9WgXcQ", "shorts");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/live/dQw4w9WgXcQ") == "dQw4w9WgXcQ", "live");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ") == "dQw4w9WgXcQ", "nocookie");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/watch?t=30&v=dQw4w9WgXcQ") == "dQw4w9WgXcQ", "v is not the first query key");
        Assert(TufThumbnail.ExtractYouTubeId("watch it here https://youtu.be/dQw4w9WgXcQ thanks") == "dQw4w9WgXcQ", "link embedded in prose");
    }
    public static void TestNonYouTubeAndMalformedLinks() {
        Assert(TufThumbnail.ExtractYouTubeId(null) == null, "null");
        Assert(TufThumbnail.ExtractYouTubeId("   ") == null, "blank");
        Assert(TufThumbnail.ExtractYouTubeId("dQw4w9WgXcQ") == null, "a bare id is not a link");
        Assert(TufThumbnail.ExtractYouTubeId("ftp://youtu.be/dQw4w9WgXcQ") == null, "only http(s)");
        Assert(TufThumbnail.ExtractYouTubeId("https://notyoutube.com/watch?v=dQw4w9WgXcQ") == null, "wrong host");
        Assert(TufThumbnail.ExtractYouTubeId("https://youtube.com.evil.test/watch?v=dQw4w9WgXcQ") == null, "host suffix is not a match");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/watch?v=short") == null, "id too short");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/watch?v=has spaces here") == null, "id charset");
        Assert(TufThumbnail.ExtractYouTubeId("https://www.youtube.com/feed/subscriptions") == null, "not a video path");
    }
    public static void TestBilibiliLinkShapes() {
        Assert(TufThumbnail.ExtractBilibiliId("https://www.bilibili.com/video/BV1GJ411x7h7") == "BV1GJ411x7h7", "standard BV url");
        Assert(TufThumbnail.ExtractBilibiliId("https://m.bilibili.com/video/BV1GJ411x7h7/") == "BV1GJ411x7h7", "mobile + trailing slash");
        Assert(TufThumbnail.ExtractBilibiliId("https://www.bilibili.com/video/av12345") == null, "av ids carry no BV thumbnail");
        Assert(TufThumbnail.ExtractBilibiliId("https://www.bilibili.com/video/bv1GJ411x7h7") == null, "BV prefix is case sensitive");
        Assert(TufThumbnail.ExtractBilibiliId("https://notbilibili.com/video/BV1GJ411x7h7") == null, "wrong host");
        Assert(TufThumbnail.ExtractBilibiliId(null) == null, "null");
    }
    public static void TestResolvePrefersYouTubeAndReportsKind() {
        TufThumbnail.TufVideoRef youtube = TufThumbnail.Resolve("https://youtu.be/dQw4w9WgXcQ");
        Assert(youtube.Kind == TufThumbnail.TufVideoKind.YouTube, "youtube kind");
        Assert(youtube.HasThumbnail, "youtube has a thumbnail");
        TufThumbnail.TufVideoRef bilibili = TufThumbnail.Resolve("https://www.bilibili.com/video/BV1GJ411x7h7");
        Assert(bilibili.Kind == TufThumbnail.TufVideoKind.Bilibili, "bilibili kind");
        Assert(bilibili.Id == "BV1GJ411x7h7", "bilibili id");
        TufThumbnail.TufVideoRef none = TufThumbnail.Resolve("https://example.test/nope");
        Assert(none.Kind == TufThumbnail.TufVideoKind.None, "unknown host resolves to none");
        Assert(!none.HasThumbnail, "none has no thumbnail");
        TufThumbnail.TufVideoRef both = TufThumbnail.Resolve(
            "https://www.bilibili.com/video/BV1GJ411x7h7 https://youtu.be/dQw4w9WgXcQ");
        Assert(both.Kind == TufThumbnail.TufVideoKind.YouTube, "youtube wins when both are present");
    }
    public static void TestThumbnailUrlStaysOnTheApprovedHost() {
        string url = TufThumbnail.ThumbnailUrl("https://youtu.be/dQw4w9WgXcQ");
        Assert(url == $"https://{TufThumbnail.Host}/vi/dQw4w9WgXcQ/{TufThumbnail.MediumRes}.jpg", "medium-res url shape");
        Assert(TufThumbnail.ThumbnailUrl("https://example.test/nope") == null, "no url without an id");
        Assert(new Uri(url).Host == TufThumbnail.Host, "url host is the constant, never the input's");
        Assert(new Uri(url).Scheme == Uri.UriSchemeHttps, "always https");
        Assert(TufThumbnail.ThumbnailUrlForId("abc", "maxresdefault").EndsWith("/maxresdefault.jpg", StringComparison.Ordinal),
            "quality is honoured");
    }
}
