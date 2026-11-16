#if !IL2CPP && !QUARTZ_KEYVIEWER
using Facet;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Editor;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
// The Element tab: binding, stat or graph, the hidden flag, label, press count and geometry, as the classic Element tab.
internal static partial class KvInspectorPages {
    private static readonly string[] StatTypes = ["kps", "kpsAvg", "kpsMax", "total"];
    private static readonly string[] GraphTypes = ["line", "bar"];

    private static void BuildElement(VisualElement p, KvElement[] batch) {
        KvElement first = batch[0];
        bool single = batch.Length == 1, allKeys = AllOf(batch, KvElementKind.Key);
        VisualElement at = p;
        if(single && first.Kind == KvElementKind.Key) at = Binding(p);
        else if(AllOf(batch, KvElementKind.Stat)) {
            at = Section(p, "KVI_SEC_STAT");
            StatChoice(at);
        }
        else if(AllOf(batch, KvElementKind.Graph)) at = Graph(p);
        Flag(at, "KVI_HIDDEN", false, Sel, e => e.Hidden, (e, v) => e.Hidden = v, "DESC_KVI_HIDDEN");

        VisualElement label = Section(p, "KVI_SEC_LABEL");
        Flag(label, "KVI_LABEL_ENABLED", true, Sel, e => e.LabelEnabled, (e, v) => e.LabelEnabled = v, "DESC_KVI_LABEL_ENABLED", rebuild: true);
        Func<bool> anyLabel = () => AnyOf(Sel, e => e.LabelEnabled);
        Text(label, "KVI_DISPLAY", Sel, e => e.DisplayText, (e, v) => e.DisplayText = v, 24, visible: anyLabel);
        if(allKeys) Text(label, "KVI_DISPLAY_PRESSED", Sel, e => e.PressedText, (e, v) => e.PressedText = v, 24, visible: anyLabel);

        if(allKeys) {
            VisualElement count = Section(p, "KVI_SEC_COUNT");
            Flag(count, "KVI_COUNTINTOTAL", true, Sel, e => e.CountInTotal, (e, v) => e.CountInTotal = v, "DESC_KVI_COUNTINTOTAL");
            if(single) Field(count, "KVI_COUNT", e => e.Count, (e, v) => e.Count = Mathf.RoundToInt(v));
            Controls.Action(count, "", "KVI_COUNT_RESET", ResetCount, o => o.Id = "KVI_COUNT_RESET");
        }

        VisualElement geometry = Section(p, "KVI_SEC_GEOMETRY");
        if(single) {
            (VisualElement x, VisualElement y) = Pair(geometry);
            Field(x, "KVI_X", e => e.X, (e, v) => e.X = v);
            Field(y, "KVI_Y", e => e.Y, (e, v) => e.Y = v);
            (VisualElement w, VisualElement h) = Pair(geometry);
            Field(w, "KVI_W", e => e.W, (e, v) => e.W = v);
            Field(h, "KVI_H", e => e.H, (e, v) => e.H = v);
        }
        else {
            (VisualElement w, VisualElement h) = Pair(geometry);
            Num(w, "KVI_W", first.W, KvElement.MinSize, 500f, "0 px", 1f, Sel, e => e.W, (e, v) => e.W = v);
            Num(h, "KVI_H", first.H, KvElement.MinSize, 500f, "0 px", 1f, Sel, e => e.H, (e, v) => e.H = v);
        }
    }

    private static void StatChoice(VisualElement p) =>
        Pick(p, "kv-stat-type", StatTypes, StatKey, StatTypes[0], Sel, e => e.StatType, (e, v) => e.StatType = v);

    private static string StatKey(string s) => s switch {
        "kpsAvg" => "KVI_STAT_KPSAVG",
        "kpsMax" => "KVI_STAT_KPSMAX",
        "total" => "KVI_STAT_TOTAL",
        _ => "KVI_STAT_KPS",
    };

    // ---- the key and the ghost key: Escape cancels a capture and leaves the binding; the clear button unbinds the ghost

    private static readonly Action<Action<string>> Capture = done =>
        KeyCaptureService.Start(k => done(k == KeyCode.None ? global::Facet.Input.ChordCancel : k.ToString()), null);

    private static VisualElement Binding(VisualElement p) {
        VisualElement s = Section(p, "KVI_SEC_BINDING");
        Controls.Key(s, "KVI_REBIND", Many(Sel, e => KeyName(e.KeyCodeValue, e.GlobalKey), (e, v) => {
            if(Parse(v) is { } k) e.BindKey(k);
        }), o => KeyOpt(o, "KVI_REBIND", "DESC_KVI_REBIND"));
        Control<string> ghost = Controls.Key(s, "KVI_GHOST_SET", Many(Sel, e => KeyName(e.GhostKeyCodeValue, e.GhostKey), (e, v) => {
            if(string.IsNullOrEmpty(v)) e.GhostKey = "";
            else if(Parse(v) is { } k) e.BindGhostKey(k);
        }), o => KeyOpt(o, "KVI_GHOST_SET", "DESC_KVI_GHOST_SET"));
        Controls.Action(s, "", "KVI_GHOST_CLEAR", () => ghost.Set(""), o => o.Id = "KVI_GHOST_CLEAR");
        return s;
    }

    private static void KeyOpt(KeyOptions o, string id, string tip) {
        Opt(o, id, tip, null);
        o.Capture = Capture;
        o.CancelCapture = KeyCaptureService.Cancel;
    }

    private static string KeyName(KeyCode code, string raw) => code != KeyCode.None ? code.ToString() : raw ?? "";

    private static KeyCode? Parse(string name) =>
        !string.IsNullOrEmpty(name) && Enum.TryParse(name, out KeyCode k) && k != KeyCode.None ? k : null;

    // ---- graph

    private static VisualElement Graph(VisualElement p) {
        VisualElement s = Section(p, "KVI_SEC_GRAPH");
        Pick(s, "kv-graph-type", GraphTypes, g => g == "bar" ? "KVI_GRAPH_BAR" : "KVI_GRAPH_LINE", "line", Sel,
            e => KvProps.Str(e.Raw, "graphType", "line"), (e, v) => e.Raw["graphType"] = v);
        StatChoice(s);
        Colour(s, "KVI_GRAPH_COLOR", KeyViewerOverlay.HexToColor("#86EFAC", 1f), true, Sel,
            e => KvProps.Color(e.Raw, "graphColor", "#86EFAC", 1f), (e, c) => KvProps.SetColor(e.Raw, "graphColor", c));
        Num(s, "KVI_GRAPH_SPEED", 1000f, 500f, 5000f, "0 ms", 50f, Sel,
            e => KvProps.Float(e.Raw, "graphSpeed", 1000f), (e, v) => KvProps.SetInt(e.Raw, "graphSpeed", v));
        Flag(s, "KVI_GRAPH_AVG", true, Sel, e => KvProps.Bool(e.Raw, "showAvgLine", true), (e, v) => e.Raw["showAvgLine"] = v);
        return s;
    }

    // ---- Reset Count: one step that holds each element's tab, key and old count, never the element, so an undo after a
    // restore or a replace still finds it

    private static void ResetCount() {
        KeyViewerOverlay.FlushCounts();   // presses not yet written to the elements are part of what is reset
        KvDocument doc = KvStore.Current;
        string tab = ctx.Tab;
        List<(string Key, int Count)> old = [];
        foreach(KvElement e in Keys())
            if(KvEditOps.Key(doc, tab, e) is { } key) old.Add((key, e.Count));
        if(old.Count == 0) return;
        using(History.Step("KVI_COUNT_RESET", null, History.BoundTo(Doc), QuartzHost.Translate)) {
            History.Record("KVI_COUNT_RESET", null, () => SetCounts(tab, old, false), () => SetCounts(tab, old, true), QuartzHost.Translate);
            SetCounts(tab, old, true);
        }
        ctx.Changed?.Invoke();
    }

    private static void SetCounts(string tab, List<(string Key, int Count)> counts, bool zero) {
        KvDocument doc = KvStore.Current;
        foreach((string key, int count) in counts)
            if(KvEditOps.Find(doc, tab, key) is { } e) e.Count = zero ? 0 : count;
    }
}
#endif
