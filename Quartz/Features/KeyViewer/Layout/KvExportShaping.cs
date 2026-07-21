using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Layout;
internal enum KvExportFormat {
    Quartz,
    DmNote,
}
internal static class KvExportShaping {
    internal const string QuartzPrefix = "quartz";
    private static readonly string[] DmNoteStatTypes = ["kps", "total"];
    internal static void Shape(JObject root, KvExportFormat format) {
        if(root == null || format != KvExportFormat.DmNote) return;
        DowngradeStatsForDmNote(root);
        StripQuartzKeys(root);
    }
    internal const string GapStats = "stats-avgmax";
    internal const string GapGhostKeys = "ghost-keys";
    internal const string GapPressedLabels = "pressed-labels";
    internal const string GapHiddenLabels = "hidden-labels";
    internal const string GapCounterWhilePressed = "counter-while-pressed";
    internal const string GapCountInTotal = "count-in-total";
    internal const string GapPerKeyKps = "per-key-kps";
    internal const string GapFootRows = "foot-rows";
    internal const string GapPressScale = "press-scale";
    internal const string GapNoteShadows = "note-shadows";
    internal const string GapOther = "other";
    private static readonly string[] GapOrder = [
        GapStats, GapGhostKeys, GapPressedLabels, GapHiddenLabels, GapCounterWhilePressed,
        GapCountInTotal, GapPerKeyKps, GapFootRows, GapPressScale, GapNoteShadows, GapOther,
    ];
    private static readonly string[] BookkeepingKeys = ["quartzRenderAnchors", "quartzSettings", "quartzFormat"];
    internal static List<string> DetectDmNoteGaps(JObject root) {
        HashSet<string> found = [];
        if(root != null) {
            foreach(string table in new[] { "keyPositions", "statPositions", "graphPositions", "knobPositions" }) {
                if(root[table] is not JObject byTab) continue;
                foreach(JProperty tab in byTab.Properties()) {
                    if(tab.Value is not JArray arr) continue;
                    foreach(JToken entry in arr) {
                        if(entry is not JObject outer) continue;
                        Inspect(outer, table == "statPositions", found);
                        if(outer["position"] is JObject inner) Inspect(inner, table == "statPositions", found);
                    }
                }
            }
        }
        List<string> ordered = [];
        foreach(string gap in GapOrder)
            if(found.Contains(gap)) ordered.Add(gap);
        return ordered;
    }
    private static void Inspect(JObject element, bool stat, HashSet<string> found) {
        if(stat && !IsDmNoteStat(element["statType"]?.ToString())) found.Add(GapStats);
        if(HasText(element["ghostKey"])) found.Add(GapGhostKeys);
        foreach(JProperty prop in element.Properties()) {
            if(!prop.Name.StartsWith(QuartzPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if(Array.IndexOf(BookkeepingKeys, prop.Name) >= 0) continue;
            switch(prop.Name) {
                case "quartzPressedText":
                    if(HasText(prop.Value)) found.Add(GapPressedLabels);
                    break;
                case "quartzLabelEnabled":
                    found.Add(GapHiddenLabels);
                    break;
                case "quartzCounterShowWhilePressed":
                    found.Add(GapCounterWhilePressed);
                    break;
                case "quartzCountInTotal":
                    found.Add(GapCountInTotal);
                    break;
                case "quartzPerKeyKps":
                    found.Add(GapPerKeyKps);
                    break;
                case "quartzFoot":
                    found.Add(GapFootRows);
                    break;
                case "quartzPressScale":
                    found.Add(GapPressScale);
                    break;
                case "quartzNoteShadow":
                    if(prop.Value.Type == JTokenType.Boolean && prop.Value.Value<bool>()) found.Add(GapNoteShadows);
                    break;
                case "quartzNoteShadowColor":
                case "quartzNoteShadowX":
                case "quartzNoteShadowY":
                    break;
                default:
                    found.Add(GapOther);
                    break;
            }
        }
    }
    private static bool HasText(JToken token) =>
        token != null && token.Type == JTokenType.String && token.ToString().Length > 0;
    internal static void DowngradeStatsForDmNote(JObject root) => ForEachStat(root, (outer, inner) => {
        if(IsDmNoteStat(ReadStatType(outer, inner))) return;
        if(outer?["statType"] != null) outer["statType"] = "kps";
        if(inner?["statType"] != null) inner["statType"] = "kps";
        if(outer?["statType"] == null && inner?["statType"] == null && outer != null) outer["statType"] = "kps";
    });
    internal static void StripQuartzKeys(JToken node) {
        switch(node) {
            case JObject obj:
                List<JProperty> doomed = null;
                foreach(JProperty prop in obj.Properties()) {
                    if(prop.Name.StartsWith(QuartzPrefix, StringComparison.OrdinalIgnoreCase)) (doomed ??= []).Add(prop);
                    else StripQuartzKeys(prop.Value);
                }
                if(doomed != null) foreach(JProperty prop in doomed) prop.Remove();
                break;
            case JArray arr:
                foreach(JToken item in arr) StripQuartzKeys(item);
                break;
        }
    }
    private static void ForEachStat(JObject root, Action<JObject, JObject> visit) {
        if(root?["statPositions"] is not JObject byTab) return;
        foreach(JProperty tab in byTab.Properties()) {
            if(tab.Value is not JArray arr) continue;
            foreach(JToken entry in arr) {
                if(entry is not JObject outer) continue;
                visit(outer, outer["position"] as JObject);
            }
        }
    }
    private static string ReadStatType(JObject outer, JObject inner) {
        string type = outer?["statType"]?.ToString();
        return string.IsNullOrEmpty(type) ? inner?["statType"]?.ToString() : type;
    }
    private static bool IsDmNoteStat(string type) {
        if(string.IsNullOrEmpty(type)) return true;
        foreach(string legal in DmNoteStatTypes)
            if(type.Equals(legal, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
