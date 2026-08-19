namespace Quartz.Features.Discord;
public readonly record struct DiscordGuild(string Id, string Name);
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
public readonly record struct ReadStateEntry(string ChannelId, string LastMessageId, int MentionCount);
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
        || HasImageExtension(Filename);
    private static bool HasImageExtension(string name) {
        if(string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.EndsWith(".png", StringComparison.Ordinal)
            || n.EndsWith(".jpg", StringComparison.Ordinal)
            || n.EndsWith(".jpeg", StringComparison.Ordinal)
            || n.EndsWith(".gif", StringComparison.Ordinal)
            || n.EndsWith(".webp", StringComparison.Ordinal)
            || n.EndsWith(".bmp", StringComparison.Ordinal);
    }
}
public readonly record struct MediaRef(string Url, int? Width, int? Height);
public readonly record struct DiscordReaction(string EmojiId, string EmojiName, bool Animated, int Count, bool Me);
public readonly record struct DiscordSticker(string Id, string Name, int FormatType);
public readonly record struct ReactionUpdate(
    string ChannelId, string MessageId, string UserId,
    string EmojiId, string EmojiName, bool Animated, bool Added);
public readonly record struct DiscordMessage(
    string Id,
    string ChannelId,
    string AuthorName,
    string AuthorId,
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
