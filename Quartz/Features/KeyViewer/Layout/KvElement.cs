using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Layout;
internal enum KvElementKind {
    Key,
    Stat,
    Graph,
    /// <summary>HID axis element. Quartz does not render these, but preserves them so a
    /// DM Note preset that uses knobs survives an edit-and-export round trip.</summary>
    Knob,
}
/// <summary>
/// One editable element, backed by its source DM Note position object.
///
/// Every accessor reads and writes <see cref="Raw"/> in place rather than projecting into a
/// typed struct and re-serializing. That is deliberate and load-bearing: a DM Note position
/// object carries ~30 fields Quartz neither renders nor understands (sounds, note borders,
/// counter animation curves, font weight/style, layer + group ids, plugin state). DM Note's
/// loader ignores unknown keys instead of erroring and has no version field, so anything we
/// fail to round-trip is silently destroyed with no way to detect it. Editing the backing
/// object means unknown fields survive by construction, including fields added by DM Note
/// versions that postdate this code.
/// </summary>
internal sealed partial class KvElement {
    /// <summary>The backing DM Note position object. Edits write through to this.</summary>
    internal JObject Raw { get; }
    /// <summary>
    /// The object <see cref="Raw"/> was found in, which is <see cref="Raw"/> itself unless the
    /// source nested its geometry under "position". Stat and graph discriminators (statType,
    /// graphType) sit on the outer object in that shape, so a reader after one has to look
    /// here first and fall back to <see cref="Raw"/>.
    /// </summary>
    internal JObject Container =>
        Raw.Parent is JProperty { Name: "position" } prop && prop.Parent is JObject outer ? outer : Raw;
    internal KvElementKind Kind { get; }
    /// <summary>
    /// DM Note globalKey for a <see cref="KvElementKind.Key"/>, mirrored from the parallel
    /// keys[tab] array. <see cref="KvDocument"/> owns writing it back — the position object
    /// itself never stores the binding.
    /// </summary>
    internal string GlobalKey { get; set; } = "";
    private KvElement(JObject raw, KvElementKind kind) {
        Raw = raw;
        Kind = kind;
    }
    /// <summary>
    /// Wrap an existing position object, backfilling only the fields DM Note's Rust
    /// deserializer marks mandatory. Everything else is left exactly as authored.
    /// </summary>
    internal static KvElement Wrap(JObject raw, KvElementKind kind, string globalKey = "") {
        EnsureRequired(raw);
        return new KvElement(raw, kind) { GlobalKey = globalKey ?? "" };
    }
    /// <summary>
    /// DM Note's KeyPosition struct leaves these seven without serde defaults, so a position
    /// object missing any one of them fails the whole preset load with "invalid-preset" —
    /// not just that element. Anything Quartz authors or repairs must carry them.
    /// </summary>
    private static void EnsureRequired(JObject raw) {
        if(raw == null) return;
        if(raw["dx"] == null) raw["dx"] = 0f;
        if(raw["dy"] == null) raw["dy"] = 0f;
        if(raw["width"] == null) raw["width"] = 60f;
        if(raw["height"] == null) raw["height"] = 60f;
        if(raw["count"] == null) raw["count"] = 0;
        if(raw["noteColor"] == null) raw["noteColor"] = "#FFFFFF";
        if(raw["noteOpacity"] == null) raw["noteOpacity"] = 80;
    }
    private float Num(string key, float fallback) {
        JToken t = Raw[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try { return t.ToObject<float>(); } catch { return fallback; }
    }
    private bool Flag(string key, bool fallback) {
        JToken t = Raw[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try { return t.ToObject<bool>(); } catch { return fallback; }
    }
    private string Str(string key, string fallback) {
        JToken t = Raw[key];
        return t == null || t.Type == JTokenType.Null ? fallback : t.ToString();
    }
    internal float X {
        get => Num("dx", 0f);
        set => Raw["dx"] = Clamp(value);
    }
    internal float Y {
        get => Num("dy", 0f);
        set => Raw["dy"] = Clamp(value);
    }
    internal float W {
        get => Num("width", DefaultW);
        set => Raw["width"] = Math.Max(MinSize, value);
    }
    internal float H {
        get => Num("height", DefaultH);
        set => Raw["height"] = Math.Max(MinSize, value);
    }
    /// <summary>
    /// Painter order. Read as a float for convenience, but WRITTEN AS AN INTEGER: DM Note declares
    /// zIndex as i32, and serde rejects a float for an integer field — which fails the whole preset
    /// load with "invalid-preset", not just this element. A float here makes every export Quartz
    /// writes unopenable in DM Note.
    /// </summary>
    internal float Z {
        get => Num("zIndex", 0f);
        set => Raw["zIndex"] = (int)Math.Round(value);
    }
    internal bool Hidden {
        get => Flag("hidden", false);
        set => Raw["hidden"] = value;
    }
    /// <summary>Live press counter. DM Note persists this into presets; it is not cosmetic.</summary>
    internal int Count {
        get => Math.Max(0, (int)Math.Round(Num("count", 0f)));
        set => Raw["count"] = Math.Max(0, value);
    }
    internal string DisplayText {
        get => Str("displayText", "");
        set {
            if(string.IsNullOrEmpty(value)) Raw.Remove("displayText");
            else Raw["displayText"] = value;
        }
    }
    /// <summary>
    /// Discriminator on stat and graph elements.
    ///
    /// The readers disagree about where it lives: the stat path looks at the outer object first
    /// and only then at the geometry object, while the graph path reads the geometry object
    /// alone. On the flat shape those are the same object, but a source that nested its
    /// geometry under "position" has two places to disagree — so the read prefers the outer,
    /// matching the stat path, and the write sets both. Writing only <see cref="Raw"/> let a
    /// stale outer value win silently.
    /// </summary>
    internal string StatType {
        get {
            JObject outer = Container;
            if(!ReferenceEquals(outer, Raw)) {
                JToken t = outer["statType"];
                if(t != null && t.Type != JTokenType.Null && t.ToString().Length > 0) return t.ToString();
            }
            return Str("statType", "");
        }
        set {
            Raw["statType"] = value;
            JObject outer = Container;
            if(!ReferenceEquals(outer, Raw)) outer["statType"] = value;
        }
    }
    // ---- Quartz-only extensions -------------------------------------------------
    // DM Note has no concept of these and its loader ignores unknown keys, so they ride
    // along through a DM Note round trip untouched. Verified absent from DM Note's source.
    /// <summary>
    /// Replaces the old foot-key total exclusion. Foot keys are ordinary elements that
    /// happen to sit lower and not contribute to the total counter.
    /// </summary>
    internal bool CountInTotal {
        get => Flag("quartzCountInTotal", true);
        set {
            if(value) Raw.Remove("quartzCountInTotal");
            else Raw["quartzCountInTotal"] = false;
        }
    }
    /// <summary>
    /// Show this key's presses-per-second in its counter instead of its running total. Simple
    /// mode's own PerKeyKps is one switch over every key; here each element answers for itself.
    ///
    /// Only a key reads this. A stat already picks its readout with statType, and a graph draws
    /// no counter at all.
    /// </summary>
    internal bool PerKeyKps {
        get => Flag("quartzPerKeyKps", false);
        set {
            if(value) Raw["quartzPerKeyKps"] = true;
            else Raw.Remove("quartzPerKeyKps");
        }
    }
    /// <summary>
    /// Marks a member of the generated foot row, so it can be found again to regrow or drop it.
    ///
    /// Nothing else identifies one. A foot key is an ordinary element that happens to have its
    /// counter off, not feed the total and spawn no note — and every one of those is a setting the
    /// user can apply to a key they placed themselves, so inferring the row from them would delete
    /// hand-authored work. Only <see cref="KvPresets.AppendFootRow"/> sets this.
    /// </summary>
    internal bool Foot {
        get => Flag("quartzFoot", false);
        set {
            if(value) Raw["quartzFoot"] = true;
            else Raw.Remove("quartzFoot");
        }
    }
    internal string GhostKey {
        get => Str("ghostKey", "");
        set {
            if(string.IsNullOrEmpty(value)) Raw.Remove("ghostKey");
            else Raw["ghostKey"] = value;
        }
    }
    // ---- geometry ---------------------------------------------------------------
    /// <summary>Matches DM Note's ResizeHandles MIN_SIZE.</summary>
    internal const float MinSize = 10f;
    /// <summary>Matches DM Note's MIN_GRID_POSITION / MAX_GRID_POSITION.</summary>
    internal const float MinPosition = -8000f;
    internal const float MaxPosition = 8000f;
    private float DefaultW => Kind == KvElementKind.Graph ? 200f : 60f;
    private float DefaultH => Kind == KvElementKind.Graph ? 100f : 60f;
    private static float Clamp(float v) => Math.Clamp(v, MinPosition, MaxPosition);
    internal void MoveTo(float x, float y) {
        X = x;
        Y = y;
    }
    internal void Resize(float w, float h) {
        W = w;
        H = h;
    }
    /// <summary>Deep copy, including every unmodelled field.</summary>
    internal KvElement Clone() => new((JObject)Raw.DeepClone(), Kind) { GlobalKey = GlobalKey };
}
