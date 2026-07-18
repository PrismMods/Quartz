using System.Globalization;
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using UnityEngine;
namespace Quartz.UI.Editor;
/// <summary>
/// Typed access to a DM Note position object for the inspector.
///
/// Every writer edits the backing JObject in place rather than replacing it — see
/// <see cref="Features.KeyViewer.Layout.KvElement"/> for why that is load-bearing. Field names
/// must match <c>KeyViewerOverlay.ParseDmNoteSpec</c> exactly: a name that disagrees with the
/// reader is not an error, it just renders as the default forever.
/// </summary>
internal static class KvProps {
    internal static float Float(JObject o, string key, float fallback) {
        JToken t = o?[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try { return t.ToObject<float>(); } catch { return fallback; }
    }
    internal static int Int(JObject o, string key, int fallback) {
        JToken t = o?[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try { return t.ToObject<int>(); } catch { return fallback; }
    }
    internal static bool Bool(JObject o, string key, bool fallback) {
        JToken t = o?[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try { return t.ToObject<bool>(); } catch { return fallback; }
    }
    internal static string Str(JObject o, string key, string fallback) {
        JToken t = o?[key];
        return t == null || t.Type == JTokenType.Null ? fallback : t.ToString();
    }
    /// <summary>
    /// Child object at <paramref name="key"/>, created when absent. A nested write
    /// (counter.fill.idle) has to materialize its intermediates or it lands nowhere.
    /// </summary>
    internal static JObject Child(JObject o, string key) {
        if(o == null) return null;
        if(o[key] is JObject existing) return existing;
        JObject created = [];
        o[key] = created;
        return created;
    }
    /// <summary>Read-side counterpart to <see cref="Child"/>: never materializes.</summary>
    internal static JObject ChildOrNull(JObject o, string key) => o?[key] as JObject;
    /// <summary>
    /// Write a whole number. serde accepts an integer where DM Note's Rust declares a float,
    /// but rejects a float where it declares an integer — and one rejected field fails the
    /// entire preset load, not just the element carrying it. So every value without a
    /// meaningful fractional part is written as an integer, whichever way the field happens to
    /// be declared over there. Fields KvPresets already proves are floats (dx, dy, width,
    /// height, borderRadius, borderWidth, noteWidth) keep their fractions and are written raw.
    /// </summary>
    internal static void SetInt(JObject o, string key, float v) {
        if(o != null) o[key] = Mathf.RoundToInt(v);
    }
    // ---- colour -----------------------------------------------------------------
    // ParseDmNoteSpec's alpha argument to HexToColor is only a fallback for strings that carry
    // no alpha of their own, and it is not 1 for every field: backgroundColor falls back to
    // 0.9, counter stroke to 0. A bare #RRGGBB has no alpha channel, so writing one would hand
    // the field back whatever its fallback happens to be — an opaque background written as hex
    // would render at 90%, an opaque counter stroke at 0%. Every colour except the note
    // colours therefore serializes as rgba(), at any alpha.
    internal static Color Color(JObject o, string key, string fallbackCss, float fallbackAlpha) =>
        KeyViewerOverlay.HexToColor(Str(o, key, fallbackCss), fallbackAlpha);
    internal static void SetColor(JObject o, string key, Color c) {
        if(o != null) o[key] = ToCss(c);
    }
    /// <summary>Invariant culture is not cosmetic: a comma-decimal locale would emit
    /// "rgba(0, 0, 0, 0,5)", which HexToColor splits on ',' and reads as garbage.</summary>
    internal static string ToCss(Color c) => string.Format(
        CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3})",
        Channel(c.r), Channel(c.g), Channel(c.b), Math.Round(Mathf.Clamp01(c.a), 3));
    /// <summary>
    /// Note colours pair a bare hex with a separate opacity field (noteOpacity /
    /// noteGlowOpacity), which is how DM Note's own editor writes them. An inline rgba() alpha
    /// here would override that opacity in Quartz and be multiplied by it a second time on DM
    /// Note's side, so the two would disagree.
    /// </summary>
    internal static string ToHex(Color c) => string.Format(
        CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", Channel(c.r), Channel(c.g), Channel(c.b));
    private static int Channel(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    // ---- noteColor / noteGlowColor union ------------------------------------------
    // Both are `string | { type: "gradient", top, bottom }`. ToString() on the object form
    // yields JSON text, which HexToColor silently resolves to white rather than throwing, so
    // the shape has to be tested rather than assumed.
    internal static bool IsGradient(JObject o, string key) =>
        o?[key] is JObject g
        && string.Equals(g["type"]?.ToString(), "gradient", StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// The colour to show for <paramref name="key"/>, reading the requested stop when the field
    /// currently holds a gradient and the plain string otherwise.
    /// </summary>
    internal static Color NoteColor(JObject o, string key, string fallbackHex, bool bottom = false) {
        JToken t = o?[key];
        if(t == null || t.Type == JTokenType.Null) return KeyViewerOverlay.HexToColor(fallbackHex, 1f);
        if(t is JObject g) return KeyViewerOverlay.HexToColor(Str(g, bottom ? "bottom" : "top", fallbackHex), 1f);
        return KeyViewerOverlay.HexToColor(t.ToString(), 1f);
    }
    /// <summary>The field's current value as a plain hex, collapsing a gradient to its top stop.</summary>
    internal static string SolidHex(JObject o, string key, string fallbackHex) {
        JToken t = o?[key];
        if(t == null || t.Type == JTokenType.Null) return fallbackHex;
        if(t is JObject g) return Str(g, "top", fallbackHex);
        return t.ToString();
    }
    internal static void SetNoteSolid(JObject o, string key, Color c) {
        if(o == null) return;
        o[key] = ToHex(c);
    }
    /// <summary>
    /// Write one gradient stop, converting the field to gradient form first if it is still a
    /// string. Both stops seed from the current solid so flipping the mode does not move the
    /// colour until a stop is actually edited.
    /// </summary>
    internal static void SetNoteStop(JObject o, string key, bool bottom, Color c, string fallbackHex) {
        JObject g = AsGradient(o, key, fallbackHex);
        if(g == null) return;
        g[bottom ? "bottom" : "top"] = ToHex(c);
    }
    /// <summary>
    /// <paramref name="fallbackHex"/> is what an absent field renders as, which is not always
    /// white: noteGlowColor falls back to the element's noteColor. Seeding the stops from the
    /// literal default instead would snap the colour on a mode flip the user expects to be
    /// invisible.
    /// </summary>
    internal static JObject AsGradient(JObject o, string key, string fallbackHex) {
        if(o == null) return null;
        if(IsGradient(o, key)) return (JObject)o[key];
        string solid = SolidHex(o, key, fallbackHex);
        JObject g = new() { ["type"] = "gradient", ["top"] = solid, ["bottom"] = solid };
        o[key] = g;
        return g;
    }
    /// <summary>Collapse a gradient back to a plain string, keeping the top stop.</summary>
    internal static void MakeSolid(JObject o, string key, string fallbackHex) {
        if(o == null || !IsGradient(o, key)) return;
        o[key] = SolidHex(o, key, fallbackHex);
    }
}
