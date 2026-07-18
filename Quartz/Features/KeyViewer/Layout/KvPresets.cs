using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
/// <summary>
/// The fixed key-viewer styles, as generators that write their layout into a
/// <see cref="KvDocument"/>. A style is a starting point the user then edits freely, not a
/// rendering mode of its own.
///
/// Positions come from <see cref="KeyViewerOverlay"/>'s own geometry helpers rather than a
/// second copy of them, so a generated layout is what the simple-mode overlay draws today,
/// to the pixel, and cannot drift away from it.
/// </summary>
internal static class KvPresets {
    // KeyViewerOverlay's geometry constants are private, so the ones its helpers do not
    // hand back are repeated here. Every position and size still comes from BuildLayout,
    // GridSize and BuildFootLayout — only these cosmetics are duplicated.
    private const float KeyW = 50f;
    private const float KeyRadius = 8f;
    private const float BorderWidth = 2f;
    private const float KeyFontSize = 18f;
    private const float CounterFontSize = 14f;
    private const float StatFontSize = 16f;
    private const float FootFontSize = 13f;
    private const float FootGapAbove = 12f;
    /// <summary>
    /// KeyViewerOverlay.Box.Slot's own value for a stat. Every per-key override falls
    /// through to the global at this slot, which is exactly what a stat renders with.
    /// </summary>
    private const int StatSlot = -1;
    /// <summary>AddFootKey pins Box.RainGroup to 0, the group that spawns no note at all.</summary>
    private const int FootRainGroup = 0;
    /// <summary>
    /// Editor-only 24-key preset. Not a legacy style: the flat slot model tops out at 20 mains
    /// (slot 20 is <see cref="KeyViewerSettings.FootSlotBase"/>), so a seventh legacy style would
    /// collide with the foot range. The generator below builds the extra row directly instead.
    /// </summary>
    internal const int Style24 = 100;
    /// <summary>Style ids in the order the settings UI lists them (ascending key count).</summary>
    internal static readonly int[] Styles = [4, 0, 1, 5, 2, 3, Style24];
    /// <summary>
    /// A settings object holding nothing but the field initializers — the stock palette, the
    /// stock bindings (Key8..Key20, FootKeyDefaults), no labels, no ghost keys, no counts.
    /// Read off a default object instead of re-typed, so a generated layout and a fresh
    /// install cannot disagree.
    ///
    /// This is what "add an 8-key tab" means: the stock 8-key layout, not whatever the user's
    /// own settings happen to hold. <see cref="KvMigration"/> is the only caller allowed to
    /// pass live settings instead.
    /// </summary>
    internal static readonly KeyViewerSettings Stock = new();
    internal static int KeyCount(int style) => style switch {
        0 => 10,
        1 => 12,
        3 => 20,
        4 => 8,
        5 => 14,
        Style24 => 24,
        _ => 16,
    };
    /// <summary>
    /// Stock bindings for the 24-key preset's third row, left to right. Chosen to not collide
    /// with the 16-key stock row above them (JipperKeyViewer's own row reuses keys that stock
    /// layout already binds).
    /// </summary>
    private static readonly int[] ThirdRowKeys = [
        (int)KeyCode.Z, (int)KeyCode.X, (int)KeyCode.C, (int)KeyCode.V,
        (int)KeyCode.N, (int)KeyCode.M, (int)KeyCode.G, (int)KeyCode.K,
    ];
    /// <summary>
    /// The 24-key preset: the stock 16-key layout with one more full row of keys between it and
    /// the stats. Built here rather than as a legacy style — see <see cref="Style24"/>.
    /// <paramref name="onElement"/> sees the 16-key portion exactly as GenerateKeyLayout's would;
    /// the third row binds itself and is not offered (it has no legacy slot to bake from).
    /// </summary>
    internal static void Generate24KeyTab(KvDocument doc, string tab, Action<KvElement, int> onElement = null) {
        if(doc == null) return;
        GenerateKeyLayout(doc, tab, 2, Stock, onElement);
        List<KeyViewerOverlay.KeySlot> slots = [];
        List<KeyViewerOverlay.StatSlot> stats = [];
        KeyViewerOverlay.BuildLayout(2, slots, stats);
        float rowPitch = 0f;
        foreach(KeyViewerOverlay.KeySlot s in slots) rowPitch = Mathf.Max(rowPitch, s.Y);
        // The stats sit directly under the 16-key rows; the new row takes their place and they
        // move down one pitch.
        foreach(KvElement st in doc.Elements(tab, KvElementKind.Stat)) st.Y += rowPitch;
        float z = doc.AllElements(tab).Count;
        int index = 0;
        foreach(KeyViewerOverlay.KeySlot s in slots) {
            // The first row is the template: one slot per column at y 0.
            if(s.Y > 0.5f || index >= ThirdRowKeys.Length) continue;
            KvKeyStyle appearance = StyleFor(Stock, s.Slot, 3);
            KvElement el = Add(doc, tab, KvElementKind.Key, s.X, rowPitch * 2f, s.W, s.H, appearance, z++);
            el.BindKey((KeyCode)ThirdRowKeys[index]);
            index++;
        }
        doc.ReindexZOrder(tab);
    }
    /// <summary>One element's resolved appearance, in the units the DM Note schema stores.</summary>
    internal struct KvKeyStyle {
        internal Color Bg, BgPressed, Outline, OutlinePressed, Text, TextPressed, Note;
        internal float NoteW;
        internal int FontSize, CounterFontSize;
        internal bool Counter;
        internal bool PerKeyKps;
        internal bool NoteEffect;
    }
    /// <summary>
    /// Replace <paramref name="tab"/> with the layout <paramref name="style"/> draws.
    /// <paramref name="conf"/> supplies the palette and defaults to the stock one.
    /// <paramref name="onElement"/> sees every element with its legacy slot
    /// (<see cref="StatSlot"/> for a stat), the last point at which the old flat 36-slot
    /// model can still be applied to it.
    /// </summary>
    internal static void GenerateKeyLayout(KvDocument doc, string tab, int style,
        KeyViewerSettings conf = null, Action<KvElement, int> onElement = null) {
        if(doc == null) return;
        conf ??= Stock;
        style = Mathf.Clamp(style, 0, KeyViewerSettings.MaxStyle);
        doc.EnsureTab(tab);
        doc.Clear(tab);
        List<KeyViewerOverlay.KeySlot> keys = [];
        List<KeyViewerOverlay.StatSlot> stats = [];
        KeyViewerOverlay.BuildLayout(style, keys, stats);
        float gridW = KeyViewerOverlay.GridSize(style).x;
        float z = 0f;
        foreach(KeyViewerOverlay.KeySlot slot in keys) {
            KvKeyStyle appearance = StyleFor(conf, slot.Slot, RainGroupFor(style, slot.Slot));
            KvElement el = Add(doc, tab, KvElementKind.Key, slot.X, slot.Y, slot.W, slot.H, appearance, z++);
            ApplyNoteAlignment(el, gridW);
            onElement?.Invoke(el, slot.Slot);
        }
        foreach(KeyViewerOverlay.StatSlot slot in stats) {
            KvKeyStyle appearance = StyleFor(conf, StatSlot, RainGroupFor(style, StatSlot));
            KvElement el = Add(doc, tab, KvElementKind.Stat, slot.X, slot.Y, slot.W, slot.H, appearance, z++);
            el.StatType = slot.Total ? "total" : "kps";
            el.DisplayText = slot.Total ? "Total" : "KPS";
            ApplyStatCounter(el, style, conf.StatsTogether);
            onElement?.Invoke(el, StatSlot);
        }
        doc.ReindexZOrder(tab);
    }
    /// <summary>
    /// Append the foot row below whatever already sits on <paramref name="tab"/>. The foot
    /// keys were a second, separately positioned overlay; here they are ordinary elements
    /// under the grid, the way PageKeyViewer's preview already merges the two.
    /// </summary>
    internal static void AppendFootRow(KvDocument doc, string tab, int footCount,
        KeyViewerSettings conf = null, Action<KvElement, int> onElement = null) {
        if(doc == null || footCount <= 0) return;
        conf ??= Stock;
        doc.EnsureTab(tab);
        List<KeyViewerOverlay.KeySlot> slots = [];
        Vector2 footSize = KeyViewerOverlay.BuildFootLayout(footCount, slots);
        // Measured content rather than GridSize(style): the same number for a generated layout,
        // and still right once the layout has been edited and no style describes it.
        Vector2 content = ContentSize(doc, tab);
        float shiftX = (content.x - footSize.x) * 0.5f;
        float shiftY = content.y + FootGapAbove;
        float z = doc.AllElements(tab).Count;
        foreach(KeyViewerOverlay.KeySlot slot in slots) {
            KvKeyStyle appearance = StyleFor(conf, slot.Slot, FootRainGroup);
            KvElement el = Add(doc, tab, KvElementKind.Key,
                slot.X + shiftX, slot.Y + shiftY, slot.W, slot.H, appearance, z++);
            // Foot-ness is not an element type any more: a foot key is simply one that does
            // not feed the total. This replaces AddKey's `if(!box.IsFoot)` guard.
            el.CountInTotal = false;
            // The renderer never reads this — the line above is what it acts on. It exists so
            // SetFootRow can find this row again; see KvElement.Foot.
            el.Foot = true;
            onElement?.Invoke(el, slot.Slot);
        }
        doc.ReindexZOrder(tab);
    }
    /// <summary>The most foot keys a row can hold, from the legacy FootStyle axis (0..8, doubled).</summary>
    internal static int MaxFootCount => KeyViewerSettings.MaxFootStyle * 2;
    /// <summary>Foot keys on <paramref name="tab"/>. Zero means the row is off.</summary>
    internal static int FootCount(KvDocument doc, string tab) {
        if(doc == null) return 0;
        int count = 0;
        foreach(KvElement el in doc.Elements(tab, KvElementKind.Key))
            if(el.Foot) count++;
        return count;
    }
    /// <summary>Drop the generated foot row, leaving every other element on the tab where it is.</summary>
    internal static bool RemoveFootRow(KvDocument doc, string tab) {
        if(doc == null) return false;
        List<KvElement> foot = [];
        foreach(KvElement el in doc.Elements(tab, KvElementKind.Key))
            if(el.Foot) foot.Add(el);
        if(foot.Count == 0) return false;
        foreach(KvElement el in foot) doc.Remove(tab, el);
        doc.ReindexZOrder(tab);
        return true;
    }
    /// <summary>
    /// Give <paramref name="tab"/> a foot row of <paramref name="footCount"/> keys, or none at 0.
    ///
    /// The old row is removed first, and not only to avoid duplicates: AppendFootRow places itself
    /// under the tab's measured content, so a surviving row would push the new one below it and
    /// every count change would march the foot keys further down the layout.
    /// </summary>
    internal static void SetFootRow(KvDocument doc, string tab, int footCount,
        KeyViewerSettings conf = null, Action<KvElement, int> onElement = null) {
        if(doc == null) return;
        RemoveFootRow(doc, tab);
        AppendFootRow(doc, tab, Mathf.Clamp(footCount, 0, MaxFootCount), conf, onElement);
    }
    /// <summary>
    /// A position object carrying every field DM Note's loader requires plus the ones Quartz
    /// renders. The only place this file names schema fields — ParseDmNoteSpec is the
    /// matching reader, and a name that disagrees with it silently renders as a default.
    /// </summary>
    internal static JObject NewPosition(float x, float y, float w, float h, in KvKeyStyle style) => new() {
        // DM Note's KeyPosition has no serde default for the first seven: one missing field
        // fails the whole preset load, not just this element.
        ["dx"] = x,
        ["dy"] = y,
        ["width"] = w,
        ["height"] = h,
        ["count"] = 0,
        // noteColor stays a bare hex with its alpha in noteOpacity, the way DM Note's own
        // editor writes the pair. An inline rgba() alpha here would be multiplied by
        // noteOpacity a second time on DM Note's side.
        ["noteColor"] = Hex(style.Note),
        ["noteOpacity"] = Percent(style.Note.a),
        // The box colors have no companion opacity field, so they must carry alpha inline.
        ["backgroundColor"] = Rgba(style.Bg),
        ["activeBackgroundColor"] = Rgba(style.BgPressed),
        ["borderColor"] = Rgba(style.Outline),
        ["activeBorderColor"] = Rgba(style.OutlinePressed),
        ["fontColor"] = Rgba(style.Text),
        ["activeFontColor"] = Rgba(style.TextPressed),
        ["borderRadius"] = KeyRadius,
        ["borderWidth"] = BorderWidth,
        ["fontSize"] = style.FontSize,
        ["noteWidth"] = style.NoteW,
        ["noteEffectEnabled"] = style.NoteEffect,
        ["counter"] = new JObject {
            ["enabled"] = style.Counter,
            ["fontSize"] = style.CounterFontSize,
            // AddKey puts the counter under the label; DM Note's own default is "top".
            ["align"] = "bottom",
        },
    };
    /// <summary>
    /// Appearance for an element the user adds by hand. Resolved from the same palette a
    /// generated layout uses, so a new element matches the ones already on the canvas instead
    /// of introducing a third copy of DM Note's defaults.
    ///
    /// Always the stock settings, never the live ones — deliberately, and not overridable.
    /// <see cref="StyleFor"/> also carries the legacy behaviour flags (HideMainKeyCount,
    /// PerKeyKps), which are simple-mode globals the editor does not show. Seeding a hand-added
    /// element from them would give it a mode the user cannot see set and cannot find to unset.
    /// Migration is the only path those flags may travel, because there they describe a layout
    /// the user really did configure.
    /// </summary>
    internal static KvKeyStyle NewElementStyle(bool stat) =>
        StyleFor(Stock, stat ? StatSlot : 0, 1);
    /// <summary>
    /// Appearance for <paramref name="slot"/>, resolving per-key overrides against the
    /// globals exactly as KeyViewerOverlay.ApplyBoxColors does.
    /// </summary>
    private static KvKeyStyle StyleFor(KeyViewerSettings conf, int slot, int rainGroup) {
        bool stat = slot < 0;
        bool foot = slot >= KeyViewerSettings.FootSlotBase;
        float baseFont = foot ? FootFontSize : KeyFontSize;
        float noteW = rainGroup == 1 ? conf.RainWidth : conf.Rain2Width;
        return new KvKeyStyle {
            Bg = conf.PerKeyOr(conf.PerKeyBg, slot, conf.GetBg()),
            BgPressed = conf.PerKeyOr(conf.PerKeyBgPressed, slot, conf.GetBgPressed()),
            Outline = conf.PerKeyOr(conf.PerKeyOutline, slot, conf.GetOutline()),
            OutlinePressed = conf.PerKeyOr(conf.PerKeyOutlinePressed, slot, conf.GetOutlinePressed()),
            Text = conf.PerKeyOr(conf.PerKeyText, slot, conf.GetText()),
            TextPressed = conf.PerKeyOr(conf.PerKeyTextPressed, slot, conf.GetTextPressed()),
            Note = conf.PerKeyOr(conf.PerKeyRain, slot, rainGroup switch {
                1 => conf.GetRain(),
                3 => conf.GetRain3(),
                _ => conf.GetRain2(),
            }),
            // SpawnRain reads a width of ~0 as "one key wide".
            NoteW = noteW <= 0.5f ? KeyW : noteW,
            // AddStat passes StatFontSize straight through: the font scales never applied to
            // a stat, and folding them in now would change what the user sees.
            FontSize = Round(stat ? StatFontSize : baseFont * conf.KeyFontFor(slot)),
            CounterFontSize = Round(stat ? StatFontSize : CounterFontSize * conf.CounterFontFor(slot)),
            // AddFootKey builds no counter at all; AddKey's is gated on HideMainKeyCount.
            Counter = stat || (!foot && !conf.HideMainKeyCount),
            // A stat picks its readout with statType instead, and simple mode's PerKeyKps never
            // reached a foot key: AddKey's !IsFoot guard skips the KpsLog feed, and AddFootKey
            // draws nothing that could show it.
            PerKeyKps = !stat && !foot && conf.PerKeyKps,
            NoteEffect = !foot,
        };
    }
    /// <summary>
    /// Mirrors AddKey's Box.RainGroup, which picks the global note color and width a key
    /// uses. Stats pass <see cref="StatSlot"/> and land in group 1; they never spawn a note,
    /// so the value is inert for them.
    /// </summary>
    private static int RainGroupFor(int style, int slot) =>
        slot < 8 ? 1 : style == 3 && slot >= 16 ? 3 : 2;
    /// <summary>
    /// SpawnRain shifts a multi-column key's note to the grid-facing edge (Box.RainAlign);
    /// DM Note says the same thing with noteAlignment. A one-key-wide box never shifts.
    /// </summary>
    private static void ApplyNoteAlignment(KvElement el, float gridW) {
        if(el.W <= KeyW + 0.5f) return;
        float center = el.X + el.W * 0.5f;
        float mid = gridW * 0.5f;
        if(center < mid - 0.5f) el.Raw["noteAlignment"] = "right";
        else if(center > mid + 0.5f) el.Raw["noteAlignment"] = "left";
    }
    /// <summary>
    /// Reproduce AddStat's caption/value arrangement with DM Note's counter fields. Styles 0
    /// and 1 stack the caption over the value; otherwise StatsTogether draws "KPS  0" as one
    /// centred string, which is precisely ParseDmNoteSpec's InlineStatCounter.
    /// </summary>
    private static void ApplyStatCounter(KvElement el, int style, bool statsTogether) {
        if(el.Raw["counter"] is not JObject counter) return;
        // A stat never registers a press, so its counter shows the idle fill forever — and DM
        // Note's idle fill defaults to gray (rgba(121,121,121,0.9)). In game the value is the
        // same white as the caption, so the fill is pinned to the element's own font colour.
        JToken font = el.Raw["fontColor"];
        JToken activeFont = el.Raw["activeFontColor"];
        counter["fill"] = new JObject {
            ["idle"] = font?.DeepClone() ?? "#FFFFFF",
            ["active"] = (activeFont ?? font)?.DeepClone() ?? "#FFFFFF",
        };
        if(style is 0 or 1) {
            counter["align"] = "bottom";
            return;
        }
        // Caption left, value right, matching AddStat. In DM Note's InsideCounterLayout align
        // "right" orders the pair [name, counter]; "left" would reverse it to [counter, name] and
        // read "0 KPS". alignMode "center" keeps them together; "between" pins them to the box
        // edges, the closest the schema comes to StatsTogether = false.
        counter["align"] = "right";
        if(!statsTogether) counter["alignMode"] = "between";
    }
    private static KvElement Add(KvDocument doc, string tab, KvElementKind kind,
        float x, float y, float w, float h, in KvKeyStyle style, float z) {
        KvElement el = KvElement.Wrap(NewPosition(x, y, w, h, style), kind);
        el.Z = z;
        // Set through the accessor rather than written into NewPosition: a Quartz extension is
        // dropped when it is off, and a literal there would stamp `false` onto every element.
        el.PerKeyKps = style.PerKeyKps;
        doc.Add(tab, el);
        return el;
    }
    private static Vector2 ContentSize(KvDocument doc, string tab) {
        float w = 0f, h = 0f;
        foreach(KvElement el in doc.AllElements(tab)) {
            w = Mathf.Max(w, el.X + el.W);
            h = Mathf.Max(h, el.Y + el.H);
        }
        return new Vector2(w, h);
    }
    // fontSize and noteOpacity are integers in the schema. Writing a float where DM Note's
    // Rust declares an integer fails the whole preset load, so round rather than trust
    // serde to coerce.
    private static int Round(float v) => Mathf.Max(1, Mathf.RoundToInt(v));
    private static int Percent(float a) => Mathf.Clamp(Mathf.RoundToInt(a * 100f), 0, 100);
    private static int Channel(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    private static string Hex(Color c) => string.Format(
        CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", Channel(c.r), Channel(c.g), Channel(c.b));
    private static string Rgba(Color c) => string.Format(
        CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3})",
        Channel(c.r), Channel(c.g), Channel(c.b), Math.Round(Mathf.Clamp01(c.a), 3));
}
