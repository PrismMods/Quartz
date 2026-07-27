#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerStylesheet {
    private static IEnumerable<(string name, string args)> Functions(string v) {
        int i = 0, n = v.Length;
        while(i < n) {
            while(i < n && (char.IsWhiteSpace(v[i]) || v[i] == ',')) { i++; }
            int start = i;
            while(i < n && (char.IsLetterOrDigit(v[i]) || v[i] == '-')) { i++; }
            if(i >= n || v[i] != '(') {
                if(i == start) { i++; }
                continue;
            }
            string name = v.Substring(start, i - start);
            int rp = MatchParen(v, i);
            if(rp < 0) { yield break; }
            string args = v.Substring(i + 1, rp - i - 1);
            yield return (name, args);
            i = rp + 1;
        }
    }
    private static List<float> Nums(string args) {
        var list = new List<float>();
        foreach(string tok in SplitTopLevel(args, ',')) {
            foreach(string sub in tok.Split(Space, StringSplitOptions.RemoveEmptyEntries)) {
                if(TryLen(sub, out float f)) { list.Add(f); }
            }
        }
        return list;
    }
    private static CssGradient? ParseGradient(string v) {
        int idx = v.IndexOf("linear-gradient", StringComparison.OrdinalIgnoreCase);
        bool radial = false;
        if(idx < 0) {
            idx = v.IndexOf("radial-gradient", StringComparison.OrdinalIgnoreCase);
            radial = true;
        }
        if(idx < 0) { return null; }
        int lp = v.IndexOf('(', idx);
        if(lp < 0) { return null; }
        int rp = MatchParen(v, lp);
        if(rp < 0) { return null; }
        string inner = v.Substring(lp + 1, rp - lp - 1);
        var grad = new CssGradient();
        bool first = true;
        foreach(string partRaw in SplitTopLevel(inner, ',')) {
            string part = partRaw.Trim();
            if(part.Length == 0) { continue; }
            if(first && !radial && IsDirection(part)) {
                grad.AngleDeg = DirectionToAngle(part);
                first = false;
                continue;
            }
            first = false;
            if(TryParseColor(FirstColorToken(part), out CssColor c)) { grad.Stops.Add(c); }
        }
        return grad.Stops.Count >= 2 ? grad : null;
    }
    private static string FirstColorToken(string part) {
        string t = part.Trim();
        if(t.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) || t.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)) {
            int lp = t.IndexOf('(');
            int rp = lp >= 0 ? MatchParen(t, lp) : -1;
            return rp > 0 ? t.Substring(0, rp + 1) : t;
        }
        int sp = t.IndexOf(' ');
        return sp > 0 ? t.Substring(0, sp) : t;
    }
    private static bool IsDirection(string p) {
        string t = p.Trim();
        if(t.StartsWith("to ", StringComparison.OrdinalIgnoreCase)) { return true; }
        return t.EndsWith("deg", StringComparison.OrdinalIgnoreCase)
            || t.EndsWith("turn", StringComparison.OrdinalIgnoreCase)
            || t.EndsWith("rad", StringComparison.OrdinalIgnoreCase);
    }
    private static float DirectionToAngle(string p) {
        string t = p.Trim();
        if(t.StartsWith("to ", StringComparison.OrdinalIgnoreCase)) {
            return t.Substring(3).Trim().ToLowerInvariant() switch {
                "top" => 0f,
                "right" => 90f,
                "bottom" => 180f,
                "left" => 270f,
                "top right" or "right top" => 45f,
                "bottom right" or "right bottom" => 135f,
                "bottom left" or "left bottom" => 225f,
                "top left" or "left top" => 315f,
                _ => 180f,
            };
        }
        if(t.EndsWith("deg", StringComparison.OrdinalIgnoreCase) && TryLen(t, out float deg)) { return deg; }
        if(t.EndsWith("turn", StringComparison.OrdinalIgnoreCase) && TryLen(t, out float turn)) { return turn * 360f; }
        if(t.EndsWith("rad", StringComparison.OrdinalIgnoreCase) && TryLen(t, out float rad)) { return rad * 57.29578f; }
        return 180f;
    }
    private static bool LooksLikeColor(string p) {
        string t = p.Trim();
        return t.Length > 0
            && (t[0] == '#'
                || t.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)
                || NamedColor(t, out _));
    }
    internal static Dictionary<string, string> ParseDeclarations(string body) {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach(string declRaw in SplitTopLevel(body, ';')) {
            string decl = declRaw.Trim();
            if(decl.Length == 0) { continue; }
            int colon = IndexOfTopLevel(decl, ':');
            if(colon <= 0) { continue; }
            string name = decl.Substring(0, colon).Trim().ToLowerInvariant();
            string value = decl.Substring(colon + 1).Trim();
            int bang = value.IndexOf('!');
            while(bang >= 0) {
                if(value.Substring(bang + 1).TrimStart().StartsWith("important", StringComparison.OrdinalIgnoreCase)) {
                    value = value.Substring(0, bang).Trim();
                    break;
                }
                bang = value.IndexOf('!', bang + 1);
            }
            if(name.Length > 0 && value.Length > 0) { d[name] = value; }
        }
        return d;
    }
    internal static string StripComments(string css) {
        var sb = new StringBuilder(css.Length);
        for(int i = 0; i < css.Length; i++) {
            if(i + 1 < css.Length && css[i] == '/' && css[i + 1] == '*') {
                int end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if(end < 0) { break; }
                i = end + 1;
                continue;
            }
            sb.Append(css[i]);
        }
        return sb.ToString();
    }
    internal static List<string> SplitTopLevel(string s, char sep) {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for(int i = 0; i < s.Length; i++) {
            char c = s[i];
            if(c is '(' or '[') {
                depth++;
            } else if(c is ')' or ']') {
                if(depth > 0) { depth--; }
            } else if(c == sep && depth == 0) {
                parts.Add(s.Substring(start, i - start));
                start = i + 1;
            }
        }
        parts.Add(s.Substring(start));
        return parts;
    }
    private static int IndexOfTopLevel(string s, char target) {
        int depth = 0;
        for(int i = 0; i < s.Length; i++) {
            char c = s[i];
            if(c is '(' or '[') { depth++; } else if(c is ')' or ']') { if(depth > 0) { depth--; } } else if(c == target && depth == 0) {
                return i;
            }
        }
        return -1;
    }
    private static int MatchParen(string s, int open) {
        int depth = 0;
        for(int i = open; i < s.Length; i++) {
            if(s[i] == '(') { depth++; } else if(s[i] == ')') {
                depth--;
                if(depth == 0) { return i; }
            }
        }
        return -1;
    }
}
