using Quartz.UI.Search;
using Quartz.Utility;
using static Asserts;
static class SearchMatcherTests {
    private sealed record Row(string Label, string Section, string Page, string Tip = null);
    private static readonly Row[] Rows = [
        // First on purpose, and as long as "Note Rain": shares both words but not the phrase, so only the phrase bonus ranks it lower.
        new("Rain Note", "Rain", "Overlay"),
        new("Note Rain", "Key Viewer", "Key Viewer"),
        new("Reverse Rain", "Key Viewer", "Key Viewer"),
        new("Scale", "Key Viewer", "Key Viewer"),
        new("Key Viewer", null, "Key Viewer"),
        new("Judgement Popup Font", "Judgement", "Visuals"),
        new("Accent Color", "Theme", "Settings"),
        new("This Tabub Is Mine", "Key Viewer", "Key Viewer", "Past a point in the level, note rain stops and a picture drops over the key viewer, so nobody watching can read your tapping pattern."),
        new("Rain Offset Y", "Rain", "Overlay"),
    ];
    private static List<string> Find(string query) {
        string[][] tokens = SearchMatcher.Tokens(query, StringUtils.Normalize);
        string phrase = StringUtils.Normalize(query);
        List<(string Label, int Score)> hits = [];
        foreach(bool fuzzy in new[] { false, true }) {
            foreach(Row r in Rows) {
                SearchText[] label = [new(r.Label, StringUtils.Normalize)];
                SearchText[] context = [new(r.Section ?? "", StringUtils.Normalize), new(r.Page, StringUtils.Normalize)];
                SearchText tip = r.Tip == null ? null : new SearchText(r.Tip, StringUtils.Normalize);
                int score = SearchMatcher.Score(tokens, phrase, label, context, tip, fuzzy);
                if(score > 0) hits.Add((r.Label, score));
            }
            if(hits.Count > 0) break;
        }
        // Same tie-break as PageSearch: shorter label first.
        return [.. hits.OrderByDescending(h => h.Score).ThenBy(h => h.Label.Length).Select(h => h.Label)];
    }
    public static void TestWordsLandAcrossLabelSectionAndPage() {
        Assert(Find("Key Rain").FirstOrDefault() == "Note Rain", "'Key Rain' finds Note Rain on the Key Viewer page");
        Assert(Find("Keyviewer Rain").FirstOrDefault() == "Note Rain", "'Keyviewer Rain' ignores the missing space");
        Assert(Find("kv scale").FirstOrDefault() == "Scale", "the kv alias reaches Key Viewer");
        Assert(Find("key viewer size").Contains("Scale"), "size answers to scale");
        Assert(Find("note rain")[0] == "Note Rain", "an exact label outranks rows that only share words");
        Assert(!Find("Key Rain").Contains("Rain Offset Y"), "every word must land: 'key' is nowhere near Rain Offset Y");
    }
    public static void TestTyposOnlyWhenNothingExactMatches() {
        Assert(Find("keyveiwer rain").FirstOrDefault() == "Note Rain", "a swapped pair is one typo");
        Assert(Find("judgment font").FirstOrDefault() == "Judgement Popup Font", "the American spelling still finds Judgement");
        Assert(Find("acent colr").FirstOrDefault() == "Accent Color", "one typo per short word");
        Assert(Find("rain").All(l => l.Contains("Rain") || l == "This Tabub Is Mine"), "an exact hit keeps typo matches out");
        Assert(Find("xyzzy").Count == 0, "nonsense finds nothing");
        Assert(!SearchMatcher.WithinTypos("cat", "car"), "three-letter words get no typo allowance");
    }
    public static void TestTooltipWordsAreALastResort() {
        List<string> hits = Find("pattern picture");
        Assert(hits.Count == 1 && hits[0] == "This Tabub Is Mine", "tooltip text is searchable");
        Assert(Find("rain")[^1] == "This Tabub Is Mine", "a tooltip-only hit ranks below every label hit");
    }
}
