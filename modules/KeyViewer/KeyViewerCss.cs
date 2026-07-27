#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerStylesheet {
    private readonly CssBucket _keyIdle = new(), _keyActive = new();
    private readonly CssBucket _beforeIdle = new(), _beforeActive = new();
    private readonly CssBucket _afterIdle = new(), _afterActive = new();
    private readonly CssBucket _ctrIdle = new(), _ctrActive = new();
    private readonly CssBucket _graph = new();
    public List<CssFontFace> FontFaces { get; } = new();
    public bool IsEmpty { get; private set; } = true;
    public static KeyViewerStylesheet Parse(string? css) {
        var sheet = new KeyViewerStylesheet();
        if(string.IsNullOrWhiteSpace(css)) { return sheet; }
        foreach((string prelude, string body) in CssReader.Rules(StripComments(css!))) {
            if(prelude.StartsWith("@", StringComparison.Ordinal)) {
                string at = prelude.TrimStart('@').TrimStart();
                if(at.StartsWith("font-face", StringComparison.OrdinalIgnoreCase)) {
                    sheet.AddFontFace(ParseDeclarations(body));
                } else if(at.StartsWith("media", StringComparison.OrdinalIgnoreCase)
                    || at.StartsWith("supports", StringComparison.OrdinalIgnoreCase)) {
                    foreach((string p2, string b2) in CssReader.Rules(body)) {
                        if(!p2.StartsWith("@", StringComparison.Ordinal)) { sheet.AddRule(p2, ParseDeclarations(b2)); }
                    }
                }
                continue;
            }
            sheet.AddRule(prelude, ParseDeclarations(body));
        }
        return sheet;
    }
    private void AddFontFace(Dictionary<string, string> decls) {
        var face = new CssFontFace();
        if(decls.TryGetValue("font-family", out string? fam)) { face.Family = fam.Trim().Trim('"', '\'').Trim(); }
        if(decls.TryGetValue("src", out string? src)) {
            foreach(string part in SplitTopLevel(src, ',')) {
                string url = ExtractUrl(part);
                if(url.Length > 0) { face.Srcs.Add(url); }
            }
        }
        if(face.Family.Length > 0 && face.Srcs.Count > 0) {
            FontFaces.Add(face);
            IsEmpty = false;
        }
    }
    private static string ExtractUrl(string part) {
        int u = part.IndexOf("url(", StringComparison.OrdinalIgnoreCase);
        if(u < 0) { return ""; }
        int lp = u + 3;
        int rp = MatchParen(part, lp);
        if(rp < 0) { return ""; }
        return part.Substring(lp + 1, rp - lp - 1).Trim().Trim('"', '\'').Trim();
    }
    private void AddRule(string selectorList, Dictionary<string, string> decls) {
        if(decls.Count == 0) { return; }
        foreach(string raw in SplitTopLevel(selectorList, ',')) {
            string sel = raw.Trim();
            if(sel.Length == 0) { continue; }
            int pseudo = 0;
            string baseSel = sel;
            int dc = sel.IndexOf("::", StringComparison.Ordinal);
            int sc = dc >= 0 ? dc : sel.IndexOf(':', StringComparison.Ordinal);
            if(sc >= 0) {
                string tail = sel.Substring(sc).ToLowerInvariant();
                if(tail.Contains("before")) {
                    pseudo = 1;
                } else if(tail.Contains("after")) {
                    pseudo = 2;
                } else {
                    continue;
                }
                baseSel = sel.Substring(0, sc);
            }
            string lower = baseSel.ToLowerInvariant();
            bool counter = lower.IndexOf("counter", StringComparison.Ordinal) >= 0;
            bool hasState = lower.IndexOf("data-state", StringComparison.Ordinal) >= 0;
            string[] classes = ExtractClasses(baseSel);
            if(pseudo == 0 && HasGraphVar(decls)) {
                _graph.Add(classes, decls);
                IsEmpty = false;
            }
            if(!counter && !hasState) { continue; }
            int state = -1;
            if(lower.IndexOf("inactive", StringComparison.Ordinal) >= 0) { state = 0; }
            else if(lower.IndexOf("active", StringComparison.Ordinal) >= 0) { state = 1; }
            if(counter) {
                AddTo(_ctrIdle, _ctrActive, state, classes, decls);
            } else if(pseudo == 1) {
                AddTo(_beforeIdle, _beforeActive, state, classes, decls);
            } else if(pseudo == 2) {
                AddTo(_afterIdle, _afterActive, state, classes, decls);
            } else {
                AddTo(_keyIdle, _keyActive, state, classes, decls);
            }
            IsEmpty = false;
        }
    }
    private static void AddTo(CssBucket idle, CssBucket active, int state, string[] classes, Dictionary<string, string> decls) {
        if(state != 1) { idle.Add(classes, decls); }
        if(state != 0) { active.Add(classes, decls); }
    }
    private static string[] ExtractClasses(string selector) {
        List<string>? names = null;
        for(int i = 0; i < selector.Length; i++) {
            if(selector[i] != '.') { continue; }
            int j = i + 1;
            while(j < selector.Length && (char.IsLetterOrDigit(selector[j]) || selector[j] is '-' or '_')) { j++; }
            string name = selector.Substring(i + 1, j - i - 1);
            if(name.Length > 0 && !name.Equals("counter", StringComparison.OrdinalIgnoreCase)) {
                (names ??= new List<string>()).Add(name);
            }
            i = j - 1;
        }
        return names == null ? Array.Empty<string>() : names.ToArray();
    }
    internal static void Overlay(Dictionary<string, string> into, Dictionary<string, string> from) {
        foreach(KeyValuePair<string, string> kv in from) {
            into[kv.Key] = kv.Value;
        }
    }
}
internal static class CssReader {
    public static IEnumerable<(string prelude, string body)> Rules(string css) {
        int i = 0, n = css.Length;
        while(i < n) {
            while(i < n && char.IsWhiteSpace(css[i])) { i++; }
            if(i >= n) { yield break; }
            int start = i;
            int depth = 0;
            int braceOpen = -1;
            while(i < n) {
                char c = css[i];
                if(c == '{') {
                    if(depth == 0) { braceOpen = i; }
                    depth++;
                } else if(c == '}') {
                    depth--;
                    if(depth == 0) { break; }
                } else if(c == ';' && depth == 0) {
                    break;
                }
                i++;
            }
            if(braceOpen < 0) {
                i++;
                continue;
            }
            string prelude = css.Substring(start, braceOpen - start).Trim();
            int bodyStart = braceOpen + 1;
            int bodyEnd = i < n ? i : n;
            string body = css.Substring(bodyStart, Math.Max(0, bodyEnd - bodyStart));
            i++;
            yield return (prelude, body);
        }
    }
}
