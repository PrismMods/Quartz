#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerStylesheet {
    private static void MapKey(Dictionary<string, string> d, CssKeyStyle s) {
        bool clipText = false;
        CssGradient? bgGradient = null;
        foreach(KeyValuePair<string, string> kv in d) {
            string prop = kv.Key;
            string val = kv.Value;
            switch(prop) {
                case "--key-radius":
                case "border-radius":
                    if(TryLen(val, out float r)) { s.Radius = r; }
                    break;
                case "--key-bg":
                case "background-color":
                    if(TryParseColor(val, out CssColor bg)) { s.Bg = bg; }
                    break;
                case "background":
                case "background-image":
                    if(ParseGradient(val) is { } g) { bgGradient = g; }
                    break;
                case "--key-border":
                case "border":
                    ParseBorder(val, s);
                    break;
                case "border-width":
                    if(TryLen(val, out float bw)) { s.BorderWidth = bw; }
                    break;
                case "border-color":
                    if(TryParseColor(val, out CssColor bc)) { s.BorderColor = bc; }
                    break;
                case "--key-text-color":
                case "color":
                    if(TryParseColor(val, out CssColor tc)) { s.TextColor = tc; }
                    break;
                case "background-clip":
                case "-webkit-background-clip":
                    if(val.Trim().Equals("text", StringComparison.OrdinalIgnoreCase)) { clipText = true; }
                    break;
                case "-webkit-text-fill-color":
                    if(val.Trim().Equals("transparent", StringComparison.OrdinalIgnoreCase)) { clipText = true; }
                    break;
                case "font-size":
                    if(TryLen(val, out float fs)) { s.FontSize = fs; }
                    break;
                case "font-weight":
                    s.Bold = IsBold(val);
                    break;
                case "font-family":
                    s.FontFamily = FirstFamily(val);
                    break;
                case "--key-offset-x":
                    if(TryLen(val, out float ox)) { s.OffsetX = ox; }
                    break;
                case "--key-offset-y":
                    if(TryLen(val, out float oy)) { s.OffsetY = oy; }
                    break;
                case "text-shadow":
                    if(ParseShadow(val) is { } ts) { s.TextShadow = ts; }
                    break;
                case "box-shadow":
                    if(ParseShadow(val) is { } bs) { s.BoxShadow = bs; }
                    break;
                case "transform":
                    s.Transform = ParseTransform(val);
                    break;
                case "filter":
                    s.Filter = ParseFilter(val);
                    break;
                case "transition":
                case "transition-duration":
                    if(TryDuration(val, out float tr)) { s.TransitionSeconds = tr; }
                    break;
                case "mix-blend-mode":
                    s.Blend = ParseBlend(val);
                    break;
                case "backdrop-filter":
                case "-webkit-backdrop-filter":
                    if(FilterBlur(val, out float bb)) { s.BackdropBlur = bb; }
                    break;
            }
        }
        ApplyAnimation(d, bgGradient);
        if(clipText && bgGradient != null) {
            bgGradient.ClipText = true;
            s.TextGradient = bgGradient;
        } else {
            s.BgGradient = bgGradient;
        }
    }
    private static void MapCounter(Dictionary<string, string> d, CssCounterStyle s) {
        CssGradient? gradient = null;
        foreach(KeyValuePair<string, string> kv in d) {
            switch(kv.Key) {
                case "--counter-color":
                case "color":
                    if(TryParseColor(kv.Value, out CssColor c)) { s.Color = c; }
                    break;
                case "background":
                case "background-image":
                    if(ParseGradient(kv.Value) is { } g) { gradient = g; }
                    break;
                case "--counter-stroke-color":
                    if(TryParseColor(kv.Value, out CssColor sc)) { s.StrokeColor = sc; }
                    break;
                case "--counter-stroke-width":
                    if(TryLen(kv.Value, out float sw)) { s.StrokeWidth = sw; }
                    break;
                case "font-size":
                    if(TryLen(kv.Value, out float fs)) { s.FontSize = fs; }
                    break;
                case "font-weight":
                    s.Bold = IsBold(kv.Value);
                    break;
                case "text-shadow":
                    if(ParseShadow(kv.Value) is { } ts) { s.TextShadow = ts; }
                    break;
            }
        }
        ApplyAnimation(d, gradient);
        if(gradient != null) {
            gradient.ClipText = true;
            s.Gradient = gradient;
        }
    }
    private static CssLayer? MapLayer(Dictionary<string, string> d) {
        if(d.Count == 0) { return null; }
        var layer = new CssLayer();
        CssGradient? grad = null;
        foreach(KeyValuePair<string, string> kv in d) {
            string val = kv.Value;
            switch(kv.Key) {
                case "background":
                case "background-image":
                    if(ParseGradient(val) is { } g) { grad = g; } else if(TryParseColor(val, out CssColor bc)) { layer.Bg = bc; layer.Has = true; }
                    break;
                case "background-color":
                    if(TryParseColor(val, out CssColor c)) { layer.Bg = c; layer.Has = true; }
                    break;
                case "border-radius":
                    if(TryLen(val, out float r)) { layer.Radius = r; layer.Has = true; }
                    break;
                case "inset":
                    ParseInset(val, layer);
                    break;
                case "top": if(TryLen(val, out float t)) { layer.InsetT = t; layer.Has = true; } break;
                case "right": if(TryLen(val, out float rr)) { layer.InsetR = rr; layer.Has = true; } break;
                case "bottom": if(TryLen(val, out float b)) { layer.InsetB = b; layer.Has = true; } break;
                case "left": if(TryLen(val, out float l)) { layer.InsetL = l; layer.Has = true; } break;
                case "filter":
                    if(FilterBlur(val, out float blur)) { layer.Blur = blur; layer.Has = true; }
                    break;
                case "mix-blend-mode":
                    layer.Blend = ParseBlend(val); layer.Has = true;
                    break;
                case "z-index":
                    if(int.TryParse(val.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) { layer.Z = z; layer.Has = true; }
                    break;
            }
        }
        ApplyAnimation(d, grad);
        if(grad != null) {
            layer.Gradient = grad;
            layer.Has = true;
        }
        return layer.Has ? layer : null;
    }
    private static void ApplyAnimation(Dictionary<string, string> d, CssGradient? gradient) {
        if(gradient == null) { return; }
        if((d.TryGetValue("animation", out string? a) || d.TryGetValue("animation-duration", out a))
            && TryDuration(a, out float seconds) && seconds > 0.01f) {
            gradient.Animated = true;
            gradient.AnimSeconds = seconds;
        }
    }
}
