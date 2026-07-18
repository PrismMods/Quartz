using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Editor;
/// <summary>
/// The batch half of the inspector: the subsets a selection resolves to, the mixed-value test
/// every batch row reads through, and the arrange block that only means anything above one
/// element.
///
/// There is no separate batch panel. Every tab builder takes a <see cref="KvElement"/>[] and a
/// single selection is the length-1 case, so a batch row is the single-element row with a write
/// that fans out. That is what keeps the two in step: a row added to a tab is a batch row the
/// same day.
///
/// One consequence is load-bearing. <see cref="Edit"/>, <see cref="Stream"/> and
/// <see cref="Commit"/> snapshot once per gesture, so a fan-out *inside* one of them is a single
/// undo step for the whole selection — not one per element, and not one per frame of a drag.
/// Writing the loop around them instead would produce N snapshots of a half-applied edit.
/// </summary>
internal sealed partial class KvInspector {
    /// <summary>What DM Note shows wherever a batch has no one value to display.</summary>
    private static string MixedText => MainCore.Tr.Get("KVI_MIXED", "Mixed");
    /// <summary>DM Note's mixed swatch: a grey that reads as "not a colour any of them has".</summary>
    private static readonly Color MixedSwatch = new(0.4f, 0.4f, 0.4f, 1f);
    /// <summary>
    /// True when the elements disagree on <paramref name="read"/>. A length-1 batch — every single
    /// selection — short-circuits to false, which is why the single tabs never mark anything mixed
    /// despite running the same code.
    /// </summary>
    private static bool Mixed<T>(KvElement[] batch, Func<KvElement, T> read) {
        if(batch == null || batch.Length < 2) return false;
        T first = read(batch[0]);
        for(int i = 1; i < batch.Length; i++)
            if(!EqualityComparer<T>.Default.Equals(first, read(batch[i]))) return true;
        return false;
    }
    // DM Note blanks a mixed control and shows "Mixed" in its place, then lets the real value
    // back in the moment the control is touched. Overwriting the readout after construction gives
    // exactly that here for free: every path that changes either widget runs through Set, which
    // rewrites what these two just wrote. So the marker cannot outlive the disagreement it
    // describes, and no widget needs a mixed state of its own.
    private static void MarkMixed(UISlider s) {
        if(s?.ValueText != null) s.ValueText.text = MixedText;
    }
    private static void MarkMixed(UIColorPicker p) {
        if(p == null) return;
        if(p.ValueText != null) p.ValueText.text = MixedText;
        if(p.SwatchImage != null) p.SwatchImage.color = MixedSwatch;
    }
    /// <summary>A slider over a batch: opens on the first element's value, writes to every one.</summary>
    private UISlider Num(
        RectTransform root, List<UIObject> tracked, string label, string id,
        float def, float min, float max, string format, float step,
        KvElement[] batch, Func<KvElement, float> read, Action<KvElement, float> write
    ) {
        UISlider s = Num(
            root, tracked, label, id, def, min, max, read(batch[0]), format, step,
            v => {
                foreach(KvElement el in batch) write(el, v);
            }
        );
        if(Mixed(batch, read)) MarkMixed(s);
        return s;
    }
    /// <summary>
    /// A toggle over a batch. Deliberately unmarked when mixed, which is DM Note's own behaviour:
    /// a switch has nowhere to show a third state, and the alternative — inventing one — would put
    /// a control in this panel that exists nowhere else in the menu.
    /// </summary>
    private UIToggle Flag(
        RectTransform root, List<UIObject> tracked, string label, string id,
        bool def, KvElement[] batch, Func<KvElement, bool> read, Action<KvElement, bool> write,
        bool rebuild = false
    ) => Flag(
        root, tracked, label, id, def, read(batch[0]),
        v => {
            foreach(KvElement el in batch) write(el, v);
        },
        rebuild
    );
    private UIColorPicker Colour(
        RectTransform root, List<UIObject> tracked, string label, string id, Color def,
        KvElement[] batch, Func<KvElement, Color> read, Action<KvElement, Color> write, bool showAlpha
    ) {
        UIColorPicker p = Colour(
            root, tracked, label, id, def, read(batch[0]),
            c => {
                foreach(KvElement el in batch) write(el, c);
            },
            showAlpha
        );
        if(Mixed(batch, read)) MarkMixed(p);
        return p;
    }
    /// <summary>
    /// The option every element is on, or null when they disagree — which
    /// <see cref="GenerateUI.SegmentedControl"/> resolves to no highlighted segment, since it
    /// selects by equality against the values it was given. That is the strip's own way of saying
    /// there is no common answer, and it costs no extra chrome in a pane with none to spare.
    /// </summary>
    private static string MatchMulti(string[] known, KvElement[] batch, Func<KvElement, string> read, string fallback) {
        string first = Match(known, read(batch[0]), fallback);
        for(int i = 1; i < batch.Length; i++)
            if(!string.Equals(Match(known, read(batch[i]), fallback), first, StringComparison.Ordinal)) return null;
        return first;
    }
    // ---- selection subsets -------------------------------------------------------
    /// <summary>
    /// The elements of <paramref name="batch"/> a tab's fields describe. A batch write has to skip
    /// what does not read the field: a note field on a stat, or a counter field on a graph, would
    /// otherwise leave a key in the file that the renderer never looks at and that DM Note's
    /// loader still has to accept.
    /// </summary>
    private static KvElement[] OfKind(KvElement[] batch, KvElementKind kind) {
        List<KvElement> hits = [];
        foreach(KvElement el in batch)
            if(el.Kind == kind) hits.Add(el);
        return [.. hits];
    }
    /// <summary>Keys and stats: everything ParseDmNoteSpec draws a counter for.</summary>
    private static KvElement[] KeyLike(KvElement[] batch) {
        List<KvElement> hits = [];
        foreach(KvElement el in batch)
            if(el.Kind is KvElementKind.Key or KvElementKind.Stat) hits.Add(el);
        return [.. hits];
    }
    /// <summary>
    /// Everything ParseDmNoteSpec reads — which is every kind but a graph, knobs included. A knob
    /// renders nowhere in Quartz, but it carries the same box fields and a single selection has
    /// always offered them, so a batch drops it only where a graph would be dropped too.
    /// </summary>
    private static KvElement[] NonGraphs(KvElement[] batch) {
        List<KvElement> hits = [];
        foreach(KvElement el in batch)
            if(el.Kind != KvElementKind.Graph) hits.Add(el);
        return [.. hits];
    }
    private static bool AllOf(KvElement[] batch, KvElementKind kind) {
        foreach(KvElement el in batch)
            if(el.Kind != kind) return false;
        return true;
    }
    private static bool AllGraphs(KvElement[] batch) => AllOf(batch, KvElementKind.Graph);
    // ---- arrange -----------------------------------------------------------------
    private void BuildArrange(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        Header(root, "KVI_SEC_ARRANGE", "Arrange");
        Arrange(root, tracked, "Align Left", "kvi_snap_left", batch, els => {
            float x = float.MaxValue;
            foreach(KvElement el in els) x = Mathf.Min(x, el.X);
            foreach(KvElement el in els) el.X = x;
        });
        Arrange(root, tracked, "Align Right", "kvi_snap_right", batch, els => {
            float r = float.MinValue;
            foreach(KvElement el in els) r = Mathf.Max(r, el.X + el.W);
            foreach(KvElement el in els) el.X = r - el.W;
        });
        Arrange(root, tracked, "Align Top", "kvi_snap_top", batch, els => {
            float y = float.MaxValue;
            foreach(KvElement el in els) y = Mathf.Min(y, el.Y);
            foreach(KvElement el in els) el.Y = y;
        });
        Arrange(root, tracked, "Align Bottom", "kvi_snap_bottom", batch, els => {
            float b = float.MinValue;
            foreach(KvElement el in els) b = Mathf.Max(b, el.Y + el.H);
            foreach(KvElement el in els) el.Y = b - el.H;
        });
        Arrange(root, tracked, "Space Evenly Across", "kvi_spread_h", batch, els => Spread(els, true));
        Arrange(root, tracked, "Space Evenly Down", "kvi_spread_v", batch, els => Spread(els, false));
    }
    /// <summary>
    /// Even out the gaps between centres, holding the two outermost elements still. Fewer than
    /// three elements have no interior to redistribute, so it is a no-op rather than a move.
    /// </summary>
    private static void Spread(KvElement[] els, bool horizontal) {
        if(els.Length < 3) return;
        KvElement[] ordered = [.. els];
        Array.Sort(ordered, (a, b) => horizontal
            ? (a.X + a.W * 0.5f).CompareTo(b.X + b.W * 0.5f)
            : (a.Y + a.H * 0.5f).CompareTo(b.Y + b.H * 0.5f));
        float Centre(KvElement e) => horizontal ? e.X + e.W * 0.5f : e.Y + e.H * 0.5f;
        float start = Centre(ordered[0]);
        float step = (Centre(ordered[^1]) - start) / (ordered.Length - 1);
        for(int i = 1; i < ordered.Length - 1; i++) {
            float target = start + step * i;
            if(horizontal) ordered[i].X = target - ordered[i].W * 0.5f;
            else ordered[i].Y = target - ordered[i].H * 0.5f;
        }
    }
    private void Arrange(
        RectTransform root, List<UIObject> tracked, string label, string id,
        KvElement[] batch, Action<KvElement[]> apply
    ) => Btn(root, tracked, label, id, () => Edit(() => apply(batch))).SetNeutral();
}
