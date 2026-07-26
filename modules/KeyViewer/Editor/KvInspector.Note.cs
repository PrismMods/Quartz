using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvInspector {
    private static readonly string[] NoteAligns = ["left", "center", "right"];
    private void BuildNoteTab(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        if(batch.Length == 0) return;
        KvElement first = batch[0];
        Header(root, "KVI_SEC_NOTE", "Rain");
        Flag(root, tracked, "Rain Effect", "kvi_note_enabled", true,
            batch, el => KvProps.Bool(el.Raw, "noteEffectEnabled", true),
            (el, v) => el.Raw["noteEffectEnabled"] = v);
        NoteColorRows(
            root, tracked, batch, "noteColor",
            "kvi_note_gradient", "Rain Gradient",
            "kvi_note_color", "Rain Color", _ => "#FFFFFF",
            "Fade the rain between two colours from top to bottom instead of using one flat colour."
        );
        Num(root, tracked, "Rain Opacity", "kvi_note_opacity", 80f, 0f, 100f, "0' %'", 1f,
            batch, el => KvProps.Int(el.Raw, "noteOpacity", 80),
            (el, v) => SetNoteOpacity(el.Raw, "noteOpacity", v));
        Num(root, tracked, "Corner Radius", "kvi_note_radius", 0f, 0f, 30f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "noteBorderRadius", 0f),
            (el, v) => KvProps.SetInt(el.Raw, "noteBorderRadius", v));
        Header(root, "KVI_SEC_NOTE_GLOW", "Glow");
        Flag(root, tracked, "Rain Glow", "kvi_note_glow", false,
            batch, el => KvProps.Bool(el.Raw, "noteGlowEnabled", false),
            (el, v) => el.Raw["noteGlowEnabled"] = v);
        Num(root, tracked, "Glow Size", "kvi_note_glow_size", 20f, 0f, 50f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "noteGlowSize", 20f),
            (el, v) => KvProps.SetInt(el.Raw, "noteGlowSize", v));
        NoteColorRows(
            root, tracked, batch, "noteGlowColor",
            "kvi_note_glow_gradient", "Glow Gradient",
            "kvi_note_glow_color", "Glow Color",
            el => KvProps.SolidHex(el.Raw, "noteColor", "#FFFFFF"),
            "Fade the glow between two colours from top to bottom instead of using one flat colour."
        );
        Num(root, tracked, "Glow Opacity", "kvi_note_glow_opacity", 70f, 0f, 100f, "0' %'", 1f,
            batch, el => KvProps.Int(el.Raw, "noteGlowOpacity", 70),
            (el, v) => SetNoteOpacity(el.Raw, "noteGlowOpacity", v));
        Header(root, "KVI_SEC_NOTE_SHADOW", "Shadow");
        Flag(root, tracked, "Rain Shadow", "kvi_note_shadow", false,
            batch, el => KvProps.Bool(el.Raw, "quartzNoteShadow", false),
            (el, v) => el.Raw["quartzNoteShadow"] = v);
        Colour(root, tracked, "Shadow Color", "kvi_note_shadow_color",
            new Color(0f, 0f, 0f, 0.5f),
            batch, el => KvProps.Color(el.Raw, "quartzNoteShadowColor", "rgba(0, 0, 0, 0.5)", 0.5f),
            (el, c) => KvProps.SetColor(el.Raw, "quartzNoteShadowColor", c), true);
        Num(root, tracked, "Shadow Offset X", "kvi_note_shadow_x", 3f, -30f, 30f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "quartzNoteShadowX", 3f),
            (el, v) => KvProps.SetInt(el.Raw, "quartzNoteShadowX", v));
        Num(root, tracked, "Shadow Offset Y", "kvi_note_shadow_y", -3f, -30f, 30f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "quartzNoteShadowY", -3f),
            (el, v) => KvProps.SetInt(el.Raw, "quartzNoteShadowY", v));
        Header(root, "KVI_SEC_NOTE_BORDER", "Border");
        Num(root, tracked, "Border Width", "kvi_note_border_width", 0f, 0f, 20f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "noteBorderWidth", 0f),
            (el, v) => KvProps.SetInt(el.Raw, "noteBorderWidth", v));
        Colour(root, tracked, "Border Color", "kvi_note_border_color", Color.white,
            batch, el => KvProps.Color(el.Raw, "noteBorderColor", "#FFFFFF", 1f),
            (el, c) => el.Raw["noteBorderColor"] = KvProps.ToHex(c), false);
        Num(root, tracked, "Border Opacity", "kvi_note_border_opacity", 100f, 0f, 100f, "0' %'", 1f,
            batch, el => KvProps.Int(el.Raw, "noteBorderOpacity", 100),
            (el, v) => KvProps.SetInt(el.Raw, "noteBorderOpacity", v));
        Segments(root, BorderSides, BorderSideName, BorderSideKey,
            MatchMulti(BorderSides, batch, el => KvProps.Str(el.Raw, "noteBorderSide", "all"), "all"),
            v => {
                Edit(() => {
                    foreach(KvElement el in batch) el.Raw["noteBorderSide"] = v;
                });
                Push();
            });
        Header(root, "KVI_SEC_NOTE_POS", "Track");
        Num(root, tracked, "Rain Width", "kvi_note_width", first.W, 0f, 500f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "noteWidth", el.W),
            (el, v) => el.Raw["noteWidth"] = v);
        Segments(root, NoteAligns, AlignName, AlignKey,
            MatchMulti(NoteAligns, batch, el => KvProps.Str(el.Raw, "noteAlignment", "center"), "center"),
            v => {
                Edit(() => {
                    foreach(KvElement el in batch) el.Raw["noteAlignment"] = v;
                });
                Push();
            });
        Num(root, tracked, "Rain Offset X", "kvi_note_offset_x", 0f, -500f, 500f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "noteOffsetX", 0f),
            (el, v) => KvProps.SetInt(el.Raw, "noteOffsetX", v));
        Num(root, tracked, "Rain Offset Y", "kvi_note_offset_y", 0f, -500f, 500f, "0 px", 1f,
            batch, el => KvProps.Float(el.Raw, "noteOffsetY", 0f),
            (el, v) => KvProps.SetInt(el.Raw, "noteOffsetY", v));
        Flag(root, tracked, "Align Rain To Track Top", "kvi_note_autoy", true,
            batch, el => KvProps.Bool(el.Raw, "noteAutoYCorrection", true),
            (el, v) => el.Raw["noteAutoYCorrection"] = v
        ).Rect.AddToolTip(
            "DESC_KVI_NOTE_AUTOY",
            "Start this element's rain at the top of the shared track rather than at the element itself. Turn it off to give a lower element a shorter track of its own."
        );
    }
    private static readonly string[] BorderSides = ["all", "vertical", "horizontal"];
    private static string BorderSideName(string s) => s switch {
        "vertical" => MainCore.Tr.Get("KVI_BSIDE_VERTICAL", "Vertical"),
        "horizontal" => MainCore.Tr.Get("KVI_BSIDE_HORIZONTAL", "Horizontal"),
        _ => MainCore.Tr.Get("KVI_BSIDE_ALL", "All"),
    };
    private static string BorderSideKey(string s) => s switch {
        "vertical" => "KVI_BSIDE_VERTICAL",
        "horizontal" => "KVI_BSIDE_HORIZONTAL",
        _ => "KVI_BSIDE_ALL",
    };
    private static void SetNoteOpacity(JObject o, string key, float v) {
        KvProps.SetInt(o, key, v);
        if(o[key + "Top"] != null) KvProps.SetInt(o, key + "Top", v);
        if(o[key + "Bottom"] != null) KvProps.SetInt(o, key + "Bottom", v);
    }
    private void NoteColorRows(
        RectTransform root, List<UIObject> tracked, KvElement[] batch,
        string field, string gradientId, string gradientLabel,
        string colorId, string colorLabel, Func<KvElement, string> fallbackHex, string tooltip
    ) {
        KvElement first = batch[0];
        string firstFallback = fallbackHex(first);
        bool single = batch.Length == 1;
        bool gradient = single && KvProps.IsGradient(first.Raw, field);
        if(single) {
            Flag(root, tracked, gradientLabel, gradientId, false, gradient, v => {
                if(v) KvProps.AsGradient(first.Raw, field, firstFallback);
                else KvProps.MakeSolid(first.Raw, field, firstFallback);
            }, rebuild: true).Rect.AddToolTip("DESC_" + gradientId.ToUpperInvariant(), tooltip);
        }
        Color def = KeyViewerOverlay.HexToColor(firstFallback, 1f);
        if(!gradient) {
            Colour(root, tracked, colorLabel, colorId, def,
                batch, el => KvProps.NoteColor(el.Raw, field, fallbackHex(el)),
                (el, c) => KvProps.SetNoteSolid(el.Raw, field, c), false);
            return;
        }
        Colour(root, tracked, colorLabel + " (Top)", colorId + "_top", def,
            batch, el => KvProps.NoteColor(el.Raw, field, fallbackHex(el)),
            (el, c) => KvProps.SetNoteStop(el.Raw, field, false, c, fallbackHex(el)), false);
        Colour(root, tracked, colorLabel + " (Bottom)", colorId + "_bottom", def,
            batch, el => KvProps.NoteColor(el.Raw, field, fallbackHex(el), true),
            (el, c) => KvProps.SetNoteStop(el.Raw, field, true, c, fallbackHex(el)), false);
    }
    private static string Match(string[] known, string value, string fallback) {
        foreach(string k in known)
            if(string.Equals(k, value, StringComparison.OrdinalIgnoreCase)) return k;
        return fallback;
    }
    private static string AlignName(string s) => s switch {
        "left" => MainCore.Tr.Get("KVI_ALIGN_LEFT", "Left"),
        "right" => MainCore.Tr.Get("KVI_ALIGN_RIGHT", "Right"),
        _ => MainCore.Tr.Get("KVI_ALIGN_CENTER", "Center"),
    };
    private static string AlignKey(string s) => s switch {
        "left" => "KVI_ALIGN_LEFT",
        "right" => "KVI_ALIGN_RIGHT",
        _ => "KVI_ALIGN_CENTER",
    };
}
