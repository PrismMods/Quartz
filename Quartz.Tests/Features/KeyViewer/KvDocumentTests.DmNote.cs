using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static partial class KvDocumentTests {
    private static readonly string[] RequiredEverywhere =
        ["dx", "dy", "width", "height", "count", "noteColor", "noteOpacity"];
    private static readonly string[] IntegerFields =
        ["count", "noteOpacity", "noteGlowOpacity", "noteBorderOpacity", "zIndex"];
    private static readonly Dictionary<string, string[]> EnumFields = new() {
        ["noteAlignment"] = ["left", "center", "right"],
    };
    private static readonly string[] DmNoteStatTypes = ["kps", "total"];
    private static readonly Dictionary<string, string[]> CounterEnumFields = new() {
        ["placement"] = ["inside", "outside"],
        ["align"] = ["top", "bottom", "left", "right"],
        ["alignMode"] = ["center", "between"],
    };
    private static string DmNoteImportViolation(string json) {
        JObject root = JObject.Parse(json);
        JObject keys = root["keys"] as JObject;
        foreach((string table, string[] disc) in new (string, string[])[] {
            ("keyPositions", []),
            ("statPositions", ["statType"]),
            ("graphPositions", ["statType", "graphType", "graphSpeed", "graphColor"]),
            ("knobPositions", []),
        }) {
            if(root[table] is not JObject byTab) continue;
            foreach(JProperty tab in byTab.Properties()) {
                if(tab.Value is not JArray arr) continue;
                for(int i = 0; i < arr.Count; i++) {
                    JObject outer = arr[i] as JObject;
                    JObject p = outer?["position"] as JObject ?? outer;
                    if(p == null) return $"{table}[{tab.Name}][{i}] is not an object";
                    string where = $"{table}[{tab.Name}][{i}]";
                    foreach(string req in RequiredEverywhere)
                        if(p[req] == null) return $"{where} missing required '{req}'";
                    foreach(string d in disc)
                        if((outer ?? p)[d] == null) return $"{where} missing '{d}'";
                    if(table == "statPositions") {
                        string statType = ((outer ?? p)["statType"] ?? p["statType"])?.ToString();
                        if(statType != null && Array.IndexOf(DmNoteStatTypes, statType) < 0)
                            return $"{where}.statType='{statType}' is not a legal DM Note stat readout";
                    }
                    foreach(string f in IntegerFields) {
                        JToken t = p[f];
                        if(t != null && t.Type == JTokenType.Float)
                            return $"{where}.{f} is a float ({t}); DM Note declares it an integer";
                    }
                    foreach((string f, string[] legal) in EnumFields) {
                        JToken t = p[f];
                        if(t != null && Array.IndexOf(legal, t.ToString()) < 0)
                            return $"{where}.{f}='{t}' is not a legal DM Note value";
                    }
                    if(p["counter"] is JObject counter)
                        foreach((string f, string[] legal) in CounterEnumFields) {
                            JToken t = counter[f];
                            if(t != null && Array.IndexOf(legal, t.ToString()) < 0)
                                return $"{where}.counter.{f}='{t}' is not a legal DM Note value";
                        }
                    JToken nc = p["noteColor"];
                    if(nc is { Type: not JTokenType.String } and JObject nco
                       && (nco["top"] == null || nco["bottom"] == null))
                        return $"{where}.noteColor object is not a valid gradient";
                }
                if(table == "keyPositions" && keys?[tab.Name] is JArray names && names.Count != arr.Count)
                    return $"keys[{tab.Name}]={names.Count} but keyPositions={arr.Count} (not parallel)";
            }
        }
        return null;
    }
    public static void TestGeneratedLayoutPassesDmNoteImport() {
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        KvElement key = KvElement.Wrap([], KvElementKind.Key, "Z");
        key.MoveTo(0f, 0f);
        key.Raw["noteColor"] = new JObject { ["type"] = "gradient", ["top"] = "#FFF", ["bottom"] = "#000" };
        key.Raw["counter"] = new JObject { ["enabled"] = true, ["align"] = "bottom", ["placement"] = "inside" };
        key.Z = 0;
        doc.Add(tab, key);
        KvElement stat = KvElement.Wrap([], KvElementKind.Stat, "");
        stat.StatType = "kps";
        stat.MoveTo(0f, 60f);
        stat.Z = 1;
        doc.Add(tab, stat);
        KvElement graph = KvElement.Wrap([], KvElementKind.Graph, "");
        graph.StatType = "kpsAvg";
        graph.Raw["graphType"] = "line";
        graph.Raw["graphSpeed"] = 1000;
        graph.Raw["graphColor"] = "#86EFAC";
        graph.MoveTo(0f, 120f);
        graph.Z = 2;
        doc.Add(tab, graph);
        string violation = DmNoteImportViolation(doc.ToJson());
        Assert(violation == null, "generated layout must pass DM Note import, but: " + violation);
    }
    public static void TestDmNoteImportValidatorCatchesFloatIntField() {
        string bad = """
            {"selectedKeyType":"t","keys":{"t":["Z"]},
             "keyPositions":{"t":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,
               "noteColor":"#FFF","noteOpacity":80,"zIndex":0.5}]}}
            """;
        Assert(DmNoteImportViolation(bad) != null, "a float in an integer field must be flagged");
        Assert(DmNoteImportViolation(Preset) == null, "the fixture preset satisfies the import rules");
    }
    public static void TestDmNoteGapsReportOnlyWhatIsUsed() {
        KvDocument plain = KvDocument.Empty();
        string plainTab = plain.SelectedTab;
        plain.Add(plainTab, KvElement.Wrap([], KvElementKind.Key, "Z"));
        KvElement kps = KvElement.Wrap([], KvElementKind.Stat, "");
        kps.StatType = "total";
        plain.Add(plainTab, kps);
        plain.SetRenderAnchor(plainTab, 12f, 0f);
        List<string> none = KvExportShaping.DetectDmNoteGaps(JObject.Parse(plain.ToJson()));
        Assert(none.Count == 0, "a DM-Note-expressible layout reports no gaps, but got: " + string.Join(", ", none));
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        KvElement key = KvElement.Wrap([], KvElementKind.Key, "Z");
        key.PressedText = "!";
        key.GhostKey = "X";
        key.CountInTotal = false;
        doc.Add(tab, key);
        KvElement stat = KvElement.Wrap([], KvElementKind.Stat, "");
        stat.StatType = "kpsMax";
        doc.Add(tab, stat);
        List<string> gaps = KvExportShaping.DetectDmNoteGaps(JObject.Parse(doc.ToJson()));
        Assert(string.Join(",", gaps) == string.Join(",", new[] {
            KvExportShaping.GapStats, KvExportShaping.GapGhostKeys,
            KvExportShaping.GapPressedLabels, KvExportShaping.GapCountInTotal,
        }), "the gap list names what is used, in report order, but got: " + string.Join(", ", gaps));
    }
    public static void TestDmNoteGapsIgnoreDisabledNoteShadow() {
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        KvElement key = KvElement.Wrap([], KvElementKind.Key, "Z");
        key.Raw["quartzNoteShadow"] = false;
        key.Raw["quartzNoteShadowColor"] = "rgba(0, 0, 0, 0.5)";
        doc.Add(tab, key);
        Assert(KvExportShaping.DetectDmNoteGaps(JObject.Parse(doc.ToJson())).Count == 0,
            "a note shadow that is switched off is not a gap");
        key.Raw["quartzNoteShadow"] = true;
        Assert(KvExportShaping.DetectDmNoteGaps(JObject.Parse(doc.ToJson()))
            is [KvExportShaping.GapNoteShadows], "a note shadow that is on is");
    }
}
