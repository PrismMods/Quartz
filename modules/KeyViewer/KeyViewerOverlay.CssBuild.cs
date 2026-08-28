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
    private static void BuildCssFx(Box box, DmNoteSpec spec) {
        try {
            BuildCssFxInner(box, spec);
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] CSS fx build failed: " + ex.Message);
        }
    }
    private static void BuildCssFxInner(Box box, DmNoteSpec spec) {
        if(spec.CssFont != null) {
            if(box.Label != null) { box.Label.font = spec.CssFont; Exempt(box.Label); }
            if(box.Value != null) { box.Value.font = spec.CssFont; Exempt(box.Value); }
        }
        if(spec.Bold && box.Label != null) box.Label.fontStyle |= FontStyles.Bold;
        if(spec.CounterBold && box.Value != null) box.Value.fontStyle |= FontStyles.Bold;
        if((spec.LabelGradient != null || spec.ActiveLabelGradient != null) && box.Label != null) box.Label.color = Color.white;
        if((spec.CounterGradient != null || spec.ActiveCounterGradient != null) && box.Value != null) box.Value.color = Color.white;
        BuildKeyImage(box, spec);
        BuildBoxGlow(box, spec);
        BuildFillGradient(box, spec);
        box.BeforeLayer = BuildPseudo(box, spec, spec.IdleBefore ?? spec.ActiveBefore, true);
        box.AfterLayer = BuildPseudo(box, spec, spec.IdleAfter ?? spec.ActiveAfter, false);
        bool animated = IsAnimated(spec)
            || spec.TransitionSec > 0.01f
            || LayerAnimated(spec.IdleBefore) || LayerAnimated(spec.ActiveBefore)
            || LayerAnimated(spec.IdleAfter) || LayerAnimated(spec.ActiveAfter);
        if(animated) cssFx.Add(box);
    }
    private static void Exempt(Component c) {
        if(c.GetComponent<FontExempt>() == null) c.gameObject.AddComponent<FontExempt>();
    }
    private static void BuildBoxGlow(Box box, DmNoteSpec spec) {
        if(!spec.BoxGlow.On && !spec.ActiveBoxGlow.On) return;
        float blur = Mathf.Max(spec.BoxGlow.Blur, spec.ActiveBoxGlow.Blur);
        float pad = Mathf.Max(2f, blur + spec.BoxBorderWidth);
        GameObject obj = new("CssGlow");
        obj.transform.SetParent(EnsureGlowLayer(), false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(spec.X - pad, -(spec.Y - pad));
        rt.sizeDelta = new Vector2(spec.W + pad * 2f, spec.H + pad * 2f);
        Image img = obj.AddComponent<Image>();
        img.sprite = GlowSprite();
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        box.Glow = img;
    }
    private static void BuildFillGradient(Box box, DmNoteSpec spec) {
        CssAnimGradient g = spec.FillGradient ?? spec.ActiveFillGradient;
        if(g == null || box.Fill == null) return;
        Mask mask = box.Fill.GetComponent<Mask>() ?? box.Fill.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        GameObject obj = new("CssFillGrad");
        obj.transform.SetParent(box.Fill.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        float diag = Mathf.Sqrt(spec.W * spec.W + spec.H * spec.H);
        rt.sizeDelta = new Vector2(diag, diag);
        rt.localRotation = Quaternion.Euler(0f, 0f, g.AngleDeg - 90f);
        RawImage ri = obj.AddComponent<RawImage>();
        ri.texture = GradientTexture(g.Stops, 0f);
        ri.raycastTarget = false;
        rt.SetAsFirstSibling();
        box.FillGrad = ri;
    }
    private static RawImage BuildPseudo(Box box, DmNoteSpec spec, CssLayerRt layer, bool isBefore) {
        if(layer == null) return null;
        bool behind = layer.Z < 0;
        GameObject obj = new(isBefore ? "CssBefore" : "CssAfter");
        obj.transform.SetParent(behind ? EnsureGlowLayer() : box.Fill.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        RawImage ri = obj.AddComponent<RawImage>();
        ri.raycastTarget = false;
        float w = spec.W - layer.InsetL - layer.InsetR;
        float h = spec.H - layer.InsetT - layer.InsetB;
        if(behind && layer.HasGradient) {
            float cx = spec.X + spec.W * 0.5f + (layer.InsetL - layer.InsetR) * 0.5f;
            float cy = spec.Y + spec.H * 0.5f + (layer.InsetT - layer.InsetB) * 0.5f;
            float diag = Mathf.Sqrt(w * w + h * h);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(diag, diag);
            rt.localRotation = Quaternion.Euler(0f, 0f, layer.GradAngle - 90f);
            ri.texture = GradientTexture(layer.GradStops, layer.Blur);
            ri.color = Color.white;
        } else if(behind) {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(spec.X + layer.InsetL, -(spec.Y + layer.InsetT));
            rt.sizeDelta = new Vector2(w, h);
            ri.color = layer.Bg;
        } else {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(layer.InsetL, layer.InsetB);
            rt.offsetMax = new Vector2(-layer.InsetR, -layer.InsetT);
            if(layer.HasGradient) {
                ri.texture = GradientTexture(layer.GradStops, layer.Blur);
                ri.color = Color.white;
            } else {
                ri.color = layer.Bg;
            }
        }
        return ri;
    }
    private static bool LayerAnimated(CssLayerRt layer) => layer is { HasGradient: true, GradPeriod: > 0.01f };
    private static RectTransform EnsureGlowLayer() {
        if(cssGlowLayer != null) return cssGlowLayer;
        GameObject obj = new("CssGlowLayer");
        obj.transform.SetParent(buildRoot, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling();
        cssGlowLayer = rt;
        return cssGlowLayer;
    }
    private static bool IsAnimated(DmNoteSpec spec) =>
        Animated(spec.LabelGradient) || Animated(spec.ActiveLabelGradient)
        || Animated(spec.CounterGradient) || Animated(spec.ActiveCounterGradient)
        || Animated(spec.FillGradient) || Animated(spec.ActiveFillGradient);
    private static bool Animated(CssAnimGradient g) => g != null && g.Period > 0.01f && g.Stops.Length > 1;
}
