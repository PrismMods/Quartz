#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerStylesheet {
    public static bool TryParseColor(string v, out CssColor color) {
        color = CssColor.Unset;
        string s = v.Trim();
        if(s.Length == 0) { return false; }
        if(s.Equals("transparent", StringComparison.OrdinalIgnoreCase)) {
            color = CssColor.Transparent;
            return true;
        }
        if(NamedColor(s, out CssColor named)) {
            color = named;
            return true;
        }
        if(s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) {
            int lp = s.IndexOf('(');
            int rp = lp >= 0 ? s.IndexOf(')', lp) : -1;
            if(lp < 0 || rp < 0) { return false; }
            string[] parts = s.Substring(lp + 1, rp - lp - 1).Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length < 3) { return false; }
            color = new CssColor(Comp(parts[0], 255f), Comp(parts[1], 255f), Comp(parts[2], 255f),
                parts.Length >= 4 ? Alpha(parts[3]) : 1f);
            return true;
        }
        if(s.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)) {
            int lp = s.IndexOf('(');
            int rp = lp >= 0 ? s.IndexOf(')', lp) : -1;
            if(lp < 0 || rp < 0) { return false; }
            string[] parts = s.Substring(lp + 1, rp - lp - 1).Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length < 3) { return false; }
            float h = float.TryParse(parts[0].Trim().Replace("deg", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float hv) ? hv : 0f;
            float sl = Pct(parts[1]);
            float ll = Pct(parts[2]);
            float a = parts.Length >= 4 ? Alpha(parts[3]) : 1f;
            HslToRgb(h, sl, ll, out float r, out float g, out float b);
            color = new CssColor(r, g, b, a);
            return true;
        }
        string h2 = s.TrimStart('#');
        try {
            switch(h2.Length) {
                case 3:
                case 4:
                    color = new CssColor(Hex(h2[0]) / 15f, Hex(h2[1]) / 15f, Hex(h2[2]) / 15f,
                        h2.Length == 4 ? Hex(h2[3]) / 15f : 1f);
                    return true;
                case 6:
                case 8:
                    color = new CssColor(
                        Convert.ToInt32(h2.Substring(0, 2), 16) / 255f,
                        Convert.ToInt32(h2.Substring(2, 2), 16) / 255f,
                        Convert.ToInt32(h2.Substring(4, 2), 16) / 255f,
                        h2.Length == 8 ? Convert.ToInt32(h2.Substring(6, 2), 16) / 255f : 1f);
                    return true;
            }
        } catch {
            return false;
        }
        return false;
    }
    private static void HslToRgb(float h, float s, float l, out float r, out float g, out float b) {
        h = ((h % 360f) + 360f) % 360f / 360f;
        if(s <= 0f) {
            r = g = b = l;
            return;
        }
        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;
        r = HueToRgb(p, q, h + 1f / 3f);
        g = HueToRgb(p, q, h);
        b = HueToRgb(p, q, h - 1f / 3f);
    }
    private static float HueToRgb(float p, float q, float t) {
        if(t < 0f) { t += 1f; }
        if(t > 1f) { t -= 1f; }
        if(t < 1f / 6f) { return p + (q - p) * 6f * t; }
        if(t < 1f / 2f) { return q; }
        if(t < 2f / 3f) { return p + (q - p) * (2f / 3f - t) * 6f; }
        return p;
    }
    private static int Hex(char c) => Convert.ToInt32(c.ToString(), 16);
    internal static float Comp(string s, float scale) {
        string t = s.Trim();
        if(t.EndsWith("%", StringComparison.Ordinal)) {
            return float.TryParse(t.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float pct) ? Clamp01(pct / 100f) : 1f;
        }
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? Clamp01(v / scale) : 1f;
    }
    private static float Pct(string s) {
        string t = s.Trim().TrimEnd('%').Trim();
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? Clamp01(v / 100f) : 0f;
    }
    internal static float Alpha(string s) {
        string t = s.Trim();
        if(t.EndsWith("%", StringComparison.Ordinal)) {
            return float.TryParse(t.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float pct) ? Clamp01(pct / 100f) : 1f;
        }
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? Clamp01(v <= 1f ? v : v / 255f) : 1f;
    }
    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    private static bool NamedColor(string name, out CssColor color) {
        switch(name.Trim().ToLowerInvariant()) {
            case "white": color = new CssColor(1f, 1f, 1f, 1f); return true;
            case "black": color = new CssColor(0f, 0f, 0f, 1f); return true;
            case "red": color = new CssColor(1f, 0f, 0f, 1f); return true;
            case "green": color = new CssColor(0f, 0.5f, 0f, 1f); return true;
            case "lime": color = new CssColor(0f, 1f, 0f, 1f); return true;
            case "blue": color = new CssColor(0f, 0f, 1f, 1f); return true;
            case "yellow": color = new CssColor(1f, 1f, 0f, 1f); return true;
            case "cyan": case "aqua": color = new CssColor(0f, 1f, 1f, 1f); return true;
            case "magenta": case "fuchsia": color = new CssColor(1f, 0f, 1f, 1f); return true;
            case "gray": case "grey": color = new CssColor(0.5f, 0.5f, 0.5f, 1f); return true;
            case "silver": color = new CssColor(0.75f, 0.75f, 0.75f, 1f); return true;
            case "orange": color = new CssColor(1f, 0.647f, 0f, 1f); return true;
            case "pink": color = new CssColor(1f, 0.753f, 0.796f, 1f); return true;
            case "purple": color = new CssColor(0.5f, 0f, 0.5f, 1f); return true;
            default: color = CssColor.Unset; return false;
        }
    }
}
