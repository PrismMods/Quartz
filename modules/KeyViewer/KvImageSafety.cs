using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;

namespace Quartz.Features.KeyViewer;

internal static class KvImageSafety {
    internal const int MaxEncodedBytes = CssAssetDownloader.MaxAssetBytes;
    internal const int MaxDecodedEdge = 8192;
    internal const long MaxDecodedPixels = 64L * 1024L * 1024L;
    internal const int MaxBase64Characters = ((MaxEncodedBytes + 2) / 3) * 4;

    internal static bool TryIdentify(byte[] bytes, out IImageInfo info) =>
        TryIdentify(bytes, out info, out _);

    internal static bool TryIdentify(byte[] bytes, out IImageInfo info, out IImageFormat format) {
        info = null;
        format = null;
        if(bytes == null || bytes.Length == 0 || bytes.Length > MaxEncodedBytes) return false;
        try {
            info = Image.Identify(bytes, out format);
            return info != null && IsAllowedDimensions(info.Width, info.Height);
        } catch(Exception e) {
            Quartz.Core.Diag.Ignore(e);
            info = null;
            format = null;
            return false;
        }
    }

    internal static Image LoadFirstFrame(byte[] bytes, IImageFormat format) {
        if(format is GifFormat) {
            return Image.Load(bytes, new GifDecoder {
                IgnoreMetadata = true,
                MaxFrames = 1,
                DecodingMode = FrameDecodingMode.First,
            });
        }
        if(format is TiffFormat) {
            return Image.Load(bytes, new TiffDecoder {
                IgnoreMetadata = true,
                DecodingMode = FrameDecodingMode.First,
            });
        }
        return Image.Load(bytes);
    }

    internal static bool IsAllowedDimensions(int width, int height) =>
        width > 0 && height > 0
        && width <= MaxDecodedEdge && height <= MaxDecodedEdge
        && (long)width * height <= MaxDecodedPixels;

    internal static bool CanDecodeBase64(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxBase64Characters;
}

internal readonly struct KvImageCacheKey : IEquatable<KvImageCacheKey> {
    private readonly string source;
    private readonly object documentScope;
    private readonly object contentScope;

    private KvImageCacheKey(string source, object documentScope, object contentScope) {
        this.source = source ?? "";
        this.documentScope = documentScope;
        this.contentScope = contentScope;
    }

    internal static KvImageCacheKey External(string source) => new(source, null, null);
    internal static KvImageCacheKey Embedded(string source, object documentScope, object contentScope) =>
        new(source, documentScope, contentScope);

    public bool Equals(KvImageCacheKey other) =>
        string.Equals(source, other.source, StringComparison.Ordinal)
        && ReferenceEquals(documentScope, other.documentScope)
        && ReferenceEquals(contentScope, other.contentScope);

    public override bool Equals(object obj) => obj is KvImageCacheKey other && Equals(other);

    public override int GetHashCode() {
        unchecked {
            int hash = StringComparer.Ordinal.GetHashCode(source ?? "");
            hash = (hash * 397) ^ (documentScope == null ? 0 : RuntimeHelpers.GetHashCode(documentScope));
            return (hash * 397) ^ (contentScope == null ? 0 : RuntimeHelpers.GetHashCode(contentScope));
        }
    }
}
