using Newtonsoft.Json.Linq;
namespace Quartz.Features.Panels;
/// <summary>The spaces drawn around a panel's label separator.</summary>
public enum LabelSeparatorSpacing { Both = 0, After = 1, None = 2 }
/// <summary>How a panel's label separator is drawn, and how a saved panel's spacing is read. Unity-free, so it is unit-tested.</summary>
internal static class PanelSeparator {
    /// <summary>PanelConfig's field, as it is saved.</summary>
    internal const string Key = "LabelSeparatorSpacing";

    /// <summary>Empty draws one space. Both and After pad the trimmed text; text that is all spaces is padded as typed, so a
    /// lone " " still draws three spaces. None draws it exactly as typed.</summary>
    internal static string Draw(string raw, LabelSeparatorSpacing spacing) {
        if(string.IsNullOrEmpty(raw)) return " ";
        if(spacing == LabelSeparatorSpacing.None) return raw;
        string core = raw.Trim();
        if(core.Length == 0) core = raw;
        return spacing == LabelSeparatorSpacing.After ? core + " " : " " + core + " ";
    }

    /// <summary>The stored spacing. A panel saved before the setting (or with a value out of range) gets the one that keeps
    /// its look: one character was drawn padded on both sides, anything else exactly as typed.</summary>
    internal static LabelSeparatorSpacing Read(JToken panel, string raw) =>
        panel is JObject o && o[Key] is JValue { Value: long n } && n is >= 0 and <= 2   // a number too big for a long is a BigInteger: missing too
            ? (LabelSeparatorSpacing)n
            : raw?.Length == 1 ? LabelSeparatorSpacing.Both : LabelSeparatorSpacing.None;
}
