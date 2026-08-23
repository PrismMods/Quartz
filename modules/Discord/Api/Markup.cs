using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace Quartz.Features.Discord;
public readonly record struct MessageEmoji(string Name, string Id, bool Animated);
public static class Markup {
    private static readonly Regex Token = new(
        @"(?<url>https?://[^\s<>]+)"
        + @"|<@!?(?<user>\d+)>"
        + @"|<@&(?<role>\d+)>"
        + @"|<#(?<channel>\d+)>"
        + @"|<t:(?<ts>-?\d+)(?::(?<tstyle>[tTdDfFR]))?>"
        + @"|<(?<anim>a)?:(?<ename>\w+):(?<eid>\d+)>"
        + @"|(?<here>@everyone|@here)");
    public static string Render(string content, IReadOnlyDictionary<string, string> mentioned) {
        if(string.IsNullOrEmpty(content)) return content;
        return Token.Replace(content, match => {
            if(match.Groups["user"].Success) {
                string id = match.Groups["user"].Value;
                string name = null;
                if(mentioned != null && mentioned.TryGetValue(id, out string found)) name = found;
                name ??= UserCache.Resolve(id);
                return "@" + (name ?? "unknown");
            }
            if(match.Groups["role"].Success) return "@role";
            if(match.Groups["channel"].Success) return "#" + ChannelName(match.Groups["channel"].Value);
            if(match.Groups["ename"].Success)
                return EmojiAtlas.CustomTag(match.Groups["eid"].Value, match.Groups["ename"].Value);
            if(match.Groups["ts"].Success) return Timestamp(match.Groups["ts"].Value);
            return match.Value;
        });
    }
    private static string ChannelName(string id) {
        foreach(DiscordChannel channel in DiscordSession.Channels)
            if(channel.Id == id) return channel.Name;
        return "channel";
    }
    private static string Timestamp(string raw) {
        if(!long.TryParse(raw, out long seconds)) return raw;
        try {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        } catch(Exception e) {
            Quartz.Core.Diag.Ignore(e);
            return raw;
        }
    }
    public static List<MessageEmoji> Emojis(string content) {
        List<MessageEmoji> found = [];
        if(string.IsNullOrEmpty(content)) return found;
        foreach(Match match in Token.Matches(content)) {
            if(!match.Groups["eid"].Success) continue;
            found.Add(new MessageEmoji(
                match.Groups["ename"].Value,
                match.Groups["eid"].Value,
                match.Groups["anim"].Success));
        }
        return found;
    }
    public static bool EmojiOnly(string content) {
        if(string.IsNullOrEmpty(content)) return false;
        string stripped = Token.Replace(content, match => match.Groups["eid"].Success ? "" : match.Value);
        return stripped.Trim().Length == 0 && Emojis(content).Count > 0;
    }
    public static string ResolveMentions(string text) {
        if(string.IsNullOrEmpty(text) || !text.Contains('@')) return text;
        StringBuilder result = new(text.Length + 16);
        int i = 0;
        while(i < text.Length) {
            if(text[i] != '@') {
                result.Append(text[i++]);
                continue;
            }
            int end = i + 1;
            while(end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] is '_' or '.' or '-')) end++;
            string name = text[(i + 1)..end];
            string id = name.Length == 0 ? null : UserCache.FindId(name);
            if(id == null) {
                result.Append(text[i..end]);
            } else {
                result.Append("<@").Append(id).Append('>');
            }
            i = end;
        }
        return result.ToString();
    }
}
