using System.Globalization;
using UnityEngine;
namespace Quartz.Features.KeyViewer.Js;
internal readonly struct KvJsUnit {
    public readonly float Px;
    public readonly float Pct;
    public readonly bool Has;
    public KvJsUnit(float px, float pct) {
        Px = px;
        Pct = pct;
        Has = true;
    }
    public float Resolve(float basis) => Px + Pct / 100f * basis;
    public static readonly KvJsUnit None = default;
}
internal sealed class KvJsStyle {
    public const int DispBlock = 0, DispFlex = 1, DispGrid = 2, DispContents = 3, DispNone = 4;
    public const int AlignStretch = 0, AlignStart = 1, AlignCenter = 2, AlignEnd = 3;
    public const int JustifyStart = 0, JustifyCenter = 1, JustifyEnd = 2, JustifyBetween = 3;
    public int Display = DispBlock;
    public bool Row = true;
    public int AlignItems = AlignStretch;
    public int JustifyContent = JustifyStart;
    public float RowGap, ColGap;
    public int GridColumns = 1;
    public float FlexGrow;
    public KvJsUnit Width, Height, MinWidth, MinHeight;
    public KvJsUnit Left, Top, Right, Bottom;
    public bool Absolute;
    public float PadT, PadR, PadB, PadL;
    public float MarT, MarR, MarB, MarL;
    public Color? Bg;
    public Color? BorderColor;
    public float BorderWidth;
    public float Radius;
    public float Opacity = 1f;
    public Color? TextColor;
    public float FontSize;
    public bool Bold;
    public int TextAlign = -1;
    public static readonly KvJsStyle Empty = new();
    private static readonly Dictionary<string, KvJsStyle> Cache = new(StringComparer.Ordinal);
    public static KvJsStyle Parse(string css) {
        if(string.IsNullOrWhiteSpace(css)) return Empty;
        if(Cache.TryGetValue(css, out KvJsStyle cached)) return cached;
        KvJsStyle style = new();
        foreach(string decl in css.Split(';')) {
            int colon = decl.IndexOf(':');
            if(colon <= 0) continue;
            string prop = decl.Substring(0, colon).Trim().ToLowerInvariant();
            string value = decl.Substring(colon + 1).Trim();
            if(value.Length == 0) continue;
            try {
                Apply(style, prop, value);
            } catch(Exception e) { Quartz.Core.Diag.Ignore(e); }
        }
        if(Cache.Count > 1024) Cache.Clear();
        Cache[css] = style;
        return style;
    }
    private static void Apply(KvJsStyle s, string prop, string value) {
        switch(prop) {
            case "display":
                s.Display = value switch {
                    "flex" or "inline-flex" => DispFlex,
                    "grid" or "inline-grid" => DispGrid,
                    "contents" => DispContents,
                    "none" => DispNone,
                    _ => DispBlock,
                };
                break;
            case "flex-direction":
                s.Row = !value.StartsWith("column", StringComparison.Ordinal);
                break;
            case "align-items":
                s.AlignItems = value switch {
                    "flex-start" or "start" => AlignStart,
                    "center" => AlignCenter,
                    "flex-end" or "end" => AlignEnd,
                    _ => AlignStretch,
                };
                break;
            case "justify-content":
                s.JustifyContent = value switch {
                    "center" => JustifyCenter,
                    "flex-end" or "end" => JustifyEnd,
                    "space-between" or "space-around" or "space-evenly" => JustifyBetween,
                    _ => JustifyStart,
                };
                break;
            case "gap": {
                string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                float first = Len(parts[0]);
                s.RowGap = first;
                s.ColGap = parts.Length > 1 ? Len(parts[1]) : first;
                break;
            }
            case "row-gap":
                s.RowGap = Len(value);
                break;
            case "column-gap":
                s.ColGap = Len(value);
                break;
            case "grid-template-columns":
                s.GridColumns = Math.Max(1, SplitTopLevel(value).Count(static token => token.Length > 0));
                break;
            case "flex": {
                string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if(float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float grow)) s.FlexGrow = grow;
                break;
            }
            case "flex-grow":
                if(float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float g)) s.FlexGrow = g;
                break;
            case "width":
                s.Width = Unit(value);
                break;
            case "height":
                s.Height = Unit(value);
                break;
            case "min-width":
                s.MinWidth = Unit(value);
                break;
            case "min-height":
                s.MinHeight = Unit(value);
                break;
            case "position":
                s.Absolute = value is "absolute" or "fixed";
                break;
            case "left":
                s.Left = Unit(value);
                break;
            case "top":
                s.Top = Unit(value);
                break;
            case "right":
                s.Right = Unit(value);
                break;
            case "bottom":
                s.Bottom = Unit(value);
                break;
            case "padding": {
                (s.PadT, s.PadR, s.PadB, s.PadL) = Box(value);
                break;
            }
            case "padding-top": s.PadT = Len(value); break;
            case "padding-right": s.PadR = Len(value); break;
            case "padding-bottom": s.PadB = Len(value); break;
            case "padding-left": s.PadL = Len(value); break;
            case "margin": {
                (s.MarT, s.MarR, s.MarB, s.MarL) = Box(value);
                break;
            }
            case "margin-top": s.MarT = Len(value); break;
            case "margin-right": s.MarR = Len(value); break;
            case "margin-bottom": s.MarB = Len(value); break;
            case "margin-left": s.MarL = Len(value); break;
            case "background" or "background-color": {
                if(KeyViewerStylesheet.TryParseColor(FirstColorToken(value), out CssColor c)) s.Bg = new Color(c.R, c.G, c.B, c.A);
                break;
            }
            case "border": {
                ParseBorder(s, value);
                break;
            }
            case "border-color": {
                if(KeyViewerStylesheet.TryParseColor(value, out CssColor c)) s.BorderColor = new Color(c.R, c.G, c.B, c.A);
                break;
            }
            case "border-width":
                s.BorderWidth = Len(value);
                break;
            case "border-radius":
                s.Radius = Len(value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
                break;
            case "opacity":
                if(float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float o)) s.Opacity = Mathf.Clamp01(o);
                break;
            case "color": {
                if(KeyViewerStylesheet.TryParseColor(value, out CssColor c)) s.TextColor = new Color(c.R, c.G, c.B, c.A);
                break;
            }
            case "font-size":
                s.FontSize = Len(value);
                break;
            case "font-weight":
                s.Bold = value == "bold" || value == "bolder"
                    || (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float w) && w >= 600f);
                break;
            case "text-align":
                s.TextAlign = value switch {
                    "center" => 1,
                    "right" or "end" => 2,
                    _ => 0,
                };
                break;
            default:
                break;
        }
    }
    private static void ParseBorder(KvJsStyle s, string value) {
        foreach(string raw in SplitTopLevel(value)) {
            string token = raw.Trim();
            if(token.Length == 0) continue;
            if(char.IsDigit(token[0]) || token[0] == '.') {
                s.BorderWidth = Len(token);
                continue;
            }
            if(token is "solid" or "dashed" or "dotted" or "none") continue;
            if(KeyViewerStylesheet.TryParseColor(token, out CssColor c)) s.BorderColor = new Color(c.R, c.G, c.B, c.A);
        }
    }
    private static string FirstColorToken(string value) {
        foreach(string raw in SplitTopLevel(value)) {
            string token = raw.Trim();
            if(token.Length == 0) continue;
            if(KeyViewerStylesheet.TryParseColor(token, out _)) return token;
        }
        return value;
    }
    private static IEnumerable<string> SplitTopLevel(string value) {
        int depth = 0, start = 0;
        for(int i = 0; i < value.Length; i++) {
            char c = value[i];
            if(c == '(') depth++;
            else if(c == ')') depth--;
            else if(c == ' ' && depth == 0) {
                yield return value.Substring(start, i - start);
                start = i + 1;
            }
        }
        if(start < value.Length) yield return value.Substring(start);
    }
    private static (float t, float r, float b, float l) Box(string value) {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        float a = parts.Length > 0 ? Len(parts[0]) : 0f;
        float bValue = parts.Length > 1 ? Len(parts[1]) : a;
        float c = parts.Length > 2 ? Len(parts[2]) : a;
        float d = parts.Length > 3 ? Len(parts[3]) : bValue;
        return (a, bValue, c, d);
    }
    private static float Len(string token) {
        string t = token.Trim();
        if(t.EndsWith("px", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 2);
        else if(t.EndsWith("em", StringComparison.OrdinalIgnoreCase)) {
            return float.TryParse(t.Substring(0, t.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out float em) ? em * 16f : 0f;
        }
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }
    internal static KvJsUnit Unit(string token) {
        string t = token.Trim();
        if(t.Length == 0 || t == "auto") return KvJsUnit.None;
        if(t.StartsWith("calc(", StringComparison.OrdinalIgnoreCase) && t.EndsWith(")", StringComparison.Ordinal)) {
            string inner = t.Substring(5, t.Length - 6);
            float px = 0f, pct = 0f;
            int sign = 1;
            foreach(string raw in inner.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
                if(raw == "+") { sign = 1; continue; }
                if(raw == "-") { sign = -1; continue; }
                if(raw.EndsWith("%", StringComparison.Ordinal)) {
                    if(float.TryParse(raw.Substring(0, raw.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float p)) pct += sign * p;
                } else {
                    px += sign * Len(raw);
                }
                sign = 1;
            }
            return new KvJsUnit(px, pct);
        }
        if(t.EndsWith("%", StringComparison.Ordinal)) {
            return float.TryParse(t.Substring(0, t.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float pct)
                ? new KvJsUnit(0f, pct)
                : KvJsUnit.None;
        }
        return new KvJsUnit(Len(t), 0f);
    }
}
