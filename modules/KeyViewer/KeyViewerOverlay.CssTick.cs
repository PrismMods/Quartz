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
    private static void ApplyCssState(Box box, bool pressed) {
        try {
            ApplyCssStateInner(box, pressed);
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] CSS state failed: " + ex.Message);
        }
    }
    private static void ApplyCssStateInner(Box box, bool pressed) {
        DmNoteSpec spec = box.Dm;
        if(box.Label != null && (spec.LabelGlow.On || spec.ActiveLabelGlow.On)) {
            CssGlow g = pressed ? spec.ActiveLabelGlow : spec.LabelGlow;
            TMPTextShadow.Apply(box.Label, g.On, g.X, g.Y, g.Blur, g.Color);
        }
        if(box.Value != null && (spec.CounterGlow.On || spec.ActiveCounterGlow.On)) {
            CssGlow g = pressed ? spec.ActiveCounterGlow : spec.CounterGlow;
            TMPTextShadow.Apply(box.Value, g.On, g.X, g.Y, g.Blur, g.Color);
        }
        if(box.Value != null && spec.CounterStrokeWidth > 0.01f) {
            Color stroke = pressed ? spec.ActiveCounterStroke : spec.CounterStroke;
            if(box.CounterStrokeMat == null) box.CounterStrokeMat = box.Value.fontMaterial;
            Material mat = box.CounterStrokeMat;
            if(stroke.a > 0.001f) {
                mat.SetColor(ShaderUtilities.ID_OutlineColor, stroke);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Clamp(spec.CounterStrokeWidth * 0.1f, 0f, 0.5f));
            } else {
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            }
        }
        if(box.Glow != null) {
            CssGlow g = pressed ? spec.ActiveBoxGlow : spec.BoxGlow;
            box.Glow.enabled = g.On;
            if(g.On) box.Glow.color = g.Color;
        }
        if(spec.HasStateTransform) {
            Vector2 off = pressed ? spec.ActiveOffset : spec.IdleOffset;
            Vector2 scl = pressed ? spec.ActiveScale : spec.IdleScale;
            float rot = pressed ? spec.ActiveRot : spec.IdleRot;
            box.Fill.rectTransform.anchoredPosition = new Vector2(spec.X + off.x, -(spec.Y + off.y));
            box.Fill.rectTransform.localScale = new Vector3(scl.x, scl.y, 1f);
            box.Fill.rectTransform.localRotation = rot == 0f ? Quaternion.identity : Quaternion.Euler(0f, 0f, -rot);
        }
        Color filter = pressed ? spec.ActiveFilter : spec.IdleFilter;
        if(filter != Color.white) {
            ApplyFilterTint(box, filter, pressed);
        }
        float backdrop = pressed ? spec.ActiveBackdrop : spec.IdleBackdrop;
        if(backdrop > 0f && box.Fill != null && spec.FillGradient == null && spec.ActiveFillGradient == null) {
            Color baseBg = pressed ? spec.ActiveBg : spec.Bg;
            float frost = Mathf.Clamp01(0.25f + backdrop * 0.03f);
            box.Fill.color = new Color(baseBg.r, baseBg.g, baseBg.b, Mathf.Max(baseBg.a, frost));
        }
        ApplyImageState(box, spec, pressed);
        ApplyPseudoState(box.BeforeLayer, pressed ? spec.ActiveBefore : spec.IdleBefore);
        ApplyPseudoState(box.AfterLayer, pressed ? spec.ActiveAfter : spec.IdleAfter);
        if(box.FillGrad != null) {
            CssAnimGradient fg = pressed ? spec.ActiveFillGradient : spec.FillGradient;
            box.FillGrad.enabled = fg != null;
        }
        if(spec.TransitionSec > 0.01f) box.TransStart = KvClock.Now;
    }
    private static void ApplyFilterTint(Box box, Color f, bool pressed) {
        if(box.Label != null && (pressed ? box.Dm.ActiveLabelGradient : box.Dm.LabelGradient) == null) {
            box.Label.color = Mul(box.Label.color, f);
        }
        if(box.Value != null && (pressed ? box.Dm.ActiveCounterGradient : box.Dm.CounterGradient) == null) {
            box.Value.color = Mul(box.Value.color, f);
        }
        if(box.Border != null) box.Border.color = Mul(box.Border.color, f);
    }
    private static Color Mul(Color a, Color b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
    private static void ApplyPseudoState(RawImage layer, CssLayerRt rt) {
        if(layer == null) return;
        if(rt == null) {
            layer.enabled = false;
            return;
        }
        layer.enabled = true;
        if(!rt.HasGradient) layer.color = rt.Bg;
    }
    private static void CssTick(float time) {
        for(int i = 0; i < cssFx.Count; i++) {
            Box box = cssFx[i];
            if(box?.Dm == null) continue;
            try {
                TickBox(box, time);
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
    private static void TickBox(Box box, float time) {
        DmNoteSpec spec = box.Dm;
        bool pressed = box.Pressed;
        CssAnimGradient lg = pressed ? spec.ActiveLabelGradient : spec.LabelGradient;
        if(box.Label != null && lg != null
            && (lg.Period > 0.01f || lg != box.GradLabelApplied || box.GradLabelText == null)) {
            ApplyGlyphGradient(box.Label, lg, time, ref box.GradLabelText);
            box.GradLabelApplied = lg;
        }
        CssAnimGradient cg = pressed ? spec.ActiveCounterGradient : spec.CounterGradient;
        if(box.Value != null && cg != null
            && (cg.Period > 0.01f || cg != box.GradValueApplied || box.GradValueText == null)) {
            ApplyGlyphGradient(box.Value, cg, time, ref box.GradValueText);
            box.GradValueApplied = cg;
        }
        CssAnimGradient fg = pressed ? spec.ActiveFillGradient : spec.FillGradient;
        if(box.FillGrad != null && box.FillGrad.enabled && fg != null && fg.Period > 0.01f) {
            box.FillGrad.uvRect = new Rect((time / fg.Period) % 1f, 0f, 1f, 1f);
        }
        ScrollLayer(box.BeforeLayer, pressed ? spec.ActiveBefore : spec.IdleBefore, time);
        ScrollLayer(box.AfterLayer, pressed ? spec.ActiveAfter : spec.IdleAfter, time);
        if(box.TransStart >= 0f && spec.TransitionSec > 0.01f) {
            TickTransition(box, spec, pressed, time);
        }
    }
    private static void ScrollLayer(RawImage layer, CssLayerRt rt, float time) {
        if(layer != null && layer.enabled && rt is { HasGradient: true, GradPeriod: > 0.01f }) {
            layer.uvRect = new Rect((time / rt.GradPeriod) % 1f, 0f, 1f, 1f);
        }
    }
    private static void TickTransition(Box box, DmNoteSpec spec, bool pressed, float time) {
        float t = Mathf.Clamp01((time - box.TransStart) / spec.TransitionSec);
        Color fillTo = pressed ? spec.ActiveBg : spec.Bg;
        Color fillFrom = pressed ? spec.Bg : spec.ActiveBg;
        if(box.Fill != null && spec.FillGradient == null && spec.ActiveFillGradient == null) {
            box.Fill.color = Color.Lerp(fillFrom, fillTo, t);
        }
        if(box.Border != null) {
            box.Border.color = Color.Lerp(pressed ? spec.Outline : spec.ActiveOutline,
                pressed ? spec.ActiveOutline : spec.Outline, t);
        }
        if(box.Label != null && spec.LabelGradient == null && spec.ActiveLabelGradient == null) {
            box.Label.color = Color.Lerp(pressed ? spec.Text : spec.ActiveText,
                pressed ? spec.ActiveText : spec.Text, t);
        }
        if(t >= 1f) box.TransStart = -1f;
    }
    private static void ApplyGlyphGradient(TMP_Text tmp, CssAnimGradient g, float time, ref string lastText) {
        if(g.Stops.Length == 0) return;
        string text = tmp.text;
        if(!string.Equals(text, lastText, StringComparison.Ordinal)) {
            tmp.ForceMeshUpdate();
            lastText = text;
        }
        TMP_TextInfo info = tmp.textInfo;
        if(info == null || info.characterCount == 0) return;
        float scroll = g.Period > 0.01f ? (time / g.Period) % 1f : 0f;
        int count = info.characterCount;
        int lastMat = -1;
        Color32[] cols = null;
        bool wrote = false;
        for(int i = 0; i < count; i++) {
            ref TMP_CharacterInfo ch = ref info.characterInfo[i];
            if(!ch.isVisible) continue;
            float u = count > 1 ? (float)i / (count - 1) : 0f;
            Color32 col = SampleGradient(g.Stops, u + scroll);
            int mat = ch.materialReferenceIndex;
            if(cols == null || mat != lastMat) {
                cols = info.meshInfo[mat].colors32;
                lastMat = mat;
            }
            int vi = ch.vertexIndex;
            cols[vi] = col;
            cols[vi + 1] = col;
            cols[vi + 2] = col;
            cols[vi + 3] = col;
            wrote = true;
        }
        if(wrote) tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
