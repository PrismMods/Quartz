#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerStylesheet {
    private static readonly char[] Space = { ' ', '\t', '\n', '\r' };
    private static string? FirstFamily(string v) {
        foreach(string part in SplitTopLevel(v, ',')) {
            string fam = part.Trim().Trim('"', '\'').Trim();
            if(fam.Length > 0
                && !fam.Equals("sans-serif", StringComparison.OrdinalIgnoreCase)
                && !fam.Equals("serif", StringComparison.OrdinalIgnoreCase)
                && !fam.Equals("monospace", StringComparison.OrdinalIgnoreCase)) {
                return fam;
            }
        }
        return null;
    }
    private static bool IsBold(string v) {
        string t = v.Trim();
        if(t.Equals("bold", StringComparison.OrdinalIgnoreCase) || t.Equals("bolder", StringComparison.OrdinalIgnoreCase)) { return true; }
        return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) && w >= 600;
    }
    private static bool TryLen(string v, out float px) {
        px = 0f;
        string t = v.Trim();
        int i = 0;
        if(i < t.Length && (t[i] == '+' || t[i] == '-')) { i++; }
        int start = i;
        bool dot = false;
        while(i < t.Length && (char.IsDigit(t[i]) || (t[i] == '.' && !dot))) {
            if(t[i] == '.') { dot = true; }
            i++;
        }
        if(i == start) { return false; }
        string num = (t.Length > 0 && t[0] == '-' ? "-" : "") + t.Substring(start, i - start);
        return float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out px);
    }
    private static bool TryDuration(string v, out float seconds) {
        seconds = 0f;
        foreach(string tok in v.Split(Space, StringSplitOptions.RemoveEmptyEntries)) {
            string t = tok.Trim();
            if(t.EndsWith("ms", StringComparison.OrdinalIgnoreCase)) {
                if(float.TryParse(t[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ms)) {
                    seconds = ms / 1000f; return true;
                }
            } else if(t.EndsWith("s", StringComparison.OrdinalIgnoreCase)) {
                if(float.TryParse(t[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s)) {
                    seconds = s; return true;
                }
            }
        }
        return false;
    }
    private static void ParseBorder(string v, CssKeyStyle s) => ParseBorderCore(v, ref s.BorderWidth, ref s.BorderColor);
    private static void ParseBorderCore(string v, ref float? width, ref CssColor color) {
        string t = v.Trim();
        if(t.Equals("none", StringComparison.OrdinalIgnoreCase) || t.Length == 0) {
            width = 0f;
            return;
        }
        bool gotWidth = false;
        foreach(string tok in SplitTopLevel(t, ' ')) {
            string p = tok.Trim();
            if(p.Length == 0 || p.Equals("solid", StringComparison.OrdinalIgnoreCase)
                || p.Equals("dashed", StringComparison.OrdinalIgnoreCase)
                || p.Equals("dotted", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if(!gotWidth && TryLen(p, out float w) && !LooksLikeColor(p)) {
                width = w;
                gotWidth = true;
            } else if(TryParseColor(p, out CssColor c)) { color = c; }
        }
    }
    private static void ParseInset(string v, CssLayer layer) {
        var vals = new List<float>();
        foreach(string tok in v.Split(Space, StringSplitOptions.RemoveEmptyEntries)) {
            if(TryLen(tok, out float n)) { vals.Add(n); }
        }
        if(vals.Count == 0) { return; }
        float top = vals[0];
        float right = vals.Count >= 2 ? vals[1] : vals[0];
        float bottom = vals.Count >= 3 ? vals[2] : vals[0];
        float left = vals.Count >= 4 ? vals[3] : (vals.Count >= 2 ? vals[1] : vals[0]);
        layer.InsetT = top; layer.InsetR = right; layer.InsetB = bottom; layer.InsetL = left;
        layer.Has = true;
    }
    private static CssShadow? ParseShadow(string v) {
        string t = v.Trim();
        if(t.Equals("none", StringComparison.OrdinalIgnoreCase) || t.Length == 0) { return null; }
        CssShadow? best = null;
        foreach(string layer in SplitTopLevel(t, ',')) {
            float x = 0f, y = 0f, blur = 0f;
            int lenIdx = 0;
            CssColor color = CssColor.Unset;
            foreach(string tok in SplitTopLevel(layer.Trim(), ' ')) {
                string p = tok.Trim();
                if(p.Length == 0 || p.Equals("inset", StringComparison.OrdinalIgnoreCase)) { continue; }
                if(LooksLikeColor(p)) {
                    if(TryParseColor(p, out CssColor c)) { color = c; }
                    continue;
                }
                if(TryLen(p, out float len)) {
                    switch(lenIdx) {
                        case 0: x = len; break;
                        case 1: y = len; break;
                        case 2: blur = len; break;
                    }
                    lenIdx++;
                }
            }
            if(lenIdx == 0) { continue; }
            var shadow = new CssShadow(x, y, blur, color.Has ? color : new CssColor(0f, 0f, 0f, 1f));
            if(best == null || blur > best.Value.Blur) { best = shadow; }
        }
        return best;
    }
    private static CssTransform? ParseTransform(string v) {
        var t = new CssTransform();
        foreach((string name, string args) in Functions(v)) {
            List<float> nums = Nums(args);
            switch(name.ToLowerInvariant()) {
                case "scale":
                    if(nums.Count >= 1) { t.ScaleX = nums[0]; t.ScaleY = nums.Count >= 2 ? nums[1] : nums[0]; t.Has = true; }
                    break;
                case "scalex": if(nums.Count >= 1) { t.ScaleX = nums[0]; t.Has = true; } break;
                case "scaley": if(nums.Count >= 1) { t.ScaleY = nums[0]; t.Has = true; } break;
                case "translate":
                    if(nums.Count >= 1) { t.TranslateX = nums[0]; t.TranslateY = nums.Count >= 2 ? nums[1] : 0f; t.Has = true; }
                    break;
                case "translatex": if(nums.Count >= 1) { t.TranslateX = nums[0]; t.Has = true; } break;
                case "translatey": if(nums.Count >= 1) { t.TranslateY = nums[0]; t.Has = true; } break;
                case "rotate": if(nums.Count >= 1) { t.RotateDeg = nums[0]; t.Has = true; } break;
            }
        }
        return t.Has ? t : null;
    }
    private static CssFilter? ParseFilter(string v) {
        var f = new CssFilter();
        foreach((string name, string args) in Functions(v)) {
            switch(name.ToLowerInvariant()) {
                case "brightness": if(TryAmount(args, out float br)) { f.Brightness = br; f.Has = true; } break;
                case "saturate": if(TryAmount(args, out float sa)) { f.Saturate = sa; f.Has = true; } break;
                case "contrast": if(TryAmount(args, out float co)) { f.Contrast = co; f.Has = true; } break;
                case "blur": if(TryLen(args, out float bl)) { f.Blur = bl; f.Has = true; } break;
                case "drop-shadow": if(ParseShadow(args) is { } ds) { f.DropShadow = ds; f.Has = true; } break;
            }
        }
        return f.Has ? f : null;
    }
    private static bool FilterBlur(string v, out float px) {
        px = 0f;
        foreach((string name, string args) in Functions(v)) {
            if(name.Equals("blur", StringComparison.OrdinalIgnoreCase) && TryLen(args, out px)) { return true; }
        }
        return false;
    }
    private static bool TryAmount(string v, out float amount) {
        string t = v.Trim();
        if(t.EndsWith("%", StringComparison.Ordinal)) {
            if(float.TryParse(t.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float pct)) {
                amount = pct / 100f; return true;
            }
        }
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
    }
    private static CssBlend ParseBlend(string v) => v.Trim().ToLowerInvariant() switch {
        "multiply" => CssBlend.Multiply,
        "screen" => CssBlend.Screen,
        "plus-lighter" or "lighter" or "add" or "additive" => CssBlend.Additive,
        "darken" => CssBlend.Darken,
        "lighten" => CssBlend.Lighten,
        _ => CssBlend.Normal,
    };
}
