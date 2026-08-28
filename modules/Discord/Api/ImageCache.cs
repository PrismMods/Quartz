using System.Net.Http;
using Quartz.Async;
using Quartz.Core;
using Quartz.Resource;
using UnityEngine;
namespace Quartz.Features.Discord;
public static class ImageCache {
    private const int MaxEntries = 48;
    private const int MaxBytes = 8 * 1024 * 1024;
    private static readonly Dictionary<string, Sprite> sprites = [];
    private static readonly HashSet<string> pending = [];
    private static readonly HashSet<string> failed = [];
    private static readonly List<Texture2D> textures = [];
    private static HttpClient http;
    private static bool refreshQueued;
    public static event Action Changed;
    public static bool IsImageUrl(string url) {
        if(string.IsNullOrEmpty(url)) return false;
        int query = url.IndexOf('?');
        string path = (query >= 0 ? url[..query] : url).ToLowerInvariant();
        return path.EndsWith(".png", StringComparison.Ordinal)
            || path.EndsWith(".jpg", StringComparison.Ordinal)
            || path.EndsWith(".jpeg", StringComparison.Ordinal)
            || path.EndsWith(".gif", StringComparison.Ordinal)
            || path.EndsWith(".webp", StringComparison.Ordinal)
            || path.EndsWith(".bmp", StringComparison.Ordinal);
    }
    public static Sprite Get(string url) {
        if(string.IsNullOrEmpty(url)) return null;
        if(sprites.TryGetValue(url, out Sprite cached)) return cached;
        Fetch(url);
        return null;
    }
    public static bool Failed(string url) => url != null && failed.Contains(url);
    private static void Fetch(string url) {
        if(failed.Contains(url) || sprites.Count >= MaxEntries || !pending.Add(url)) return;
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        Task.Run(async () => {
            byte[] data = null;
            try {
                using HttpResponseMessage response = await http.GetAsync(url);
                if(response.IsSuccessStatusCode) {
                    long? length = response.Content.Headers.ContentLength;
                    if(length == null || length <= MaxBytes)
                        data = await response.Content.ReadAsByteArrayAsync();
                }
            } catch(Exception e) {
                Diag.Ignore(e);
            }
            MainThread.Enqueue(() => Install(url, data));
        });
    }
    private static void Install(string url, byte[] data) {
        pending.Remove(url);
        if(data == null || data.Length == 0 || data.Length > MaxBytes) {
            failed.Add(url);
            Queue();
            return;
        }
        try {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if(!texture.LoadImage(data)) {
                UnityEngine.Object.Destroy(texture);
                failed.Add(url);
                Queue();
                return;
            }
            textures.Add(texture);
            sprites[url] = SpriteManager.Create(texture);
            Queue();
        } catch(Exception e) {
            MainCore.Log.Wrn("[Discord] image decode failed: " + e.Message);
            failed.Add(url);
        }
    }
    private static void Queue() {
        if(refreshQueued) return;
        refreshQueued = true;
        MainThread.Enqueue(() => {
            refreshQueued = false;
            Changed?.Invoke();
        });
    }
    public static void Clear() {
        sprites.Clear();
        pending.Clear();
        failed.Clear();
        foreach(Texture2D texture in textures)
            if(texture != null) UnityEngine.Object.Destroy(texture);
        textures.Clear();
    }
}
