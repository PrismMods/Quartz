using System.Net.Http;
using Quartz.Async;
using Quartz.Core;
using Quartz.Resource;
using UnityEngine;
namespace Quartz.Features.Discord;
public static class AvatarCache {
    private const string Cdn = "https://cdn.discordapp.com";
    private const int Size = 64;
    private const int MaxEntries = 256;
    private static readonly Dictionary<string, Sprite> sprites = [];
    private static readonly HashSet<string> pending = [];
    private static readonly List<Texture2D> textures = [];
    private static HttpClient http;
    private static bool refreshQueued;
    public static event Action Changed;
    public static string UserUrl(string userId, string avatar) =>
        string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(avatar)
            ? null
            : $"{Cdn}/avatars/{userId}/{avatar}.png?size={Size}";
    public static string GuildUrl(string guildId, string icon) =>
        string.IsNullOrEmpty(guildId) || string.IsNullOrEmpty(icon)
            ? null
            : $"{Cdn}/icons/{guildId}/{icon}.png?size={Size}";
    public static string EmojiUrl(string emojiId) =>
        string.IsNullOrEmpty(emojiId) ? null : $"{Cdn}/emojis/{emojiId}.png?size={Size}";
    public static Sprite Get(string url) {
        if(string.IsNullOrEmpty(url)) return null;
        if(sprites.TryGetValue(url, out Sprite cached)) return cached;
        Fetch(url);
        return null;
    }
    private static void Fetch(string url) {
        if(!pending.Add(url)) return;
        if(sprites.Count >= MaxEntries) return;
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        Task.Run(async () => {
            byte[] data = null;
            try {
                using HttpResponseMessage response = await http.GetAsync(url);
                if(response.IsSuccessStatusCode) data = await response.Content.ReadAsByteArrayAsync();
            } catch(Exception e) {
                Diag.Ignore(e);
            }
            MainThread.Enqueue(() => Install(url, data));
        });
    }
    private static void Install(string url, byte[] data) {
        pending.Remove(url);
        if(data == null || data.Length == 0) return;
        try {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if(!texture.LoadImage(data)) {
                UnityEngine.Object.Destroy(texture);
                return;
            }
            textures.Add(texture);
            sprites[url] = SpriteManager.Create(texture);
            Queue();
        } catch(Exception e) {
            Diag.Ignore(e);
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
        foreach(Texture2D texture in textures)
            if(texture != null) UnityEngine.Object.Destroy(texture);
        textures.Clear();
    }
}
