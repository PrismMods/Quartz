using System.Text;
using System.Text.RegularExpressions;
namespace Quartz.Features.Discord;
public static class Markdown {
    private const string Link = "#00A8FC";
    private const string Muted = "#949BA4";
    private const string CodeBg = "#1E1F22";
    private const string Sentinel = "\u0001";
    private static readonly Regex CodeBlock = new(@"```(?:[a-zA-Z0-9+#-]*\n)?([\s\S]*?)```");
    private static readonly Regex CodeSpan = new(@"`([^`\n]+)`");
    private static readonly Regex MarkdownLink = new(@"\[([^\]]+)\]\((https?://[^\s)]+)\)");
    private static readonly Regex BareUrl = new(@"(?<![=""'>])(https?://[^\s<>]+)");
    private static readonly Regex Bold = new(@"\*\*([^\*]+)\*\*");
    private static readonly Regex Underline = new(@"__([^_]+)__");
    private static readonly Regex Strike = new(@"~~([^~]+)~~");
    private static readonly Regex ItalicStar = new(@"(?<!\*)\*([^\*\n]+)\*(?!\*)");
    private static readonly Regex ItalicScore = new(@"(?<!_)_([^_\n]+)_(?!_)");
    private static readonly Regex Spoiler = new(@"\|\|([\s\S]+?)\|\|");
    public static string ToRichText(string content) {
        if(string.IsNullOrEmpty(content)) return content;
        List<string> guarded = [];
        string working = Escape(content);
        working = CodeBlock.Replace(working, match => Guard(guarded, Block(match.Groups[1].Value.TrimEnd())));
        working = CodeSpan.Replace(working, match => Guard(guarded, Inline(match.Groups[1].Value)));
        working = MarkdownLink.Replace(working, match => Guard(guarded, Anchor(match.Groups[1].Value)));
        working = BareUrl.Replace(working, match => Guard(guarded, Anchor(match.Value)));
        working = Spoiler.Replace(working, match => Guard(guarded, Hidden(match.Groups[1].Value)));
        working = Bold.Replace(working, "<b>$1</b>");
        working = Underline.Replace(working, "<u>$1</u>");
        working = Strike.Replace(working, "<s>$1</s>");
        working = ItalicStar.Replace(working, "<i>$1</i>");
        working = ItalicScore.Replace(working, "<i>$1</i>");
        working = Lines(working);
        return Unguard(working, guarded);
    }
    private static string Escape(string value) => value.Replace("<", "<noparse><</noparse>");
    private static string Guard(List<string> store, string rendered) {
        store.Add(rendered);
        return Sentinel + (store.Count - 1) + Sentinel;
    }
    private static string Unguard(string value, List<string> store) {
        for(int i = 0; i < store.Count; i++)
            value = value.Replace(Sentinel + i + Sentinel, store[i]);
        return value;
    }
    private static string Anchor(string label) => $"<color={Link}>{label}</color>";
    private static string Inline(string code) => $"<mark={CodeBg}80><color=#E6E6E6>{code}</color></mark>";
    private static string Block(string code) => $"<mark={CodeBg}A0><color=#E6E6E6>\n{code}\n</color></mark>";
    private static string Hidden(string text) => $"<mark=#3C3F45FF><color=#3C3F45>{text}</color></mark>";
    private static string Lines(string value) {
        string[] lines = value.Split('\n');
        StringBuilder result = new(value.Length + 64);
        for(int i = 0; i < lines.Length; i++) {
            if(i > 0) result.Append('\n');
            result.Append(Line(lines[i]));
        }
        return result.ToString();
    }
    private static string Line(string line) {
        string trimmed = line.TrimStart();
        int indent = line.Length - trimmed.Length;
        string pad = indent > 0 ? new string(' ', indent) : "";
        if(trimmed.StartsWith("> ", StringComparison.Ordinal))
            return $"{pad}<color=#B5BAC1><indent=10px>{Line(trimmed[2..])}</indent></color>";
        if(trimmed == ">") return "";
        if(trimmed.StartsWith("-# ", StringComparison.Ordinal))
            return $"{pad}<size=85%><color={Muted}>{trimmed[3..]}</color></size>";
        if(trimmed.StartsWith("### ", StringComparison.Ordinal))
            return $"{pad}<size=112%><b>{trimmed[4..]}</b></size>";
        if(trimmed.StartsWith("## ", StringComparison.Ordinal))
            return $"{pad}<size=124%><b>{trimmed[3..]}</b></size>";
        if(trimmed.StartsWith("# ", StringComparison.Ordinal))
            return $"{pad}<size=138%><b>{trimmed[2..]}</b></size>";
        if(trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            return $"{pad}<indent=10px>•  {trimmed[2..]}</indent>";
        return line;
    }
}
