namespace Quartz.UI.Search;
/// <summary>One searchable string, normalized once: whole with spaces dropped ("Key Viewer" → "keyviewer"), and per word.</summary>
public sealed class SearchText {
    public readonly string Joined;
    public readonly string[] Words;
    public SearchText(string raw, Func<string, string> norm) {
        raw ??= "";
        Joined = norm(raw);
        List<string> words = [];
        int start = -1;
        for(int i = 0; i <= raw.Length; i++) {
            bool inWord = i < raw.Length && char.IsLetterOrDigit(raw[i]);
            if(inWord && start < 0) start = i;
            else if(!inWord && start >= 0) {
                string w = norm(raw[start..i]);
                if(w.Length > 0) words.Add(w);
                start = -1;
            }
        }
        Words = [.. words];
    }
}
/// <summary>
/// Ranks a settings row against a query. Every query word has to land somewhere — the row's label, the section or
/// page it sits in, or its tooltip — so "key rain" finds "Note Rain" on the Key Viewer page. Fuzzy (typos) is a
/// second pass, run only when the exact one finds nothing, so typo matches never crowd out real ones.
/// </summary>
public static class SearchMatcher {
    // What people type for what the menus call it. Normalized keys; a query word also answers to its aliases.
    private static readonly Dictionary<string, string[]> Aliases = new() {
        ["kv"] = ["keyviewer"],
        ["colour"] = ["color"],
        ["judgment"] = ["judgement"],
        ["size"] = ["scale"],
        ["scale"] = ["size"],
        ["transparency"] = ["opacity"],
        ["alpha"] = ["opacity"],
    };
    public static string[][] Tokens(string query, Func<string, string> norm) {
        string[] words = new SearchText(query, norm).Words;
        string[][] tokens = new string[words.Length][];
        for(int i = 0; i < words.Length; i++)
            tokens[i] = Aliases.TryGetValue(words[i], out string[] alt) ? [words[i], .. alt] : [words[i]];
        return tokens;
    }
    /// <summary>0 when some query word lands nowhere. <paramref name="phrase"/> is the whole query normalized.</summary>
    public static int Score(
        string[][] tokens, string phrase, SearchText[] label, SearchText[] context, SearchText detail, bool fuzzy
    ) {
        if(tokens.Length == 0) return 0;
        int total = 0;
        foreach(string[] alts in tokens) {
            int best = 0;
            foreach(string tok in alts) {
                best = Math.Max(best, Best(tok, label, fuzzy));
                best = Math.Max(best, Best(tok, context, fuzzy) / 2);
                if(detail != null && tok.Length >= 3 && detail.Joined.Contains(tok)) best = Math.Max(best, 10);
            }
            if(best == 0) return 0;
            total += best;
        }
        int bonus = 0;
        if(phrase.Length > 0)
            foreach(SearchText l in label) {
                if(l.Joined == phrase) bonus = Math.Max(bonus, 200);
                else if(l.Joined.StartsWith(phrase, StringComparison.Ordinal)) bonus = Math.Max(bonus, 100);
                else if(l.Joined.Contains(phrase)) bonus = Math.Max(bonus, 50);
            }
        return total + bonus;
    }
    private static int Best(string tok, SearchText[] texts, bool fuzzy) {
        int best = 0;
        foreach(SearchText t in texts) best = Math.Max(best, Match(tok, t, fuzzy));
        return best;
    }
    private static int Match(string tok, SearchText t, bool fuzzy) {
        int best = 0;
        foreach(string w in t.Words) {
            if(w == tok) return 100;
            if(w.StartsWith(tok, StringComparison.Ordinal)) best = 80;
        }
        if(best == 0 && t.Joined.Contains(tok)) best = 60;
        if(best == 0 && fuzzy && WithinTypos(tok, t.Joined)) best = 30;
        return best;
    }
    // None under 4 letters (short words are all one edit apart), then 1, then 2.
    private static int AllowedTypos(int length) => length < 4 ? 0 : length < 7 ? 1 : 2;
    /// <summary>Some stretch of <paramref name="text"/> is within the allowed edits of <paramref name="tok"/> (insert, delete, substitute, swap neighbours).</summary>
    internal static bool WithinTypos(string tok, string text) {
        int k = AllowedTypos(tok.Length);
        if(k == 0 || text.Length == 0) return false;
        int n = text.Length;
        // Row 0 is all zeros, so the match may start anywhere in text.
        int[] prev2 = new int[n + 1], prev = new int[n + 1], cur = new int[n + 1];
        for(int i = 1; i <= tok.Length; i++) {
            cur[0] = i;
            for(int j = 1; j <= n; j++) {
                int v = Math.Min(Math.Min(prev[j], cur[j - 1]) + 1, prev[j - 1] + (tok[i - 1] == text[j - 1] ? 0 : 1));
                if(i > 1 && j > 1 && tok[i - 1] == text[j - 2] && tok[i - 2] == text[j - 1])
                    v = Math.Min(v, prev2[j - 2] + 1);
                cur[j] = v;
            }
            (prev2, prev, cur) = (prev, cur, prev2);
        }
        for(int j = 0; j <= n; j++)
            if(prev[j] <= k) return true;
        return false;
    }
}
