using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private readonly struct InlineStyleSnapshot {
        private readonly Color bg, activeBg, outline, activeOutline, text, activeText;
        private readonly Color counterText, activeCounterText, counterStroke, activeCounterStroke;
        private readonly float borderRadius, boxBorderWidth;
        private readonly int fontSize, counterFontSize;
        private readonly Vector2 activeScale;
        internal InlineStyleSnapshot(DmNoteSpec s) {
            bg = s.Bg;
            activeBg = s.ActiveBg;
            outline = s.Outline;
            activeOutline = s.ActiveOutline;
            text = s.Text;
            activeText = s.ActiveText;
            counterText = s.CounterText;
            activeCounterText = s.ActiveCounterText;
            counterStroke = s.CounterStroke;
            activeCounterStroke = s.ActiveCounterStroke;
            borderRadius = s.BorderRadius;
            boxBorderWidth = s.BoxBorderWidth;
            fontSize = s.FontSize;
            counterFontSize = s.CounterFontSize;
            activeScale = s.ActiveScale;
        }
        internal void Restore(DmNoteSpec s) {
            s.Bg = bg;
            s.ActiveBg = activeBg;
            s.Outline = outline;
            s.ActiveOutline = activeOutline;
            s.Text = text;
            s.ActiveText = activeText;
            s.CounterText = counterText;
            s.ActiveCounterText = activeCounterText;
            s.CounterStroke = counterStroke;
            s.ActiveCounterStroke = activeCounterStroke;
            s.BorderRadius = borderRadius;
            s.BoxBorderWidth = boxBorderWidth;
            s.FontSize = fontSize;
            s.CounterFontSize = counterFontSize;
            s.ActiveScale = activeScale;
        }
    }
    private static void ApplyKeyStyle(DmNoteSpec spec, CssKeyStyleSet set) {
        CssKeyStyle i = set.Idle, a = set.Active;
        if(i.Radius.HasValue) spec.BorderRadius = Mathf.Clamp(i.Radius.Value, 0f, 100f);
        if(a.Radius.HasValue) spec.BorderRadius = Mathf.Clamp(a.Radius.Value, 0f, 100f);
        if(i.FontSize.HasValue) spec.FontSize = Mathf.Max(1, Mathf.RoundToInt(i.FontSize.Value));
        if(a.FontSize.HasValue) spec.FontSize = Mathf.Max(1, Mathf.RoundToInt(a.FontSize.Value));
        if(i.Bold.HasValue) spec.Bold = i.Bold.Value;
        if(a.Bold.HasValue) spec.Bold = a.Bold.Value;
        float? borderW = a.BorderWidth ?? i.BorderWidth;
        if(borderW.HasValue) spec.BoxBorderWidth = Mathf.Clamp(borderW.Value, 0f, 20f);
        if(i.Bg.Has) spec.Bg = ToColor(i.Bg);
        if(i.BorderColor.Has) spec.Outline = ToColor(i.BorderColor);
        if(i.TextColor.Has) spec.Text = ToColor(i.TextColor);
        spec.FillGradient = AssignGradient(ToGradient(i.BgGradient), ref spec.Bg);
        spec.LabelGradient = AssignGradient(ToGradient(i.TextGradient), ref spec.Text);
        if(i.TextShadow.On) spec.LabelGlow = ToGlow(i.TextShadow);
        if(i.BoxShadow.On) spec.BoxGlow = ToGlow(i.BoxShadow);
        if(a.Bg.Has) spec.ActiveBg = ToColor(a.Bg);
        if(a.BorderColor.Has) spec.ActiveOutline = ToColor(a.BorderColor);
        if(a.TextColor.Has) spec.ActiveText = ToColor(a.TextColor);
        spec.ActiveFillGradient = AssignGradient(ToGradient(a.BgGradient), ref spec.ActiveBg);
        spec.ActiveLabelGradient = AssignGradient(ToGradient(a.TextGradient), ref spec.ActiveText);
        if(a.TextShadow.On) spec.ActiveLabelGlow = ToGlow(a.TextShadow);
        if(a.BoxShadow.On) spec.ActiveBoxGlow = ToGlow(a.BoxShadow);
        if(i.Transform != null) {
            spec.IdleOffset = Translate(i);
            spec.IdleScale = Scale(i);
            spec.IdleRot = i.Transform.RotateDeg;
        }
        if(a.Transform != null) {
            spec.ActiveOffset = Translate(a);
            spec.ActiveScale = Scale(a);
            spec.ActiveRot = a.Transform.RotateDeg;
        }
        if(a.OffsetX.HasValue) spec.ActiveOffset.x += a.OffsetX.Value;
        if(a.OffsetY.HasValue) spec.ActiveOffset.y += a.OffsetY.Value;
        spec.ActiveOffsetX = spec.ActiveOffset.x;
        spec.ActiveOffsetY = spec.ActiveOffset.y;
        if(i.Filter != null) spec.IdleFilter = FilterColor(i.Filter);
        if(a.Filter != null) spec.ActiveFilter = FilterColor(a.Filter);
        if(i.Filter?.DropShadow.On == true && !spec.BoxGlow.On) spec.BoxGlow = ToGlow(i.Filter.DropShadow);
        if(a.Filter?.DropShadow.On == true && !spec.ActiveBoxGlow.On) spec.ActiveBoxGlow = ToGlow(a.Filter.DropShadow);
        spec.IdleBackdrop = i.BackdropBlur ?? 0f;
        spec.ActiveBackdrop = a.BackdropBlur ?? 0f;
        spec.TransitionSec = Mathf.Max(a.TransitionSeconds ?? 0f, i.TransitionSeconds ?? 0f);
        spec.IdleBefore = ToLayer(i.Before);
        spec.ActiveBefore = ToLayer(a.Before);
        spec.IdleAfter = ToLayer(i.After);
        spec.ActiveAfter = ToLayer(a.After);
        TMP_FontAsset font = ResolveFont(a.FontFamily) ?? ResolveFont(i.FontFamily);
        if(font != null) spec.CssFont = font;
        if(spec.BoxBorderWidth <= 0.01f) {
            spec.Outline.a = 0f;
            spec.ActiveOutline.a = 0f;
        }
    }
    private static void ApplyCounterStyle(DmNoteSpec spec, CssCounterStyleSet set) {
        CssCounterStyle i = set.Idle, a = set.Active;
        if(i.FontSize.HasValue) spec.CounterFontSize = Mathf.Max(1, Mathf.RoundToInt(i.FontSize.Value));
        if(a.FontSize.HasValue) spec.CounterFontSize = Mathf.Max(1, Mathf.RoundToInt(a.FontSize.Value));
        if(i.Bold.HasValue) spec.CounterBold = i.Bold.Value;
        if(a.Bold.HasValue) spec.CounterBold = a.Bold.Value;
        if(i.Color.Has) spec.CounterText = ToColor(i.Color);
        if(a.Color.Has) spec.ActiveCounterText = ToColor(a.Color);
        if(i.StrokeColor.Has) spec.CounterStroke = ToColor(i.StrokeColor);
        if(a.StrokeColor.Has) spec.ActiveCounterStroke = ToColor(a.StrokeColor);
        float? strokeW = a.StrokeWidth ?? i.StrokeWidth;
        if(strokeW.HasValue) spec.CounterStrokeWidth = strokeW.Value;
        spec.CounterGradient = AssignGradient(ToGradient(i.Gradient), ref spec.CounterText);
        spec.ActiveCounterGradient = AssignGradient(ToGradient(a.Gradient), ref spec.ActiveCounterText);
        if(i.TextShadow.On) spec.CounterGlow = ToGlow(i.TextShadow);
        if(a.TextShadow.On) spec.ActiveCounterGlow = ToGlow(a.TextShadow);
    }
    private static Vector2 Translate(CssKeyStyle s) =>
        s.Transform != null ? new Vector2(s.Transform.TranslateX, -s.Transform.TranslateY) : Vector2.zero;
    private static Vector2 Scale(CssKeyStyle s) =>
        s.Transform != null ? new Vector2(s.Transform.ScaleX, s.Transform.ScaleY) : Vector2.one;
    private static Color FilterColor(CssFilter f) {
        if(f == null) return Color.white;
        float m = Mathf.Clamp(f.Brightness * f.Contrast, 0f, 4f);
        m *= Mathf.Lerp(0.92f, 1.05f, Mathf.Clamp01(f.Saturate * 0.5f));
        float v = Mathf.Clamp01(m);
        return new Color(v, v, v, 1f);
    }
    private static CssAnimGradient AssignGradient(CssAnimGradient grad, ref Color solidFallback) {
        if(grad != null && grad.Stops.Length > 0) solidFallback = grad.Stops[0];
        return grad;
    }
    private static Color ToColor(CssColor c) => new(c.R, c.G, c.B, c.A);
    private static CssGlow ToGlow(CssShadow s) =>
        new(s.X, -s.Y, s.Blur, new Color(s.Color.R, s.Color.G, s.Color.B, s.Color.A));
    private static CssAnimGradient ToGradient(CssGradient g) {
        if(g == null || g.Stops.Count == 0) return null;
        Color[] stops = new Color[g.Stops.Count];
        for(int i = 0; i < stops.Length; i++) {
            CssColor c = g.Stops[i];
            stops[i] = new Color(c.R, c.G, c.B, c.A);
        }
        return new CssAnimGradient { Stops = stops, Period = g.Animated ? g.AnimSeconds : 0f, AngleDeg = g.AngleDeg };
    }
    private static CssLayerRt ToLayer(CssLayer layer) {
        if(layer == null) return null;
        var rt = new CssLayerRt {
            Bg = layer.Bg.Has ? ToColor(layer.Bg) : new Color(0f, 0f, 0f, 0f),
            Radius = layer.Radius ?? -1f,
            InsetT = layer.InsetT, InsetR = layer.InsetR, InsetB = layer.InsetB, InsetL = layer.InsetL,
            Blur = layer.Blur,
            Z = layer.Z,
        };
        if(layer.Gradient is { Stops.Count: > 0 } g) {
            rt.GradStops = new Color[g.Stops.Count];
            for(int i = 0; i < rt.GradStops.Length; i++) {
                CssColor c = g.Stops[i];
                rt.GradStops[i] = new Color(c.R, c.G, c.B, c.A);
            }
            rt.GradPeriod = g.Animated ? g.AnimSeconds : 0f;
            rt.GradAngle = g.AngleDeg;
        }
        return rt;
    }
}
