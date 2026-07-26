using Quartz.Core;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Quartz.Resource;
public static class SpriteRegistry {
    private static readonly Dictionary<string, byte[]> images = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Sprite> cache = new(StringComparer.Ordinal);
    public static void Register(string key, byte[] png) {
        if(string.IsNullOrWhiteSpace(key)) throw new ArgumentException("a registered sprite needs a key");
        if(png == null || png.Length == 0) throw new ArgumentException("a registered sprite needs image bytes");
        Unregister(key);
        images[key] = png;
    }
    public static void Unregister(string key) {
        if(string.IsNullOrEmpty(key)) return;
        images.Remove(key);
        if(!cache.TryGetValue(key, out Sprite sprite)) return;
        cache.Remove(key);
        Destroy(sprite);
    }
    public static bool IsRegistered(string key) => key != null && images.ContainsKey(key);
    public static Sprite Get(string key) {
        if(string.IsNullOrEmpty(key)) return null;
        if(cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        if(!images.TryGetValue(key, out byte[] png)) return null;
        try {
            Texture2D tex = new(2, 2, TextureFormat.RGBA32, false) {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            if(!tex.LoadImage(png)) {
                Object.Destroy(tex);
                MainCore.Log.Wrn($"[Sprites] '{key}' is not a readable image");
                return null;
            }
            Sprite sprite = SpriteManager.Create(tex);
            cache[key] = sprite;
            return sprite;
        } catch(Exception e) {
            MainCore.Log.Err($"[Sprites] '{key}' failed to load: {e}");
            return null;
        }
    }
    public static void Clear() {
        foreach(Sprite sprite in cache.Values) Destroy(sprite);
        cache.Clear();
        images.Clear();
    }
    private static void Destroy(Sprite sprite) {
        if(sprite == null) return;
        if(sprite.texture != null) Object.Destroy(sprite.texture);
        Object.Destroy(sprite);
    }
}
