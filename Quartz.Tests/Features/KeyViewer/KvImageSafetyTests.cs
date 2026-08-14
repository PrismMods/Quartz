using Quartz.Features.KeyViewer;
using SixLabors.ImageSharp;
using static Asserts;

static class KvImageSafetyTests {
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public static void TestMetadataLimits() {
        byte[] safe = Convert.FromBase64String(OnePixelPng);
        Assert(KvImageSafety.TryIdentify(safe, out SixLabors.ImageSharp.IImageInfo info)
            && info.Width == 1 && info.Height == 1,
            "image metadata preflight accepts a small valid image");

        byte[] oversized = (byte[])safe.Clone();
        WriteBigEndian(oversized, 16, KvImageSafety.MaxDecodedEdge + 1);
        WriteBigEndian(oversized, 29, unchecked((int)Crc32(oversized, 12, 17)));
        Assert(!KvImageSafety.TryIdentify(oversized, out _),
            "image metadata preflight rejects an oversized image before pixel decode");
        Assert(KvImageSafety.IsAllowedDimensions(8192, 8192)
            && !KvImageSafety.IsAllowedDimensions(8193, 1),
            "image metadata preflight enforces its exact decoded-size boundary");

        using var animated = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(2, 2);
        animated.Frames.AddFrame(animated.Frames.RootFrame);
        using MemoryStream gif = new();
        animated.SaveAsGif(gif);
        byte[] animatedBytes = gif.ToArray();
        Assert(KvImageSafety.TryIdentify(animatedBytes, out _, out SixLabors.ImageSharp.Formats.IImageFormat format),
            "animated GIF metadata passes the bounded canvas preflight");
        using SixLabors.ImageSharp.Image firstFrame = KvImageSafety.LoadFirstFrame(animatedBytes, format);
        Assert(firstFrame.Frames.Count == 1,
            "the ImageSharp fallback decodes only one frame from an animated image");
    }

    public static void TestCacheScopes() {
        const string source = "dmnote-local-image://shared";
        object documentA = new();
        object documentB = new();
        object contentA = new();
        object contentB = new();
        KvImageCacheKey first = KvImageCacheKey.Embedded(source, documentA, contentA);
        Assert(first.Equals(KvImageCacheKey.Embedded(source, documentA, contentA)),
            "the same embedded image content reuses its texture cache entry");
        Assert(!first.Equals(KvImageCacheKey.Embedded(source, documentB, contentA))
            && !first.Equals(KvImageCacheKey.Embedded(source, documentA, contentB)),
            "embedded image cache entries are scoped by both document and content");
    }

    private static void WriteBigEndian(byte[] bytes, int offset, int value) {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes, int offset, int count) {
        uint crc = uint.MaxValue;
        for(int i = offset; i < offset + count; i++) {
            crc ^= bytes[i];
            for(int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        return ~crc;
    }
}
