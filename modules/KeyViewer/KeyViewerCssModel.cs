#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Quartz.Features.KeyViewer;
public readonly struct CssColor {
    public readonly float R, G, B, A;
    public readonly bool Has;
    public CssColor(float r, float g, float b, float a) {
        R = r; G = g; B = b; A = a; Has = true;
    }
    public static readonly CssColor Unset = default;
    public static readonly CssColor Transparent = new(0f, 0f, 0f, 0f);
    public CssColor WithAlpha(float a) => new(R, G, B, a);
}
public sealed class CssGradient {
    public readonly List<CssColor> Stops = new();
    public float AngleDeg = 180f;
    public float AnimSeconds;
    public bool Animated;
    public bool ClipText;
}
public readonly struct CssShadow {
    public readonly bool On;
    public readonly float X, Y, Blur;
    public readonly CssColor Color;
    public CssShadow(float x, float y, float blur, CssColor color) {
        On = true; X = x; Y = y; Blur = blur; Color = color;
    }
}
public enum CssBlend { Normal, Multiply, Screen, Additive, Darken, Lighten }
public sealed class CssTransform {
    public float ScaleX = 1f, ScaleY = 1f, TranslateX, TranslateY, RotateDeg;
    public bool Has;
}
public sealed class CssFilter {
    public float Brightness = 1f, Saturate = 1f, Contrast = 1f, Blur;
    public CssShadow DropShadow;
    public bool Has;
}
public sealed class CssLayer {
    public CssColor Bg = CssColor.Unset;
    public CssGradient? Gradient;
    public float? Radius;
    public float InsetT, InsetR, InsetB, InsetL;
    public float Blur;
    public CssBlend Blend = CssBlend.Normal;
    public int Z;
    public bool Has;
}
public sealed class CssFontFace {
    public string Family = "";
    public readonly List<string> Srcs = new();
}
public sealed class CssKeyStyle {
    public float? Radius;
    public CssColor Bg = CssColor.Unset;
    public CssGradient? BgGradient;
    public float? BorderWidth;
    public CssColor BorderColor = CssColor.Unset;
    public CssColor TextColor = CssColor.Unset;
    public CssGradient? TextGradient;
    public float? FontSize;
    public bool? Bold;
    public string? FontFamily;
    public float? OffsetX, OffsetY;
    public CssShadow TextShadow;
    public CssShadow BoxShadow;
    public CssTransform? Transform;
    public CssFilter? Filter;
    public float? TransitionSeconds;
    public CssBlend Blend = CssBlend.Normal;
    public float? BackdropBlur;
    public CssLayer? Before;
    public CssLayer? After;
    public bool Any =>
        Radius.HasValue || Bg.Has || BgGradient != null || BorderWidth.HasValue
        || BorderColor.Has || TextColor.Has || TextGradient != null
        || FontSize.HasValue || Bold.HasValue || FontFamily != null
        || OffsetX.HasValue || OffsetY.HasValue || TextShadow.On || BoxShadow.On
        || Transform != null || Filter != null || TransitionSeconds.HasValue
        || Blend != CssBlend.Normal || BackdropBlur.HasValue
        || Before != null || After != null;
}
public sealed class CssKeyStyleSet {
    public readonly CssKeyStyle Idle = new();
    public readonly CssKeyStyle Active = new();
    public bool Any => Idle.Any || Active.Any;
}
public sealed class CssCounterStyle {
    public CssColor Color = CssColor.Unset;
    public CssGradient? Gradient;
    public CssColor StrokeColor = CssColor.Unset;
    public float? StrokeWidth;
    public float? FontSize;
    public bool? Bold;
    public CssShadow TextShadow;
    public bool Any =>
        Color.Has || Gradient != null || StrokeColor.Has || StrokeWidth.HasValue
        || FontSize.HasValue || Bold.HasValue || TextShadow.On;
}
public sealed class CssCounterStyleSet {
    public readonly CssCounterStyle Idle = new();
    public readonly CssCounterStyle Active = new();
    public bool Any => Idle.Any || Active.Any;
}
public sealed class CssGraphStyle {
    public CssColor Bg = CssColor.Unset;
    public float? BorderWidth;
    public CssColor BorderColor = CssColor.Unset;
    public float? Radius;
    public CssColor Color = CssColor.Unset;
    public bool Any => Bg.Has || BorderWidth.HasValue || BorderColor.Has || Radius.HasValue || Color.Has;
}
internal sealed class CssBucket {
    public readonly Dictionary<string, string> Global = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<(string[] classes, Dictionary<string, string> decls)> Classes = new();
    public void Add(string[]? classes, Dictionary<string, string> decls) {
        if(classes == null || classes.Length == 0) {
            KeyViewerStylesheet.Overlay(Global, decls);
        } else {
            Classes.Add((classes, decls));
        }
    }
    public Dictionary<string, string> Flatten(HashSet<string> keyClasses) {
        var merged = new Dictionary<string, string>(Global, StringComparer.OrdinalIgnoreCase);
        if(Classes.Count > 0) {
            foreach((string[] classes, Dictionary<string, string> decls) in Classes.OrderBy(c => c.classes.Length)) {
                if(AllPresent(classes, keyClasses)) { KeyViewerStylesheet.Overlay(merged, decls); }
            }
        }
        return merged;
    }
    private static bool AllPresent(string[] classes, HashSet<string> have) {
        for(int i = 0; i < classes.Length; i++) {
            if(!have.Contains(classes[i])) { return false; }
        }
        return true;
    }
}
