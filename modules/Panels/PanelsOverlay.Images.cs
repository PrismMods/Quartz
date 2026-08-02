using System.Globalization;
using System.Runtime.CompilerServices;
using Quartz.Compat.Game;
using Quartz.Core;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.Features.Panels;
public static partial class PanelsOverlay {
    private const int ImageAtlasPadding = 2;
    private const int ImageAtlasMaxSize = 4096;
    private const float ImageRetryDelay = 2f;
    public const float MinImageScale = 0.1f;
    public const float MaxImageScale = 8f;
    private static readonly Dictionary<string, Texture2D> imageTextures = new(StringComparer.Ordinal);
    private static bool imagesUnsupported;
    public static string ImagesDir => Path.Combine(MainCore.Paths.RootPath, "PanelImages");
    public static string EnsureImagesDir() {
        string dir = ImagesDir;
        try {
            Directory.CreateDirectory(dir);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Panels] could not create {dir}: {e.Message}");
        }
        return dir;
    }
    public static string ResolveImagePath(string raw) {
        if(string.IsNullOrWhiteSpace(raw)) return null;
        try {
            string trimmed = raw.Trim();
            if(File.Exists(trimmed)) return Path.GetFullPath(trimmed);
            string local = Path.Combine(ImagesDir, trimmed);
            return File.Exists(local) ? Path.GetFullPath(local) : null;
        } catch(Exception e) {
            Diag.Warn(e, "panel image path");
            return null;
        }
    }
    private static Texture2D ImageTexture(string full) {
        if(imageTextures.TryGetValue(full, out Texture2D cached)) return cached == null ? null : cached;
        Texture2D tex = null;
        try {
            Texture2D made = new(2, 2, TextureFormat.RGBA32, false) {
                name = "QuartzPanelImage",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if(made.LoadImage(File.ReadAllBytes(full))) tex = made;
            else Object.Destroy(made);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Panels] image load failed ({full}): {e.Message}");
            tex = null;
        }
        imageTextures[full] = tex;
        return tex;
    }
    private static int ImageSignature(PanelConfig config) {
        int hash = 17;
        foreach(StatEntry entry in config.Stats) {
            if(!entry.Enabled || entry.Id != "image" || string.IsNullOrWhiteSpace(entry.Image)) continue;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(entry.Image);
            hash = (hash * 31) + entry.ImageScale.GetHashCode();
            hash = (hash * 31) + (entry.Color is { Enabled: true } ? 1 : 0);
        }
        return hash;
    }
    private static void SyncPanelImages(LivePanel p) {
        if(p?.Config == null) return;
        int signature = ImageSignature(p.Config);
        float now = Time.unscaledTime;
        if(signature == p.ImageSignature && (p.ImageRetryAt <= 0f || now < p.ImageRetryAt)) return;
        p.ImageSignature = signature;
        p.ImageRetryAt = 0f;
        p.ImageTags.Clear();
        List<StatEntry> entries = [];
        List<Texture2D> textures = [];
        foreach(StatEntry entry in p.Config.Stats) {
            if(!entry.Enabled || entry.Id != "image" || string.IsNullOrWhiteSpace(entry.Image)) continue;
            string full = ResolveImagePath(entry.Image);
            if(full == null) {
                p.ImageRetryAt = now + ImageRetryDelay;
                continue;
            }
            Texture2D tex = ImageTexture(full);
            if(tex == null) continue;
            entries.Add(entry);
            textures.Add(tex);
        }
        ReleasePanelImages(p);
        if(p.Text != null) p.Text.text = "";
        p.LastBody = null;
        p.Dirty = true;
        if(entries.Count == 0 || !TryBuildPanelSprites(p, textures)) return;
        for(int i = 0; i < entries.Count; i++) p.ImageTags[entries[i]] = ImageTag(i, entries[i]);
    }
    private static bool TryBuildPanelSprites(LivePanel p, List<Texture2D> textures) {
        if(imagesUnsupported) return false;
        try {
            return BuildPanelSprites(p, textures);
        } catch(Exception e) {
            imagesUnsupported = true;
            MainCore.Log.Wrn(
                "[Panels] panel images are unavailable on this game build "
                + $"({GameVersion.DisplayRelease}): {e.Message}");
            return false;
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool BuildPanelSprites(LivePanel p, List<Texture2D> textures) {
        Texture2D atlas = null;
        Material material = null;
        TMP_SpriteAsset asset = null;
        try {
            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) {
                name = "QuartzPanelAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Rect[] uvs = atlas.PackTextures([.. textures], ImageAtlasPadding, ImageAtlasMaxSize, false);
            material = new Material(SpriteShader()) { name = "QuartzPanelSpriteMat", mainTexture = atlas };
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "QuartzPanelSprites";
            asset.spriteSheet = atlas;
            asset.material = material;
            asset.spriteCharacterTable.Clear();
            asset.spriteGlyphTable.Clear();
            for(int i = 0; i < uvs.Length; i++) {
                Rect uv = uvs[i];
                int w = Mathf.Max(1, Mathf.RoundToInt(uv.width * atlas.width));
                int h = Mathf.Max(1, Mathf.RoundToInt(uv.height * atlas.height));
                TMP_SpriteGlyph glyph = new() {
                    index = (uint)i,
                    metrics = new GlyphMetrics(w, h, 0f, h, w),
                    glyphRect = new GlyphRect(
                        Mathf.RoundToInt(uv.x * atlas.width),
                        Mathf.RoundToInt(uv.y * atlas.height),
                        w, h
                    ),
                    scale = 1f,
                    atlasIndex = 0,
                };
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFEu, glyph) {
                    name = SpriteName(i),
                    scale = 1f,
                });
            }
            asset.UpdateLookupTables();
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Panels] image atlas build failed: {e.Message}");
            if(asset != null) Object.Destroy(asset);
            if(material != null) Object.Destroy(material);
            if(atlas != null) Object.Destroy(atlas);
            return false;
        }
        p.Atlas = atlas;
        p.SpriteMaterial = material;
        p.Sprites = asset;
        if(p.Text != null) p.Text.spriteAsset = asset;
        return true;
    }
    private static Shader SpriteShader() {
        try {
            TMP_SpriteAsset fallback = TMP_Settings.defaultSpriteAsset;
            if(fallback != null && fallback.material != null && fallback.material.shader != null)
                return fallback.material.shader;
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        Shader sprite = Shader.Find("TextMeshPro/Sprite");
        return sprite != null ? sprite : Canvas.GetDefaultCanvasMaterial().shader;
    }
    private static string SpriteName(int index) => "qimg" + index.ToString(CultureInfo.InvariantCulture);
    private static string ImageTag(int index, StatEntry entry) {
        string sprite = "<sprite name=\"" + SpriteName(index) + "\""
            + (entry.Color is { Enabled: true } ? " tint=1" : "") + ">";
        float scale = Mathf.Clamp(entry.ImageScale, MinImageScale, MaxImageScale);
        if(Mathf.Abs(scale - 1f) < 0.001f) return sprite;
        int percent = Mathf.Max(1, Mathf.RoundToInt(scale * 100f));
        return "<size=" + percent.ToString(CultureInfo.InvariantCulture) + "%>" + sprite + "</size>";
    }
    private static void ReleasePanelImages(LivePanel p) {
        if(p == null) return;
        if(p.Text != null) p.Text.spriteAsset = null;
        if(p.Sprites != null) Object.Destroy(p.Sprites);
        if(p.SpriteMaterial != null) Object.Destroy(p.SpriteMaterial);
        if(p.Atlas != null) Object.Destroy(p.Atlas);
        p.Sprites = null;
        p.SpriteMaterial = null;
        p.Atlas = null;
    }
    private static void DisposeImageCache() {
        foreach(Texture2D tex in imageTextures.Values)
            if(tex != null) Object.Destroy(tex);
        imageTextures.Clear();
    }
}
