using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static readonly List<Box> cssFx = [];
    private static string cssCacheKey;
    private static KeyViewerStylesheet cssCache;
    private static Sprite glowSprite;
    private static RectTransform cssGlowLayer;
    private static readonly Dictionary<string, Texture2D> gradTex = [];
    private static readonly Dictionary<string, TMP_FontAsset> cssFonts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> cssFontPending = new(StringComparer.OrdinalIgnoreCase);
    private static volatile bool cssDownloadArrived;
    private static KeyViewerStylesheet GetStylesheet(string text) {
        if(cssCache == null || !string.Equals(cssCacheKey, text, StringComparison.Ordinal)) {
            cssCache = KeyViewerStylesheet.Parse(text);
            cssCacheKey = text;
        }
        return cssCache;
    }
    private static void ApplyCssToSpecs(List<DmNoteSpec> specs) {
        if(specs.Count == 0 || Conf == null || !Conf.DmCssEnabled
            || string.IsNullOrWhiteSpace(Conf.DmCssText)) {
            return;
        }
        try {
            KeyViewerStylesheet sheet = GetStylesheet(Conf.DmCssText);
            if(sheet.IsEmpty) return;
            EnsureFontFaces(sheet);
            foreach(DmNoteSpec spec in specs) {
                if(spec.IsGraph) {
                    if(!spec.GraphInlineStyles) ApplyGraphCss(spec, sheet.ResolveGraph(spec.ClassName));
                    continue;
                }
                InlineStyleSnapshot inline = new(spec);
                ApplyKeyStyle(spec, sheet.ResolveKey(spec.ClassName));
                ApplyCounterStyle(spec, sheet.ResolveCounter(spec.ClassName));
                if(spec.KeyInlineStyles) inline.Restore(spec);
            }
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] CSS apply failed: " + ex.Message);
        }
    }
    private static Color SampleGradient(Color[] stops, float p) {
        int n = stops.Length;
        if(n == 1) return stops[0];
        p -= Mathf.Floor(p);
        float scaled = p * n;
        int idx = (int)scaled % n;
        int next = (idx + 1) % n;
        return Color.Lerp(stops[idx], stops[next], scaled - Mathf.Floor(scaled));
    }
    private static Texture2D GradientTexture(Color[] stops, float blur) {
        string key = GradKey(stops, blur);
        if(gradTex.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
        const int w = 256, h = 8;
        Color[] row = new Color[w];
        int n = stops.Length;
        for(int x = 0; x < w; x++) {
            float p = (float)x / w * n;
            int idx = (int)p % n;
            int next = (idx + 1) % n;
            row[x] = Color.Lerp(stops[idx], stops[next], p - Mathf.Floor(p));
        }
        if(blur > 0.5f) row = BoxBlur(row, Mathf.Clamp(Mathf.RoundToInt(blur * 2f), 1, 32));
        Color[] px = new Color[w * h];
        for(int y = 0; y < h; y++) Array.Copy(row, 0, px, y * w, w);
        Texture2D tex = new(w, h, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        tex.SetPixels(px);
        tex.Apply(false, false);
        gradTex[key] = tex;
        return tex;
    }
    private static Color[] BoxBlur(Color[] src, int radius) {
        int n = src.Length;
        Color[] dst = new Color[n];
        for(int i = 0; i < n; i++) {
            float r = 0f, g = 0f, b = 0f, a = 0f;
            int cnt = 0;
            for(int k = -radius; k <= radius; k++) {
                int j = ((i + k) % n + n) % n;
                r += src[j].r; g += src[j].g; b += src[j].b; a += src[j].a;
                cnt++;
            }
            dst[i] = new Color(r / cnt, g / cnt, b / cnt, a / cnt);
        }
        return dst;
    }
    private static string GradKey(Color[] stops, float blur) {
        var sb = new StringBuilder(stops.Length * 8 + 4);
        foreach(Color c in stops) sb.Append(ColorUtility.ToHtmlStringRGBA(c));
        sb.Append('|').Append(Mathf.RoundToInt(blur));
        return sb.ToString();
    }
    private static Sprite GlowSprite() {
        if(glowSprite != null) return glowSprite;
        const int size = 64, margin = 22;
        Texture2D tex = new(size, size, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        Color[] px = new Color[size * size];
        for(int y = 0; y < size; y++) {
            float ay = EdgeAlpha(y, size, margin);
            for(int x = 0; x < size; x++) {
                px[y * size + x] = new Color(1f, 1f, 1f, ay * EdgeAlpha(x, size, margin));
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        glowSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(margin, margin, margin, margin));
        return glowSprite;
    }
    private static float EdgeAlpha(int i, int size, int margin) {
        float t = Mathf.Clamp01(Mathf.Min(i, size - 1 - i) / (float)margin);
        return t * t * (3f - 2f * t);
    }
    private static void DisposeCssRenderCaches() {
        foreach(Texture2D tex in gradTex.Values)
            if(tex != null) UnityEngine.Object.Destroy(tex);
        gradTex.Clear();
        if(glowSprite != null) {
            UnityEngine.Object.Destroy(glowSprite.texture);
            UnityEngine.Object.Destroy(glowSprite);
            glowSprite = null;
        }
        cssFx.Clear();
        cssCache = null;
        cssCacheKey = null;
    }
}
