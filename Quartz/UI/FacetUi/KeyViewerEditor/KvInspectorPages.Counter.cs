#if !IL2CPP && !QUARTZ_KEYVIEWER
using Facet;
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Editor;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The Counter tab, over the selection's keys and stats: visibility, placement, size, animation and colours, as classic.
internal static partial class KvInspectorPages {
    private static readonly string[] Placements = ["inside", "outside"];
    private static readonly string[] CounterAligns = ["top", "bottom", "left", "right"];
    private static readonly string[] AlignModes = ["center", "between"];

    private static void BuildCounter(VisualElement p, KvElement[] batch) {
        if(batch.Length == 0) return;
        VisualElement counter = p;   // the tab's own name: no section head repeating it
        Flag(counter, "KVI_COUNTER_ENABLED", true, Counted, e => KvProps.Bool(Read(e), "enabled", true),
            (e, v) => Counter(e)["enabled"] = v, "DESC_KVI_COUNTER_ENABLED");
        if(batch.Any(e => e.Kind == KvElementKind.Key)) {
            Flag(counter, "KVI_COUNTER_PERKEYKPS", false, Keys, e => e.PerKeyKps, (e, v) => e.PerKeyKps = v, "DESC_KVI_COUNTER_PERKEYKPS");
            Flag(counter, "KVI_COUNTER_SHOWWHILEPRESSING", true, Keys, e => e.CounterShowWhilePressed,
                (e, v) => e.CounterShowWhilePressed = v, "DESC_KVI_COUNTER_SHOWWHILEPRESSING");
        }

        VisualElement place = Section(p, "KVI_SEC_COUNTER_PLACE");
        CounterPick(place, "kv-counter-placement", Placements, s => s == "outside" ? "KVI_PLACE_OUTSIDE" : "KVI_PLACE_INSIDE", "placement", "inside");
        CounterPick(place, "kv-counter-align", CounterAligns, CounterAlignKey, "align", "top");
        CounterPick(place, "kv-counter-mode", AlignModes, s => s == "between" ? "KVI_CMODE_BETWEEN" : "KVI_CMODE_CENTER", "alignMode", "center");
        Ui.Text(place, "KVI_COUNTER_MODE_HINT", role: Role.TextMuted).style.whiteSpace = WhiteSpace.Normal;
        Num(place, "KVI_COUNTER_GAP", 6f, 0f, 100f, "0 px", 1f, Counted, e => KvProps.Float(Read(e), "gap", 6f),
            (e, v) => KvProps.SetInt(Counter(e), "gap", v));
        Num(place, "KVI_COUNTER_FONT", 16f, 1f, 200f, "0 px", 1f, Counted, e => KvProps.Int(Read(e), "fontSize", 16),
            (e, v) => KvProps.SetInt(Counter(e), "fontSize", v));
        FontStyleRows(place, Counted, "KVI_COUNTER", Read, Counter);

        VisualElement anim = Section(p, "KVI_SEC_COUNTER_ANIM");
        Flag(anim, "KVI_COUNTER_ANIM", true, Counted, e => KvProps.Bool(AnimOrNull(e), "enabled", true), (e, v) => Anim(e)["enabled"] = v);
        Num(anim, "KVI_COUNTER_ANIM_SCALE", 110f, 100f, 200f, "0' %'", 1f, Counted, e => KvProps.Float(AnimOrNull(e), "scale", 1.1f) * 100f,
            (e, v) => Anim(e)["scale"] = Mathf.Round(v) / 100f);
        Num(anim, "KVI_COUNTER_ANIM_DURATION", 300f, 50f, 2000f, "0 ms", 10f, Counted, e => KvProps.Float(AnimOrNull(e), "durationMs", 300f),
            (e, v) => KvProps.SetInt(Anim(e), "durationMs", v));

        VisualElement colors = Section(p, "KVI_SEC_COUNTER_COLORS");
        CounterColor(colors, batch[0], "KVI_COUNTER_FILL", "fill", "idle", e => KvProps.Str(e.Raw, "fontColor", DefFont), 1f);
        CounterColor(colors, batch[0], "KVI_COUNTER_FILL_ACTIVE", "fill", "active", e => KvProps.Str(e.Raw, "activeFontColor", DefFontActive), 1f);
        CounterColor(colors, batch[0], "KVI_COUNTER_STROKE", "stroke", "idle", _ => "transparent", 0f);
        CounterColor(colors, batch[0], "KVI_COUNTER_STROKE_ACTIVE", "stroke", "active", _ => "transparent", 0f);
    }

    private static void CounterPick(VisualElement p, string id, string[] values, Func<string, string> key, string field, string def) =>
        Pick(p, id, values, key, def, Counted, e => KvProps.Str(Read(e), field, def), (e, v) => Counter(e)[field] = v);

    private static void CounterColor(VisualElement p, KvElement first, string id, string group, string field,
        Func<KvElement, string> def, float defAlpha) =>
        Colour(p, id, KeyViewerOverlay.HexToColor(def(first), defAlpha), true, Counted,
            e => KvProps.Color(KvProps.ChildOrNull(Read(e), group), field, def(e), defAlpha),
            (e, c) => KvProps.SetColor(KvProps.Child(Counter(e), group), field, c));

    private static JObject Read(KvElement e) => KvProps.ChildOrNull(e.Raw, "counter");
    private static JObject Counter(KvElement e) => KvProps.Child(e.Raw, "counter");
    private static JObject AnimOrNull(KvElement e) => KvProps.ChildOrNull(Read(e), "animation");
    private static JObject Anim(KvElement e) => KvProps.Child(Counter(e), "animation");

    private static string CounterAlignKey(string s) => s switch {
        "bottom" => "KVI_CALIGN_BOTTOM",
        "left" => "KVI_CALIGN_LEFT",
        "right" => "KVI_CALIGN_RIGHT",
        _ => "KVI_CALIGN_TOP",
    };
}
#endif
