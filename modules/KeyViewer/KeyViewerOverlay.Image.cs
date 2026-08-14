using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static readonly Dictionary<KvImageCacheKey, Texture2D> cssImages = [];
    private static readonly HashSet<string> cssImagePending = new(StringComparer.Ordinal);
    private static readonly object cssImageLock = new();
    internal static Texture2D ResolveImage(string src, KvDocument document = null) {
        if(string.IsNullOrWhiteSpace(src)) return null;
        string key = src.Trim();
        try {
            document ??= KvStore.Current;
            if(document != null && document.TryEmbeddedImage(
                key, out string dataBase64, out _, out object contentScope
            )) {
                KvImageCacheKey embeddedKey = KvImageCacheKey.Embedded(key, document, contentScope);
                if(cssImages.TryGetValue(embeddedKey, out Texture2D embedded)) return embedded;
                return TryDecodeImageBase64(dataBase64, out byte[] bytes)
                    ? Cache(embeddedKey, LoadTex(bytes))
                    : null;
            }
            KvImageCacheKey cacheKey = KvImageCacheKey.External(key);
            if(cssImages.TryGetValue(cacheKey, out Texture2D cached)) return cached;
            if(key.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                int comma = key.IndexOf(',');
                int b64 = key.IndexOf("base64", StringComparison.OrdinalIgnoreCase);
                if(comma > 0 && b64 > 0 && b64 < comma
                    && TryDecodeImageBase64(key.Substring(comma + 1), out byte[] bytes))
                    return Cache(cacheKey, LoadTex(bytes));
                return null;
            }
            if(key.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                string path = ImageCachePath(key);
                if(File.Exists(path)) return Cache(cacheKey, LoadTex(ReadImageFile(path)));
                StartImageDownload(key, path);
                return null;
            }
            string file = key.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(key).LocalPath
                : key;
            if(IsLikelyLocalPath(file) && File.Exists(file))
                return Cache(cacheKey, LoadTex(ReadImageFile(file)));
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] CSS image load failed: " + ex.Message);
        }
        return null;
    }
    private static bool IsLikelyLocalPath(string v) =>
        v.Length > 1 && (v[0] == '/' || v.StartsWith("\\\\", StringComparison.Ordinal)
            || (char.IsLetter(v[0]) && v[1] == ':'));
    private static Texture2D Cache(KvImageCacheKey key, Texture2D tex) {
        if(tex != null) cssImages[key] = tex;
        return tex;
    }
    private static bool TryDecodeImageBase64(string value, out byte[] bytes) {
        bytes = null;
        if(!KvImageSafety.CanDecodeBase64(value)) return false;
        try {
            bytes = Convert.FromBase64String(value);
            return bytes.Length <= KvImageSafety.MaxEncodedBytes;
        } catch(FormatException e) {
            Diag.Ignore(e);
            bytes = null;
            return false;
        }
    }
    private static byte[] ReadImageFile(string path) {
        FileInfo info = new(path);
        if(!info.Exists || info.Length <= 0 || info.Length > KvImageSafety.MaxEncodedBytes) return null;
        return File.ReadAllBytes(path);
    }
    private static Texture2D LoadTex(byte[] bytes) {
        if(!KvImageSafety.TryIdentify(bytes, out _, out SixLabors.ImageSharp.Formats.IImageFormat format))
            return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        bool loaded = false;
        try {
            loaded = tex.LoadImage(bytes);
        } catch(Exception e) { Diag.Ignore(e); }
        if(loaded) return tex;
        UnityEngine.Object.Destroy(tex);
        return LoadTexWithImageSharp(bytes, format);
    }
    private static Texture2D LoadTexWithImageSharp(
        byte[] bytes, SixLabors.ImageSharp.Formats.IImageFormat format
    ) {
        try {
            using SixLabors.ImageSharp.Image image = KvImageSafety.LoadFirstFrame(bytes, format);
            using MemoryStream png = new();
            image.Save(png, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if(tex.LoadImage(png.ToArray())) return tex;
            UnityEngine.Object.Destroy(tex);
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] Extended image decode failed: " + ex.Message);
        }
        return null;
    }
    private static string ImageCachePath(string url) {
        string dir = Path.Combine(MainCore.Paths.RootPath, "CssImages");
        Directory.CreateDirectory(dir);
        string ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if(ext.Length is < 2 or > 5) ext = ".png";
        return Path.Combine(dir, Hash(url) + ext);
    }
    private static void StartImageDownload(string url, string path) {
        lock(cssImageLock) { if(!cssImagePending.Add(url)) return; }
        StartCssDownload(url, path, "CSS image download failed",
            "QuartzCssImage", cssImageLock, cssImagePending, url);
    }
    private static void DisposeCssImageCache() {
        foreach(Texture2D tex in cssImages.Values)
            if(tex != null) UnityEngine.Object.Destroy(tex);
        cssImages.Clear();
    }
    private static void BuildKeyImage(Box box, DmNoteSpec spec) {
        if(!spec.HasImage || box.Fill == null) return;
        spec.IdleTex = ResolveImage(spec.InactiveImage);
        spec.ActiveTex = ResolveImage(spec.ActiveImage);
        if(spec.IdleTex == null && spec.ActiveTex == null) return;
        Mask mask = box.Fill.GetComponent<Mask>() ?? box.Fill.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        GameObject obj = new("KeyImage");
        obj.transform.SetParent(box.Fill.transform, false);
        RawImage ri = obj.AddComponent<RawImage>();
        ri.raycastTarget = false;
        obj.transform.SetAsFirstSibling();
        box.KeyImage = ri;
    }
    private static void BuildGraphImage(RectTransform parent, DmNoteSpec spec) {
        if(!spec.HasImage) return;
        Texture2D tex = ResolveImage(spec.InactiveImage) ?? ResolveImage(spec.ActiveImage);
        if(tex == null) return;
        GameObject obj = new("GraphImage");
        obj.transform.SetParent(parent, false);
        RawImage ri = obj.AddComponent<RawImage>();
        ri.raycastTarget = false;
        obj.transform.SetAsFirstSibling();
        string fit = spec.ImageFitDefault.Length > 0 ? spec.ImageFitDefault : "cover";
        ApplyImageFit(ri, tex, fit, spec.W, spec.H);
        ri.color = Color.white;
    }
    private static void ApplyImageState(Box box, DmNoteSpec spec, bool pressed) {
        if(box.KeyImage == null) return;
        bool usingActive = pressed && spec.ActiveTex != null;
        Texture2D tex = usingActive ? spec.ActiveTex : spec.IdleTex;
        if(tex == null) {
            box.KeyImage.enabled = false;
            return;
        }
        box.KeyImage.enabled = true;
        string fit = usingActive
            ? Pick(spec.ActiveImageFit, spec.ImageFitDefault)
            : Pick(spec.IdleImageFit, spec.ImageFitDefault);
        if(!ReferenceEquals(tex, box.LastImageTex) || !string.Equals(fit, box.LastImageFit, StringComparison.Ordinal)) {
            ApplyImageFit(box.KeyImage, tex, fit, spec.W, spec.H);
            box.LastImageTex = tex;
            box.LastImageFit = fit;
        }
        bool dimmed = pressed && spec.ActiveTex == null && spec.IdleTex != null;
        box.KeyImage.color = dimmed ? new Color(0.62f, 0.62f, 0.62f, 1f) : Color.white;
    }
    private static string Pick(string specific, string fallback) =>
        specific.Length > 0 ? specific : fallback.Length > 0 ? fallback : "cover";
    private static void ApplyImageFit(RawImage ri, Texture2D tex, string fit, float rw, float rh) {
        ri.texture = tex;
        RectTransform rt = ri.rectTransform;
        float tw = Mathf.Max(tex.width, 1), th = Mathf.Max(tex.height, 1);
        if(string.Equals(fit, "fill", StringComparison.OrdinalIgnoreCase)) {
            Stretch(rt);
            ri.uvRect = new Rect(0f, 0f, 1f, 1f);
        } else if(string.Equals(fit, "contain", StringComparison.OrdinalIgnoreCase)) {
            float scale = Mathf.Min(rw / tw, rh / th);
            Center(rt, tw * scale, th * scale);
            ri.uvRect = new Rect(0f, 0f, 1f, 1f);
        } else if(string.Equals(fit, "none", StringComparison.OrdinalIgnoreCase)) {
            Center(rt, tw, th);
            ri.uvRect = new Rect(0f, 0f, 1f, 1f);
        } else {
            Stretch(rt);
            float ra = rw / rh, ta = tw / th;
            ri.uvRect = ta > ra
                ? new Rect((1f - ra / ta) * 0.5f, 0f, ra / ta, 1f)
                : new Rect(0f, (1f - ta / ra) * 0.5f, 1f, ta / ra);
        }
    }
    private static void Stretch(RectTransform rt) {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
    }
    private static void Center(RectTransform rt, float w, float h) {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
        rt.localRotation = Quaternion.identity;
    }
}
