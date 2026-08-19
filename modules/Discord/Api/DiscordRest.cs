using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using static Quartz.Features.Discord.Json;
namespace Quartz.Features.Discord;
public sealed class DiscordRest : IDisposable {
    public const string ApiBase = "https://discord.com/api/v10";
    private const string UserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
        + "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private static readonly Regex UrlRegex = new(@"https?://[^\s<>]+");
    private readonly HttpClient http;
    public DiscordRest(string token) {
        http = new HttpClient { BaseAddress = new Uri(ApiBase + "/") };
        http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
    }
    public async Task<(string Id, string Name)> GetSelfAsync(CancellationToken ct = default) {
        JToken root = await GetJsonAsync("users/@me", ct);
        return (Str(root, "id"), DisplayName(root));
    }
    public async Task<List<DiscordGuild>> GetGuildsAsync(CancellationToken ct = default) {
        JToken root = await GetJsonAsync("users/@me/guilds", ct);
        List<DiscordGuild> list = [];
        if(root is JArray guilds)
            foreach(JToken guild in guilds)
                list.Add(new DiscordGuild(Str(guild, "id"), Str(guild, "name") ?? "?"));
        list.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }
    public async Task<GuildPerms> GetGuildAsync(string guildId, CancellationToken ct = default) {
        JToken root = await GetJsonAsync($"guilds/{guildId}", ct);
        List<DiscordRole> roles = [];
        JArray raw = Arr(root, "roles");
        if(raw != null)
            foreach(JToken role in raw)
                roles.Add(new DiscordRole(Str(role, "id"), Str(role, "name") ?? "", Bits(role, "permissions")));
        return new GuildPerms(guildId, Str(root, "owner_id") ?? "", roles);
    }
    public async Task<List<string>> GetSelfMemberRolesAsync(string guildId, CancellationToken ct = default) {
        JToken root = await GetJsonAsync($"users/@me/guilds/{guildId}/member", ct);
        List<string> list = [];
        JArray roles = Arr(root, "roles");
        if(roles != null)
            foreach(JToken role in roles)
                if(role.Type == JTokenType.String) list.Add(role.Value<string>());
        return list;
    }
    public async Task<List<DiscordChannel>> GetChannelsAsync(string guildId, CancellationToken ct = default) {
        JToken root = await GetJsonAsync($"guilds/{guildId}/channels", ct);
        List<DiscordChannel> list = [];
        if(root is JArray channels)
            foreach(JToken channel in channels)
                list.Add(new DiscordChannel(
                    Str(channel, "id"),
                    Str(channel, "name") ?? "",
                    Int(channel, "type") ?? 0,
                    Int(channel, "position") ?? 0,
                    Str(channel, "parent_id"),
                    Str(channel, "last_message_id"),
                    ParseOverwrites(channel)));
        return list;
    }
    public async Task<List<DiscordChannel>> GetDmChannelsAsync(CancellationToken ct = default) {
        JToken root = await GetJsonAsync("users/@me/channels", ct);
        List<DiscordChannel> list = [];
        if(root is JArray channels)
            foreach(JToken channel in channels) {
                int type = Int(channel, "type") ?? 1;
                if(type != 1 && type != 3) continue;
                list.Add(new DiscordChannel(
                    Str(channel, "id"),
                    DmName(channel, type),
                    type,
                    0,
                    null,
                    Str(channel, "last_message_id"),
                    Array.Empty<PermissionOverwrite>()));
            }
        list.Sort(static (a, b) => SnowflakeOf(b.LastMessageId).CompareTo(SnowflakeOf(a.LastMessageId)));
        return list;
    }
    public async Task<List<DiscordMessage>> GetMessagesAsync(
        string channelId, int limit = 50, string before = null, CancellationToken ct = default
    ) {
        string path = $"channels/{channelId}/messages?limit={limit}";
        if(!string.IsNullOrEmpty(before)) path += $"&before={before}";
        JToken root = await GetJsonAsync(path, ct);
        List<DiscordMessage> list = [];
        if(root is JArray messages)
            foreach(JToken message in messages) list.Add(ParseMessage(message));
        list.Reverse();
        return list;
    }
    public async Task SendMessageAsync(
        string channelId, string content, string replyToId = null, CancellationToken ct = default
    ) {
        object payload = replyToId == null
            ? new { content }
            : new { content, message_reference = new { message_id = replyToId } };
        using HttpRequestMessage request = new(HttpMethod.Post, $"channels/{channelId}/messages") {
            Content = Body(payload),
        };
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
    public async Task EditMessageAsync(
        string channelId, string messageId, string content, CancellationToken ct = default
    ) {
        using HttpRequestMessage request = new(
            new HttpMethod("PATCH"), $"channels/{channelId}/messages/{messageId}"
        ) {
            Content = Body(new { content }),
        };
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
    public async Task DeleteMessageAsync(string channelId, string messageId, CancellationToken ct = default) {
        using HttpRequestMessage request = new(HttpMethod.Delete, $"channels/{channelId}/messages/{messageId}");
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
    public async Task AddReactionAsync(
        string channelId, string messageId, string emoji, CancellationToken ct = default
    ) {
        string escaped = Uri.EscapeDataString(emoji);
        using HttpRequestMessage request = new(
            HttpMethod.Put, $"channels/{channelId}/messages/{messageId}/reactions/{escaped}/@me"
        ) {
            Content = Body(new { }),
        };
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
    public async Task RemoveReactionAsync(
        string channelId, string messageId, string emoji, CancellationToken ct = default
    ) {
        string escaped = Uri.EscapeDataString(emoji);
        using HttpRequestMessage request = new(
            HttpMethod.Delete, $"channels/{channelId}/messages/{messageId}/reactions/{escaped}/@me");
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
    public async Task AckAsync(string channelId, string messageId, CancellationToken ct = default) {
        using HttpRequestMessage request = new(
            HttpMethod.Post, $"channels/{channelId}/messages/{messageId}/ack"
        ) {
            Content = Body(new { }),
        };
        try {
            using HttpResponseMessage response = await http.SendAsync(request, ct);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
    public static DiscordMessage ParseMessage(JToken m) {
        string rawStamp = Str(m, "timestamp");
        DateTimeOffset stamp = rawStamp != null
            && DateTimeOffset.TryParse(
                rawStamp,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.Now;
        JObject author = Obj(m, "author");
        if(author != null) UserCache.Remember(Str(author, "id"), DisplayName(author));
        ParseReply(m, out string replyAuthor, out string replyContent);
        JObject reference = Obj(m, "message_reference");
        return new DiscordMessage(
            Str(m, "id"),
            Str(m, "channel_id") ?? "",
            author != null ? DisplayName(author) : "",
            author != null ? Str(author, "id") ?? "" : "",
            Str(m, "content") ?? "",
            stamp,
            ParseAttachments(m),
            ParseMedia(m),
            ParseEmbeds(m),
            ParseMentionedUsers(m),
            Flag(m, "mention_everyone"),
            replyAuthor,
            replyContent,
            reference == null ? null : Str(reference, "message_id"),
            Str(m, "guild_id"),
            ParseReactions(m),
            ParseStickers(m));
    }
    private static List<DiscordSticker> ParseStickers(JToken m) {
        List<DiscordSticker> list = [];
        JArray stickers = Arr(m, "sticker_items");
        if(stickers == null) return list;
        foreach(JToken sticker in stickers) {
            string id = Str(sticker, "id");
            if(id == null) continue;
            list.Add(new DiscordSticker(id, Str(sticker, "name") ?? "", Int(sticker, "format_type") ?? 1));
        }
        return list;
    }
    private static List<DiscordReaction> ParseReactions(JToken m) {
        List<DiscordReaction> list = [];
        JArray reactions = Arr(m, "reactions");
        if(reactions == null) return list;
        foreach(JToken reaction in reactions) {
            JObject emoji = Obj(reaction, "emoji");
            if(emoji == null) continue;
            list.Add(new DiscordReaction(
                Str(emoji, "id"),
                Str(emoji, "name") ?? "",
                Flag(emoji, "animated"),
                Int(reaction, "count") ?? 0,
                Flag(reaction, "me")));
        }
        return list;
    }
    private static Dictionary<string, string> ParseMentionedUsers(JToken m) {
        Dictionary<string, string> map = [];
        JArray mentions = Arr(m, "mentions");
        if(mentions == null) return map;
        foreach(JToken user in mentions) {
            string id = Str(user, "id");
            if(id == null) continue;
            string name = DisplayName(user);
            map[id] = name;
            UserCache.Remember(id, name);
        }
        return map;
    }
    private static List<MediaRef> ParseMedia(JToken m) {
        List<MediaRef> list = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> embedSources = new(StringComparer.OrdinalIgnoreCase);
        JArray embeds = Arr(m, "embeds");
        if(embeds != null)
            foreach(JToken embed in embeds) {
                string url = Str(embed, "url");
                if(url != null) embedSources.Add(url);
                if(!IsMediaOnly(embed)) continue;
                MediaRef? picked = PickEmbedImage(embed, embedSources);
                if(picked.HasValue && seen.Add(picked.Value.Url)) list.Add(picked.Value);
            }
        string content = Str(m, "content");
        if(content != null)
            foreach(Match match in UrlRegex.Matches(content)) {
                string url = match.Value.TrimEnd('.', ',', ')', '>', '"', '\'', '!');
                if(!IsImageUrl(url) || embedSources.Contains(url)) continue;
                if(seen.Add(url)) list.Add(new MediaRef(url, null, null));
            }
        return list;
    }
    private static MediaRef? PickEmbedImage(JToken embed, HashSet<string> embedSources) {
        MediaRef? chosen = null;
        foreach(string key in new[] { "image", "thumbnail" }) {
            JObject media = Obj(embed, key);
            if(media == null) continue;
            string url = Str(media, "url");
            string proxy = Str(media, "proxy_url");
            if(url != null) embedSources.Add(url);
            if(proxy != null) embedSources.Add(proxy);
            string best = proxy ?? url;
            if(chosen == null && best != null)
                chosen = new MediaRef(best, Int(media, "width"), Int(media, "height"));
        }
        return chosen;
    }
    private static List<DiscordEmbed> ParseEmbeds(JToken m) {
        List<DiscordEmbed> list = [];
        JArray embeds = Arr(m, "embeds");
        if(embeds == null) return list;
        HashSet<string> ignore = [];
        foreach(JToken embed in embeds) {
            if(IsMediaOnly(embed)) continue;
            MediaRef? image = PickEmbedImage(embed, ignore);
            list.Add(new DiscordEmbed(
                Text(embed, "title"),
                Text(embed, "description"),
                Str(embed, "url"),
                Int(embed, "color"),
                SubText(embed, "author", "name"),
                SubText(embed, "provider", "name"),
                SubText(embed, "footer", "text"),
                image?.Url,
                ParseFields(embed)));
        }
        return list;
    }
    private static List<EmbedField> ParseFields(JToken embed) {
        List<EmbedField> list = [];
        JArray fields = Arr(embed, "fields");
        if(fields == null) return list;
        foreach(JToken field in fields)
            list.Add(new EmbedField(Text(field, "name") ?? "", Text(field, "value") ?? ""));
        return list;
    }
    private static bool IsMediaOnly(JToken embed) {
        string type = Str(embed, "type");
        if(type == "image" || type == "gifv") return true;
        return EmbedHasMedia(embed) && !EmbedHasText(embed);
    }
    private static bool EmbedHasMedia(JToken embed) =>
        Obj(embed, "image") != null || Obj(embed, "thumbnail") != null || Obj(embed, "video") != null;
    private static bool EmbedHasText(JToken embed) =>
        Text(embed, "title") != null
        || Text(embed, "description") != null
        || SubText(embed, "author", "name") != null
        || SubText(embed, "provider", "name") != null
        || SubText(embed, "footer", "text") != null
        || Count(Arr(embed, "fields")) > 0;
    private static string SubText(JToken token, string obj, string key) {
        JObject nested = Obj(token, obj);
        return nested == null ? null : Text(nested, key);
    }
    private static bool IsImageUrl(string url) {
        int query = url.IndexOf('?');
        string path = (query >= 0 ? url[..query] : url).ToLowerInvariant();
        return path.EndsWith(".png", StringComparison.Ordinal)
            || path.EndsWith(".jpg", StringComparison.Ordinal)
            || path.EndsWith(".jpeg", StringComparison.Ordinal)
            || path.EndsWith(".gif", StringComparison.Ordinal)
            || path.EndsWith(".webp", StringComparison.Ordinal)
            || path.EndsWith(".bmp", StringComparison.Ordinal);
    }
    private static List<PermissionOverwrite> ParseOverwrites(JToken channel) {
        List<PermissionOverwrite> list = [];
        JArray overwrites = Arr(channel, "permission_overwrites");
        if(overwrites == null) return list;
        foreach(JToken overwrite in overwrites) {
            string id = Str(overwrite, "id");
            if(id == null) continue;
            int type = Int(overwrite, "type")
                ?? (int.TryParse(Str(overwrite, "type"), out int parsed) ? parsed : 0);
            list.Add(new PermissionOverwrite(id, type, Bits(overwrite, "allow"), Bits(overwrite, "deny")));
        }
        return list;
    }
    private static List<Attachment> ParseAttachments(JToken m) {
        List<Attachment> list = [];
        JArray attachments = Arr(m, "attachments");
        if(attachments == null) return list;
        foreach(JToken attachment in attachments) {
            string url = Str(attachment, "url");
            if(url == null) continue;
            list.Add(new Attachment(
                url,
                Str(attachment, "filename") ?? "",
                Str(attachment, "content_type"),
                Int(attachment, "width"),
                Int(attachment, "height")));
        }
        return list;
    }
    private static void ParseReply(JToken m, out string author, out string content) {
        author = null;
        content = null;
        JObject referenced = Obj(m, "referenced_message");
        if(referenced == null) return;
        JObject replyAuthor = Obj(referenced, "author");
        author = replyAuthor != null ? DisplayName(replyAuthor) : "unknown";
        string text = Str(referenced, "content");
        if(string.IsNullOrWhiteSpace(text)) {
            bool hasMedia = Count(Arr(referenced, "attachments")) > 0 || Count(Arr(referenced, "embeds")) > 0;
            text = hasMedia ? "[attachment]" : "";
        }
        content = text;
    }
    private static string DmName(JToken channel, int type) {
        if(type == 3) {
            string groupName = Text(channel, "name");
            if(groupName != null) return groupName;
        }
        List<string> names = [];
        JArray recipients = Arr(channel, "recipients");
        if(recipients != null)
            foreach(JToken user in recipients) names.Add(DisplayName(user));
        if(names.Count == 0) return type == 3 ? "group" : "dm";
        return string.Join(", ", names);
    }
    private static ulong SnowflakeOf(string id) => ulong.TryParse(id, out ulong value) ? value : 0UL;
    private static string DisplayName(JToken user) => Str(user, "global_name") ?? Str(user, "username") ?? "?";
    private async Task<JToken> GetJsonAsync(string path, CancellationToken ct) {
        using HttpResponseMessage response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync());
    }
    public void Dispose() => http.Dispose();
}
