#if !IL2CPP && !QUARTZ_KEYVIEWER
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Editor;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The Rain tab, over the selection's keys only: rain, glow, shadow, border and track, as the classic Rain tab.
internal static partial class KvInspectorPages {
    private static readonly string[] NoteAligns = ["left", "center", "right"];
    private static readonly string[] BorderSides = ["all", "vertical", "horizontal"];

    private static void BuildRain(VisualElement p, KvElement[] keys) {
        if(keys.Length == 0) return;
        bool single = keys.Length == 1;
        VisualElement rain = Section(p, "KVI_SEC_NOTE");
        Flag(rain, "KVI_NOTE_ENABLED", true, Keys, e => KvProps.Bool(e.Raw, "noteEffectEnabled", true), (e, v) => e.Raw["noteEffectEnabled"] = v);
        NoteColorRows(rain, keys, single, "noteColor", "KVI_NOTE_GRADIENT", "KVI_NOTE_COLOR", _ => "#FFFFFF");
        Num(rain, "KVI_NOTE_OPACITY", 80f, 0f, 100f, "0' %'", 1f, Keys, e => KvProps.Int(e.Raw, "noteOpacity", 80),
            (e, v) => SetNoteOpacity(e.Raw, "noteOpacity", v));
        Num(rain, "KVI_NOTE_RADIUS", 0f, 0f, 30f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "noteBorderRadius", 0f),
            (e, v) => KvProps.SetInt(e.Raw, "noteBorderRadius", v));

        VisualElement glow = Section(p, "KVI_SEC_NOTE_GLOW");
        Flag(glow, "KVI_NOTE_GLOW", false, Keys, e => KvProps.Bool(e.Raw, "noteGlowEnabled", false), (e, v) => e.Raw["noteGlowEnabled"] = v);
        Num(glow, "KVI_NOTE_GLOW_SIZE", 20f, 0f, 50f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "noteGlowSize", 20f),
            (e, v) => KvProps.SetInt(e.Raw, "noteGlowSize", v));
        NoteColorRows(glow, keys, single, "noteGlowColor", "KVI_NOTE_GLOW_GRADIENT", "KVI_NOTE_GLOW_COLOR",
            e => KvProps.SolidHex(e.Raw, "noteColor", "#FFFFFF"));
        Num(glow, "KVI_NOTE_GLOW_OPACITY", 70f, 0f, 100f, "0' %'", 1f, Keys, e => KvProps.Int(e.Raw, "noteGlowOpacity", 70),
            (e, v) => SetNoteOpacity(e.Raw, "noteGlowOpacity", v));

        VisualElement shadow = Section(p, "KVI_SEC_NOTE_SHADOW");
        Flag(shadow, "KVI_NOTE_SHADOW", false, Keys, e => KvProps.Bool(e.Raw, "quartzNoteShadow", false), (e, v) => e.Raw["quartzNoteShadow"] = v);
        Colour(shadow, "KVI_NOTE_SHADOW_COLOR", new Color(0f, 0f, 0f, 0.5f), true, Keys,
            e => KvProps.Color(e.Raw, "quartzNoteShadowColor", "rgba(0, 0, 0, 0.5)", 0.5f), (e, c) => KvProps.SetColor(e.Raw, "quartzNoteShadowColor", c));
        Num(shadow, "KVI_NOTE_SHADOW_X", 3f, -30f, 30f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "quartzNoteShadowX", 3f),
            (e, v) => KvProps.SetInt(e.Raw, "quartzNoteShadowX", v));
        Num(shadow, "KVI_NOTE_SHADOW_Y", -3f, -30f, 30f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "quartzNoteShadowY", -3f),
            (e, v) => KvProps.SetInt(e.Raw, "quartzNoteShadowY", v));

        VisualElement border = Section(p, "KVI_SEC_NOTE_BORDER");
        Num(border, "KVI_NOTE_BORDER_WIDTH", 0f, 0f, 20f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "noteBorderWidth", 0f),
            (e, v) => KvProps.SetInt(e.Raw, "noteBorderWidth", v));
        Colour(border, "KVI_NOTE_BORDER_COLOR", Color.white, false, Keys,
            e => KvProps.Color(e.Raw, "noteBorderColor", "#FFFFFF", 1f), (e, c) => e.Raw["noteBorderColor"] = KvProps.ToHex(c));
        Num(border, "KVI_NOTE_BORDER_OPACITY", 100f, 0f, 100f, "0' %'", 1f, Keys, e => KvProps.Int(e.Raw, "noteBorderOpacity", 100),
            (e, v) => KvProps.SetInt(e.Raw, "noteBorderOpacity", v));
        Pick(border, "kv-note-border-side", BorderSides, BorderSideKey, "all", Keys,
            e => KvProps.Str(e.Raw, "noteBorderSide", "all"), (e, v) => e.Raw["noteBorderSide"] = v);

        VisualElement track = Section(p, "KVI_SEC_NOTE_POS");
        Num(track, "KVI_NOTE_WIDTH", keys[0].W, 0f, 500f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "noteWidth", e.W), (e, v) => e.Raw["noteWidth"] = v);
        Pick(track, "kv-note-align", NoteAligns, AlignKey, "center", Keys,
            e => KvProps.Str(e.Raw, "noteAlignment", "center"), (e, v) => e.Raw["noteAlignment"] = v);
        Num(track, "KVI_NOTE_OFFSET_X", 0f, -500f, 500f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "noteOffsetX", 0f),
            (e, v) => KvProps.SetInt(e.Raw, "noteOffsetX", v));
        Num(track, "KVI_NOTE_OFFSET_Y", 0f, -500f, 500f, "0 px", 1f, Keys, e => KvProps.Float(e.Raw, "noteOffsetY", 0f),
            (e, v) => KvProps.SetInt(e.Raw, "noteOffsetY", v));
        Flag(track, "KVI_NOTE_AUTOY", true, Keys, e => KvProps.Bool(e.Raw, "noteAutoYCorrection", true),
            (e, v) => e.Raw["noteAutoYCorrection"] = v, "DESC_KVI_NOTE_AUTOY");
    }

    // One colour, or a gradient's two stops: only a single key offers the gradient switch, as classic.
    private static void NoteColorRows(VisualElement p, KvElement[] keys, bool single, string field, string gradientId, string colorId,
        Func<KvElement, string> fallbackHex) {
        Color def = KeyViewerOverlay.HexToColor(fallbackHex(keys[0]), 1f);
        Func<bool> gradient = () => single && Keys().Count == 1 && KvProps.IsGradient(Keys()[0].Raw, field);
        if(single) {
            Flag(p, gradientId, false, Keys, e => KvProps.IsGradient(e.Raw, field), (e, v) => {
                if(v) KvProps.AsGradient(e.Raw, field, fallbackHex(e));
                else KvProps.MakeSolid(e.Raw, field, fallbackHex(e));
            }, "DESC_" + gradientId, rebuild: true);
        }
        Colour(p, colorId, def, false, Keys, e => KvProps.NoteColor(e.Raw, field, fallbackHex(e)),
            (e, c) => KvProps.SetNoteSolid(e.Raw, field, c), () => !gradient());
        if(!single) return;
        Colour(p, colorId + "_TOP", def, false, Keys, e => KvProps.NoteColor(e.Raw, field, fallbackHex(e)),
            (e, c) => KvProps.SetNoteStop(e.Raw, field, false, c, fallbackHex(e)), gradient);
        Colour(p, colorId + "_BOTTOM", def, false, Keys, e => KvProps.NoteColor(e.Raw, field, fallbackHex(e), true),
            (e, c) => KvProps.SetNoteStop(e.Raw, field, true, c, fallbackHex(e)), gradient);
    }

    private static void SetNoteOpacity(JObject o, string key, float v) {
        KvProps.SetInt(o, key, v);
        if(o[key + "Top"] != null) KvProps.SetInt(o, key + "Top", v);
        if(o[key + "Bottom"] != null) KvProps.SetInt(o, key + "Bottom", v);
    }

    private static string BorderSideKey(string s) => s switch {
        "vertical" => "KVI_BSIDE_VERTICAL",
        "horizontal" => "KVI_BSIDE_HORIZONTAL",
        _ => "KVI_BSIDE_ALL",
    };

    private static string AlignKey(string s) => s switch {
        "left" => "KVI_ALIGN_LEFT",
        "right" => "KVI_ALIGN_RIGHT",
        _ => "KVI_ALIGN_CENTER",
    };
}
#endif
