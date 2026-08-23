using System.Net.Http;
using System.Text;
using Quartz.Async;
using Quartz.Core;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
namespace Quartz.Features.Discord;
public static class EmojiAtlas {
    private const string Twemoji = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72/";
    private const int MaxSprites = 192;
    private const int AtlasPadding = 2;
    private const int AtlasMaxSize = 2048;
    private static readonly Dictionary<string, int> indexOf = [];
    private static readonly List<Texture2D> textures = [];
    private static readonly List<string> names = [];
    private static readonly HashSet<string> pending = [];
    private static readonly HashSet<string> failed = [];
    private static TMP_SpriteAsset asset;
    private static Texture2D atlas;
    private static Material material;
    private static HttpClient http;
    private static bool refreshQueued;
    public static TMP_SpriteAsset Asset => asset;
    public static event Action Changed;
    public static string Inline(string text) {
        if(string.IsNullOrEmpty(text)) return text;
        StringBuilder result = null;
        int i = 0;
        while(i < text.Length) {
            int length = EmojiRun(text, i, out string key);
            if(length == 0) {
                result?.Append(text[i]);
                i++;
                continue;
            }
            result ??= new StringBuilder(text.Length + 32).Append(text, 0, i);
            if(indexOf.TryGetValue(key, out int slot)) result.Append("<sprite name=\"").Append(names[slot]).Append("\">");
            else {
                result.Append(text, i, length);
                Request(key, Twemoji + key + ".png");
            }
            i += length;
        }
        return result == null ? text : result.ToString();
    }
    public static string CustomTag(string emojiId, string emojiName) {
        string key = "c" + emojiId;
        if(indexOf.TryGetValue(key, out int slot)) return "<sprite name=\"" + names[slot] + "\">";
        Request(key, AvatarCache.EmojiUrl(emojiId));
        return ":" + emojiName + ":";
    }
    private static int EmojiRun(string text, int start, out string key) {
        key = null;
        List<int> points = [];
        int i = start;
        while(i < text.Length) {
            int size = char.IsSurrogatePair(text, i) ? 2 : 1;
            int point = char.ConvertToUtf32(text, i);
            if(points.Count == 0) {
                if(!IsEmojiStart(point)) return 0;
            } else if(!IsEmojiContinuation(point)) {
                break;
            }
            if(point != 0xFE0F) points.Add(point);
            i += size;
        }
        while(points.Count > 0 && points[^1] == 0x200D) points.RemoveAt(points.Count - 1);
        if(points.Count == 0) return 0;
        StringBuilder builder = new();
        for(int p = 0; p < points.Count; p++) {
            if(p > 0) builder.Append('-');
            builder.Append(points[p].ToString("x"));
        }
        key = builder.ToString();
        return i - start;
    }
    private static bool IsEmojiStart(int point) =>
        point is (>= 0x1F000 and <= 0x1FAFF)
            or (>= 0x2600 and <= 0x27BF)
            or (>= 0x2B00 and <= 0x2BFF)
            or (>= 0x2190 and <= 0x21FF)
            or 0x203C or 0x2049 or 0x00A9 or 0x00AE or 0x2122;
    private static bool IsEmojiContinuation(int point) =>
        IsEmojiStart(point) || point is 0xFE0F or 0x200D or (>= 0x1F3FB and <= 0x1F3FF) or (>= 0xE0020 and <= 0xE007F);
    private static void Request(string key, string url) {
        if(url == null || indexOf.ContainsKey(key) || failed.Contains(key)) return;
        if(indexOf.Count >= MaxSprites || !pending.Add(key)) return;
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        Task.Run(async () => {
            byte[] data = null;
            try {
                using HttpResponseMessage response = await http.GetAsync(url);
                if(response.IsSuccessStatusCode) data = await response.Content.ReadAsByteArrayAsync();
            } catch(Exception e) {
                Diag.Ignore(e);
            }
            MainThread.Enqueue(() => Install(key, data));
        });
    }
    private static void Install(string key, byte[] data) {
        pending.Remove(key);
        if(data == null || data.Length == 0) {
            failed.Add(key);
            return;
        }
        try {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if(!texture.LoadImage(data)) {
                UnityEngine.Object.Destroy(texture);
                failed.Add(key);
                return;
            }
            indexOf[key] = textures.Count;
            names.Add("e" + textures.Count);
            textures.Add(texture);
            if(Rebuild()) Queue();
        } catch(Exception e) {
            MainCore.Log.Wrn("[Discord] emoji atlas install failed: " + e.Message);
        }
    }
    private static bool Rebuild() {
        Texture2D packed = null;
        Material sprites = null;
        TMP_SpriteAsset built = null;
        try {
            packed = new Texture2D(2, 2, TextureFormat.RGBA32, false) {
                name = "QuartzDiscordEmojiAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Rect[] uvs = packed.PackTextures([.. textures], AtlasPadding, AtlasMaxSize, false);
            sprites = new Material(SpriteShader()) { name = "QuartzDiscordEmojiMat", mainTexture = packed };
            built = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            built.name = "QuartzDiscordEmoji";
            built.spriteSheet = packed;
            built.material = sprites;
            built.spriteInfoList = [];
            built.UpdateLookupTables();
            built.spriteCharacterTable.Clear();
            built.spriteGlyphTable.Clear();
            for(int i = 0; i < uvs.Length; i++) {
                Rect uv = uvs[i];
                int w = Mathf.Max(1, Mathf.RoundToInt(uv.width * packed.width));
                int h = Mathf.Max(1, Mathf.RoundToInt(uv.height * packed.height));
                TMP_SpriteGlyph glyph = new() {
                    index = (uint)i,
                    metrics = new GlyphMetrics(w, h, 0f, h * 0.85f, w),
                    glyphRect = new GlyphRect(
                        Mathf.RoundToInt(uv.x * packed.width),
                        Mathf.RoundToInt(uv.y * packed.height),
                        w, h),
                    scale = 1f,
                    atlasIndex = 0,
                };
                built.spriteGlyphTable.Add(glyph);
                built.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFEu, glyph) {
                    name = names[i],
                    scale = 1f,
                });
            }
            built.UpdateLookupTables();
        } catch(Exception e) {
            MainCore.Log.Wrn("[Discord] emoji atlas build failed: " + e.Message);
            if(built != null) UnityEngine.Object.Destroy(built);
            if(sprites != null) UnityEngine.Object.Destroy(sprites);
            if(packed != null) UnityEngine.Object.Destroy(packed);
            return false;
        }
        if(asset != null) UnityEngine.Object.Destroy(asset);
        if(material != null) UnityEngine.Object.Destroy(material);
        if(atlas != null) UnityEngine.Object.Destroy(atlas);
        asset = built;
        material = sprites;
        atlas = packed;
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
        return Shader.Find("TextMeshPro/Sprite");
    }
    private static void Queue() {
        if(refreshQueued) return;
        refreshQueued = true;
        MainThread.Enqueue(() => {
            refreshQueued = false;
            Changed?.Invoke();
        });
    }
}
