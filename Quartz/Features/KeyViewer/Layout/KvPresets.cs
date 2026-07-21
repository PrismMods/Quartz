using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
internal static partial class KvPresets {
    private const float KeyW = 50f;
    private const float KeyRadius = 8f;
    private const float BorderWidth = 2f;
    private const float KeyFontSize = 18f;
    private const float CounterFontSize = 14f;
    private const float StatFontSize = 16f;
    private const float FootFontSize = 13f;
    private const float FootGapAbove = 12f;
    private const int StatSlot = -1;
    private const int FootRainGroup = 0;
    internal const int Style24 = 100;
    internal const int Style108 = 101;
    internal static readonly int[] Styles = [4, 0, 1, 5, 2, 3, Style24, Style108];
    internal static readonly KeyViewerSettings Stock = new();
    internal static int KeyCount(int style) => style switch {
        0 => 10,
        1 => 12,
        3 => 20,
        4 => 8,
        5 => 14,
        Style24 => 24,
        Style108 => 104,
        _ => 16,
    };
    private static readonly int[] ThirdRowKeys = [
        (int)KeyCode.Z, (int)KeyCode.X, (int)KeyCode.C, (int)KeyCode.V,
        (int)KeyCode.N, (int)KeyCode.M, (int)KeyCode.G, (int)KeyCode.K,
    ];
    internal static void Generate24KeyTab(KvDocument doc, string tab, Action<KvElement, int> onElement = null) {
        if(doc == null) return;
        GenerateKeyLayout(doc, tab, 2, Stock, onElement);
        List<KeyViewerOverlay.KeySlot> slots = [];
        List<KeyViewerOverlay.StatSlot> stats = [];
        KeyViewerOverlay.BuildLayout(2, slots, stats);
        float rowPitch = 0f;
        foreach(KeyViewerOverlay.KeySlot s in slots) rowPitch = Mathf.Max(rowPitch, s.Y);
        foreach(KvElement st in doc.Elements(tab, KvElementKind.Stat)) st.Y += rowPitch;
        float z = doc.AllElements(tab).Count;
        int index = 0;
        foreach(KeyViewerOverlay.KeySlot s in slots) {
            if(s.Y > 0.5f || index >= ThirdRowKeys.Length) continue;
            KvKeyStyle appearance = StyleFor(Stock, s.Slot, 3);
            KvElement el = Add(doc, tab, KvElementKind.Key, s.X, rowPitch * 2f, s.W, s.H, appearance, z++);
            el.BindKey((KeyCode)ThirdRowKeys[index]);
            index++;
        }
        doc.ReindexZOrder(tab);
    }
    internal struct KvKeyStyle {
        internal Color Bg, BgPressed, Outline, OutlinePressed, Text, TextPressed, Note;
        internal float NoteW;
        internal int FontSize, CounterFontSize;
        internal bool Counter;
        internal bool PerKeyKps;
        internal bool NoteEffect;
    }
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
    internal static void AppendFootRow(KvDocument doc, string tab, int footCount,
        KeyViewerSettings conf = null, Action<KvElement, int> onElement = null) {
        if(doc == null || footCount <= 0) return;
        conf ??= Stock;
        doc.EnsureTab(tab);
        List<KeyViewerOverlay.KeySlot> slots = [];
        Vector2 footSize = KeyViewerOverlay.BuildFootLayout(footCount, slots);
        Vector2 content = ContentSize(doc, tab);
        float shiftX = (content.x - footSize.x) * 0.5f;
        float shiftY = content.y + FootGapAbove;
        float z = doc.AllElements(tab).Count;
        foreach(KeyViewerOverlay.KeySlot slot in slots) {
            KvKeyStyle appearance = StyleFor(conf, slot.Slot, FootRainGroup);
            KvElement el = Add(doc, tab, KvElementKind.Key,
                slot.X + shiftX, slot.Y + shiftY, slot.W, slot.H, appearance, z++);
            el.CountInTotal = false;
            el.Foot = true;
            onElement?.Invoke(el, slot.Slot);
        }
        doc.ReindexZOrder(tab);
    }
    internal static int MaxFootCount => KeyViewerSettings.MaxFootStyle * 2;
    internal static int FootCount(KvDocument doc, string tab) {
        if(doc == null) return 0;
        int count = 0;
        foreach(KvElement el in doc.Elements(tab, KvElementKind.Key))
            if(el.Foot) count++;
        return count;
    }
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
    internal static void SetFootRow(KvDocument doc, string tab, int footCount,
        KeyViewerSettings conf = null, Action<KvElement, int> onElement = null) {
        if(doc == null) return;
        RemoveFootRow(doc, tab);
        AppendFootRow(doc, tab, Mathf.Clamp(footCount, 0, MaxFootCount), conf, onElement);
    }
    internal static JObject NewPosition(float x, float y, float w, float h, in KvKeyStyle style) => new() {
        ["dx"] = x,
        ["dy"] = y,
        ["width"] = w,
        ["height"] = h,
        ["count"] = 0,
        ["noteColor"] = Hex(style.Note),
        ["noteOpacity"] = Percent(style.Note.a),
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
            ["align"] = "bottom",
        },
    };
    internal static KvKeyStyle NewElementStyle(bool stat) =>
        StyleFor(Stock, stat ? StatSlot : 0, 1);
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
            NoteW = noteW <= 0.5f ? KeyW : noteW,
            FontSize = Round(stat ? StatFontSize : baseFont * conf.KeyFontFor(slot)),
            CounterFontSize = Round(stat ? StatFontSize : CounterFontSize * conf.CounterFontFor(slot)),
            Counter = stat || (!foot && !conf.HideMainKeyCount),
            PerKeyKps = !stat && !foot && conf.PerKeyKps,
            NoteEffect = !foot,
        };
    }
    private static int RainGroupFor(int style, int slot) =>
        slot < 8 ? 1 : style == 3 && slot >= 16 ? 3 : 2;
    private static void ApplyNoteAlignment(KvElement el, float gridW) {
        if(el.W <= KeyW + 0.5f) return;
        float center = el.X + el.W * 0.5f;
        float mid = gridW * 0.5f;
        if(center < mid - 0.5f) el.Raw["noteAlignment"] = "right";
        else if(center > mid + 0.5f) el.Raw["noteAlignment"] = "left";
    }
    private static void ApplyStatCounter(KvElement el, int style, bool statsTogether) {
        if(el.Raw["counter"] is not JObject counter) return;
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
        counter["align"] = "right";
        if(!statsTogether) counter["alignMode"] = "between";
    }
    private static KvElement Add(KvDocument doc, string tab, KvElementKind kind,
        float x, float y, float w, float h, in KvKeyStyle style, float z) {
        KvElement el = KvElement.Wrap(NewPosition(x, y, w, h, style), kind);
        el.Z = z;
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
    private static int Round(float v) => Mathf.Max(1, Mathf.RoundToInt(v));
    private static int Percent(float a) => Mathf.Clamp(Mathf.RoundToInt(a * 100f), 0, 100);
    private static int Channel(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    private static string Hex(Color c) => string.Format(
        CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", Channel(c.r), Channel(c.g), Channel(c.b));
    private static string Rgba(Color c) => string.Format(
        CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3})",
        Channel(c.r), Channel(c.g), Channel(c.b), Math.Round(Mathf.Clamp01(c.a), 3));
}
