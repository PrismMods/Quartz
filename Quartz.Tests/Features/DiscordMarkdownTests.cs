using Quartz.Features.Discord;
using static Asserts;
static class DiscordMarkdownTests {
    public static void TestInlineEmphasis() {
        Assert(Markdown.ToRichText("**hi**") == "<b>hi</b>", "bold");
        Assert(Markdown.ToRichText("__hi__") == "<u>hi</u>", "underline");
        Assert(Markdown.ToRichText("~~hi~~") == "<s>hi</s>", "strikethrough");
        Assert(Markdown.ToRichText("*hi*") == "<i>hi</i>", "italic with stars");
        Assert(Markdown.ToRichText("_hi_") == "<i>hi</i>", "italic with underscores");
    }
    public static void TestBoldWinsOverItalic() {
        Assert(
            Markdown.ToRichText("**bold**") == "<b>bold</b>",
            "a double star must not be read as two italics"
        );
    }
    public static void TestHeadingsAndSubtext() {
        Assert(Markdown.ToRichText("# Title").Contains("138%"), "h1 scales up");
        Assert(Markdown.ToRichText("## Title").Contains("124%"), "h2 scales up");
        Assert(Markdown.ToRichText("### Title").Contains("112%"), "h3 scales up");
        Assert(Markdown.ToRichText("# Title").Contains("<b>Title</b>"), "headings are bold and drop the hashes");
        string subtext = Markdown.ToRichText("-# small");
        Assert(subtext.Contains("85%") && subtext.Contains("small"), "subtext shrinks");
        Assert(!subtext.Contains("-#"), "the subtext marker is consumed");
    }
    public static void TestQuotesAreIndentedNotLiteral() {
        string quoted = Markdown.ToRichText("> quoted line");
        Assert(quoted.Contains("<indent"), "a quote indents");
        Assert(!quoted.Contains("> quoted"), "the quote marker is consumed");
    }
    public static void TestLinksShowTheirLabel() {
        string rendered = Markdown.ToRichText("[Rules](https://example.invalid/doc)");
        Assert(rendered.Contains("Rules"), "the label survives");
        Assert(!rendered.Contains("https://example.invalid/doc"), "the url is not shown for a labelled link");
        Assert(rendered.Contains("#00A8FC"), "links are coloured");
    }
    public static void TestBareUrlsAreColoured() {
        string rendered = Markdown.ToRichText("see https://example.invalid/x now");
        Assert(rendered.Contains("https://example.invalid/x"), "a bare url stays visible");
        Assert(rendered.Contains("#00A8FC"), "a bare url is coloured");
    }
    public static void TestCodeIsNotReparsed() {
        string rendered = Markdown.ToRichText("`**not bold**`");
        Assert(!rendered.Contains("<b>"), "markdown inside a code span must stay literal");
        Assert(rendered.Contains("**not bold**"), "the code text is preserved verbatim");
    }
    public static void TestUserTagsCannotInjectRichText() {
        string rendered = Markdown.ToRichText("<color=#ff0000>red</color> and <b>bold</b>");
        Assert(rendered.Contains("<noparse>"), "raw angle brackets must be escaped");
        Assert(!rendered.Contains("<color=#ff0000>"), "a user must not be able to inject a live colour tag");
    }
    public static void TestSpoilersAreHidden() {
        string rendered = Markdown.ToRichText("||secret||");
        Assert(rendered.Contains("secret"), "the text is still present");
        Assert(rendered.Contains("<mark="), "a spoiler is covered by a mark");
        Assert(!rendered.Contains("||"), "the spoiler markers are consumed");
    }
    public static void TestHeadingWithLeadingEmojiAndQuote() {
        string flag = "\U0001F1EC\U0001F1E7";
        string heading = Markdown.ToRichText("# " + flag + " English Rules for ADOFAI Community Server");
        Assert(heading.Contains("138%"), "a heading with a leading emoji must still scale: " + heading);
        Assert(!heading.StartsWith("# ", StringComparison.Ordinal), "the hash must be consumed: " + heading);
        string quoted = Markdown.ToRichText("> # Heading inside a quote");
        Assert(quoted.Contains("<indent"), "quote applies: " + quoted);
        Assert(quoted.Contains("138%"), "a heading inside a quote must still be a heading: " + quoted);
        Assert(!quoted.Contains("# Heading"), "the hash must be consumed inside a quote: " + quoted);
        string bulletQuote = Markdown.ToRichText("> - item");
        Assert(bulletQuote.Contains("\u2022"), "a bullet inside a quote must render: " + bulletQuote);
        string subQuote = Markdown.ToRichText("> -# note");
        Assert(subQuote.Contains("85%"), "subtext inside a quote must shrink: " + subQuote);
        string crlf = Markdown.ToRichText("intro\r\n# Heading after CRLF");
        Assert(crlf.Contains("138%"), "a heading after a CRLF line ending must apply: " + crlf);
    }
    public static void TestPlainTextIsUntouched() {
        Assert(Markdown.ToRichText("just words") == "just words", "plain text passes through unchanged");
        Assert(Markdown.ToRichText("") == "", "empty stays empty");
        Assert(Markdown.ToRichText(null) == null, "null stays null");
    }
    public static void TestMultilineKeepsEveryLine() {
        string rendered = Markdown.ToRichText("# Head\n> quote\nplain");
        Assert(rendered.Split('\n').Length == 3, "line count is preserved");
        Assert(rendered.Contains("plain"), "trailing plain lines survive");
    }
}
