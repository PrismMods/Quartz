namespace Quartz.Features.Discord.Api;
public readonly record struct DiscordGuild(string Id, string Name, string Icon);
public readonly record struct DiscordRole(string Id, string Name, ulong Permissions);
public readonly record struct PermissionOverwrite(string Id, int Type, ulong Allow, ulong Deny);
public readonly record struct GuildPerms(string GuildId, string OwnerId, IReadOnlyList<DiscordRole> Roles);
public readonly record struct DiscordChannel(
    string Id,
    string Name,
    int Type,
    int Position,
    string ParentId,
    string LastMessageId,
    IReadOnlyList<PermissionOverwrite> Overwrites);
public readonly record struct EmbedField(string Name, string Value);
public readonly record struct DiscordEmbed(
    string Title,
    string Description,
    string Url,
    int? Color,
    string AuthorName,
    string ProviderName,
    string Footer,
    string ImageUrl,
    IReadOnlyList<EmbedField> Fields);
public readonly record struct Attachment(string Url, string Filename, string ContentType, int? Width, int? Height) {
    public bool IsImage =>
        (ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)
        || IsImageUrl(Filename);
    // Ignores the query string so CDN urls match.
    internal static bool IsImageUrl(string url) {
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
}
public readonly record struct MediaRef(string Url, int? Width, int? Height);
public readonly record struct DiscordReaction(string EmojiId, string EmojiName, bool Animated, int Count, bool Me);
public readonly record struct DiscordSticker(string Id, string Name, int FormatType);
public readonly record struct DiscordMessage(
    string Id,
    string ChannelId,
    string AuthorName,
    string AuthorId,
    string AuthorAvatar,
    string Content,
    DateTimeOffset Timestamp,
    IReadOnlyList<Attachment> Attachments,
    IReadOnlyList<MediaRef> Media,
    IReadOnlyList<DiscordEmbed> Embeds,
    IReadOnlyDictionary<string, string> MentionedUsers,
    bool MentionEveryone,
    string ReplyAuthor,
    string ReplyContent,
    string ReplyMessageId,
    string GuildId,
    IReadOnlyList<DiscordReaction> Reactions,
    IReadOnlyList<DiscordSticker> Stickers);
