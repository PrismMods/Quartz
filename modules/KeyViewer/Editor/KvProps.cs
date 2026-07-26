using System.Globalization;
using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer;
using UnityEngine;
namespace Quartz.UI.Editor;
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
    internal static JObject Child(JObject o, string key) {
        if(o == null) return null;
        if(o[key] is JObject existing) return existing;
        JObject created = [];
        o[key] = created;
        return created;
    }
    internal static JObject ChildOrNull(JObject o, string key) => o?[key] as JObject;
    internal static void SetInt(JObject o, string key, float v) {
        if(o != null) o[key] = Mathf.RoundToInt(v);
    }
    internal static Color Color(JObject o, string key, string fallbackCss, float fallbackAlpha) =>
        KeyViewerOverlay.HexToColor(Str(o, key, fallbackCss), fallbackAlpha);
    internal static void SetColor(JObject o, string key, Color c) {
        if(o != null) o[key] = ToCss(c);
    }
    internal static string ToCss(Color c) => string.Format(
        CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3})",
        Channel(c.r), Channel(c.g), Channel(c.b), Math.Round(Mathf.Clamp01(c.a), 3));
    internal static string ToHex(Color c) => string.Format(
        CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", Channel(c.r), Channel(c.g), Channel(c.b));
    private static int Channel(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    internal static bool IsGradient(JObject o, string key) =>
        o?[key] is JObject g
        && string.Equals(g["type"]?.ToString(), "gradient", StringComparison.OrdinalIgnoreCase);
    internal static Color NoteColor(JObject o, string key, string fallbackHex, bool bottom = false) {
        JToken t = o?[key];
        if(t == null || t.Type == JTokenType.Null) return KeyViewerOverlay.HexToColor(fallbackHex, 1f);
        if(t is JObject g) return KeyViewerOverlay.HexToColor(Str(g, bottom ? "bottom" : "top", fallbackHex), 1f);
        return KeyViewerOverlay.HexToColor(t.ToString(), 1f);
    }
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
    internal static void SetNoteStop(JObject o, string key, bool bottom, Color c, string fallbackHex) {
        JObject g = AsGradient(o, key, fallbackHex);
        if(g == null) return;
        g[bottom ? "bottom" : "top"] = ToHex(c);
    }
    internal static JObject AsGradient(JObject o, string key, string fallbackHex) {
        if(o == null) return null;
        if(IsGradient(o, key)) return (JObject)o[key];
        string solid = SolidHex(o, key, fallbackHex);
        JObject g = new() { ["type"] = "gradient", ["top"] = solid, ["bottom"] = solid };
        o[key] = g;
        return g;
    }
    internal static void MakeSolid(JObject o, string key, string fallbackHex) {
        if(o == null || !IsGradient(o, key)) return;
        o[key] = SolidHex(o, key, fallbackHex);
    }
}
