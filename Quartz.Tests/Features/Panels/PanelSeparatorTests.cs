using Newtonsoft.Json.Linq;
using Quartz.Features.Panels;
using static Asserts;
static class PanelSeparatorTests {
    private static void Draws(string raw, LabelSeparatorSpacing spacing, string want) {
        string got = PanelSeparator.Draw(raw, spacing);
        Assert(got == want, $"'{raw}' with {spacing} draws '{want}', got '{got}'");
    }
    // How every panel drew before the spacing setting: empty is one space, one character is padded, longer is as typed.
    private static string OldDraw(string raw) => string.IsNullOrEmpty(raw) ? " " : raw.Length == 1 ? " " + raw + " " : raw;

    public static void TestSpacingModesDrawTheSeparator() {
        Draws("|", LabelSeparatorSpacing.Both, " | ");
        Draws(":", LabelSeparatorSpacing.Both, " : ");
        Draws(": ", LabelSeparatorSpacing.Both, " : ");
        Draws(" - ", LabelSeparatorSpacing.Both, " - ");
        Draws("ab", LabelSeparatorSpacing.Both, " ab ");
        Draws("|", LabelSeparatorSpacing.After, "| ");
        Draws(":", LabelSeparatorSpacing.After, ": ");
        Draws(" : ", LabelSeparatorSpacing.After, ": ");
        Draws(": ", LabelSeparatorSpacing.None, ": ");
        Draws("|", LabelSeparatorSpacing.None, "|");
        Draws("  ", LabelSeparatorSpacing.None, "  ");
        Draws("->", LabelSeparatorSpacing.None, "->");
        Draws(" | ", LabelSeparatorSpacing.None, " | ");
    }

    public static void TestEmptyAndBlankSeparatorsKeepTheirLook() {
        foreach(LabelSeparatorSpacing s in new[] { LabelSeparatorSpacing.Both, LabelSeparatorSpacing.After, LabelSeparatorSpacing.None })
            Draws("", s, " ");
        Draws(null, LabelSeparatorSpacing.None, " ");
        Draws(" ", LabelSeparatorSpacing.Both, "   ");   // a lone space drew three before the setting
    }

    public static void TestPanelsSavedBeforeTheSettingKeepTheirLook() {
        foreach(string raw in new[] { "|", ":", " ", ": ", "  ", "", " | ", "->", "😀" }) {
            var panel = new JObject { ["LabelSeparator"] = raw };
            LabelSeparatorSpacing read = PanelSeparator.Read(panel, raw);
            Assert(read == (raw.Length == 1 ? LabelSeparatorSpacing.Both : LabelSeparatorSpacing.None),
                $"'{raw}' saved before the setting reads as {read}");
            Assert(PanelSeparator.Draw(raw, read) == OldDraw(raw),
                $"'{raw}' saved before the setting draws '{PanelSeparator.Draw(raw, read)}', was '{OldDraw(raw)}'");
        }
    }

    public static void TestAStoredSpacingWinsAndABadOneMigrates() {
        Assert(PanelSeparator.Read(JObject.Parse("""{"LabelSeparator":"|","LabelSeparatorSpacing":2}"""), "|") == LabelSeparatorSpacing.None,
            "a stored None is kept");
        Assert(PanelSeparator.Read(JObject.Parse("""{"LabelSeparator":": ","LabelSeparatorSpacing":1}"""), ": ") == LabelSeparatorSpacing.After,
            "a stored After is kept");
        foreach(string bad in new[] { "7", "-1", "\"x\"", "\"1\"", "1.5", "null", "99999999999999999999" })
            Assert(PanelSeparator.Read(JObject.Parse("""{"LabelSeparator":"|","LabelSeparatorSpacing":""" + bad + "}"), "|") == LabelSeparatorSpacing.Both,
                $"a stored spacing of {bad} reads as if it were missing");
        Assert(PanelSeparator.Read(null, "|") == LabelSeparatorSpacing.Both, "no panel token with the default '|' reads as Both");
        Assert(PanelSeparator.Read(new JObject(), "|") == LabelSeparatorSpacing.Both, "a panel with no separator keeps the default '|' as Both");
        Assert(PanelSeparator.Read(new JValue("x"), "|") == LabelSeparatorSpacing.Both, "a panel token that is not an object never throws");
    }
}
