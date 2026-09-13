using Newtonsoft.Json.Linq;

namespace Quartz.Features.KeyViewer.Layout;

internal readonly struct DmGradientStop {
    internal string Color { get; }
    internal float Position { get; }

    internal DmGradientStop(string color, float position) {
        Color = color;
        Position = position;
    }
}

/// <summary>
/// Parsed DM Note v2 gradient sibling. Kept free of Unity types so preset
/// validation and renderer conversion share one tolerant boundary.
/// </summary>
internal sealed class DmGradientSpec {
    internal const int MinStops = 2;
    internal const int MaxStops = 8;
    internal float Angle { get; }
    internal DmGradientStop[] Stops { get; }

    private DmGradientSpec(float angle, DmGradientStop[] stops) {
        Angle = angle;
        Stops = stops;
    }

    internal static bool TryParse(JToken token, out DmGradientSpec gradient) {
        gradient = null;
        if(token is not JObject obj || obj["stops"] is not JArray rawStops
            || rawStops.Count < MinStops) return false;
        float angle = Number(obj["angle"], 90f);
        if(!float.IsFinite(angle)) return false;
        angle %= 360f;
        if(angle < 0f) angle += 360f;
        List<(DmGradientStop stop, int order)> parsed = [];
        int count = Math.Min(rawStops.Count, MaxStops);
        for(int i = 0; i < count; i++) {
            if(rawStops[i] is not JObject raw) return false;
            string color = raw["color"]?.Type == JTokenType.String ? raw["color"]?.ToString() : null;
            float position = Number(raw["pos"], float.NaN);
            if(string.IsNullOrWhiteSpace(color) || !float.IsFinite(position)) return false;
            parsed.Add((new DmGradientStop(color, Math.Clamp(position, 0f, 1f)), i));
        }
        parsed.Sort((a, b) => {
            int byPosition = a.stop.Position.CompareTo(b.stop.Position);
            return byPosition != 0 ? byPosition : a.order.CompareTo(b.order);
        });
        gradient = new DmGradientSpec(angle, [.. parsed.Select(p => p.stop)]);
        return true;
    }

    private static float Number(JToken token, float fallback) {
        if(token == null || token.Type == JTokenType.Null) return fallback;
        try { return token.ToObject<float>(); }
        catch(Exception e) { Quartz.Core.Diag.Ignore(e); return fallback; }
    }
}
