#if !IL2CPP && !QUARTZ_KEYVIEWER
using Facet;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
/// <summary>What the inspector rows need from the model, without naming PrismLib.UI. Its delegates go through static
/// lookups: every row step keeps them for the session, and a closure over a page would keep that page alive.</summary>
internal sealed class KvInspectorContext {
    internal Func<IReadOnlyList<KvElement>> Selection;
    internal Func<IReadOnlyList<KvElement>> All;
    internal Func<KvElement, string> Key;
    internal Func<object> UndoDocument;
    /// <summary>Redraws the selection's boxes, then Committed: after an edit that is not a row's.</summary>
    internal Action Changed;
    /// <summary>Redraws the selection's boxes on the next editor tick: every frame of a row's drag.</summary>
    internal Action Dirty;
    /// <summary>The edited layout, for count steps.</summary>
    internal string Tab;
}

/// <summary>
/// The key viewer editor's inspector: the classic inspector's tabs and rows over Facet rows. Every row writes the whole
/// selection through a Bind.Many keyed by element, so its undo finds the elements again after a restore.
/// </summary>
internal static partial class KvInspectorPages {
    private static KvInspectorContext ctx;

    internal static IList<NavItem> For(IReadOnlyList<KvElement> selection, KvInspectorContext context) {
        ctx = context;
        KvElement[] batch = [.. selection ?? []];
        if(batch.Length == 0)
            return [Page("KVI_TAB_ELEMENT", p => Controls.Info(p, "", () => QuartzHost.Translate("KVI_EMPTY"), o => o.Id = "KVI_EMPTY"))];
        // As classic: any key gives the key tabs, else any stat the stat tabs; rain and counter rows edit only their kinds.
        bool key = batch.Any(e => e.Kind == KvElementKind.Key), stat = batch.Any(e => e.Kind == KvElementKind.Stat);
        List<NavItem> pages = [Page("KVI_TAB_ELEMENT", p => BuildElement(p, batch)), Page("KVI_TAB_STYLE", p => BuildStyle(p, batch))];
        if(key) pages.Add(Page("KVI_TAB_NOTE", p => BuildRain(p, [.. batch.Where(e => e.Kind == KvElementKind.Key)])));
        if(key || stat) pages.Add(Page("KVI_TAB_COUNTER", p => BuildCounter(p, [.. batch.Where(IsKeyLike)])));
        return pages;
    }

    internal static string Title(IReadOnlyList<KvElement> selection) {
        int n = selection?.Count ?? 0;
        if(n == 0) return "";
        if(n > 1) return string.Format(QuartzHost.Translate("KVI_MULTI_TITLE"), n);
        string kind = selection![0].Kind switch {
            KvElementKind.Key => "KVI_KIND_KEY",
            KvElementKind.Stat => "KVI_KIND_STAT",
            KvElementKind.Graph => "KVI_KIND_GRAPH",
            _ => "KVI_KIND_KNOB",
        };
        return string.Format(QuartzHost.Translate("KVI_TITLE"), QuartzHost.Translate(kind));
    }

    /// <summary>Every row Id tab `tab` can show for one element of `kind`, or two when `multi`, rows hidden for now and
    /// choice rows (kv-*) included; empty when the selection has no such tab. Built over throwaway elements into a
    /// detached box, for checks.</summary>
    internal static IReadOnlyList<string> RowIds(KvElementKind kind, bool multi, string tab) {
        KvElement Make() => KvElement.Wrap(KvPresets.NewPosition(0, 0, 60, 60, KvPresets.NewElementStyle(kind != KvElementKind.Key)), kind);
        KvElement[] sel = multi ? [Make(), Make()] : [Make()];
        KvInspectorContext live = ctx;
        try {
            foreach(NavItem page in For(sel, new KvInspectorContext {
                         Selection = () => sel, All = () => sel, Key = e => Array.IndexOf(sel, e).ToString(), UndoDocument = () => sel,
                     })) {
                if(page.Title != tab) continue;
                var box = new VisualElement();
                page.Build(box);
                var names = new List<string>();
                Names(box, names);   // a plain walk: CI's stubs have no UQueryBuilder
                return [.. names.Where(IsRowId).Distinct()];
            }
            return [];
        } finally {
            ctx = live;   // the live inspector's rows read the context at each call
        }
    }

    // A row's own name: a Lang key, or a choice row's kv-* id; not a section, nor a part of a row (KVI_BG-swatch).
    private static void Names(VisualElement v, List<string> into) {
        into.Add(v.name);
        for(int i = 0; i < v.childCount; i++) Names(v[i], into);
    }

    private static bool IsRowId(string n) =>
        n != null && (n.StartsWith("KVI_") && n.IndexOf('-') < 0 && !n.StartsWith("KVI_SEC_") || n.StartsWith("kv-") && !n.EndsWith("-input"));

    // The tab rail's own gap is the only space above a tab's first row: a section opening the tab drops its top margin.
    private static NavItem Page(string title, Action<VisualElement> build) => new(title, p => {
        build(p);
        if(p.childCount > 0) p[0].style.marginTop = 0f;
    });

    // Two rows side by side, each at its usual height: X beside Y, width beside height.
    private static (VisualElement Left, VisualElement Right) Pair(VisualElement p) {
        VisualElement row = Ui.Row(p);
        row.style.alignItems = Align.FlexStart;
        VisualElement a = Ui.Box(row), b = Ui.Box(row);
        foreach(VisualElement half in new[] { a, b }) {
            half.style.flexGrow = 1f;
            half.style.flexBasis = 0f;
            half.style.minWidth = 0f;
        }
        a.style.marginRight = Theme.For(row).Sizes.Gap;
        return (a, b);
    }

    // ---- the selection, whole or by kind; a filtered list is kept while the selection holds the same elements

    private static readonly Func<IReadOnlyList<KvElement>> Sel = () => ctx.Selection();
    private static readonly Subset KeysOnly = new(e => e.Kind == KvElementKind.Key);
    private static readonly Subset KeyLike = new(IsKeyLike);
    private static readonly Subset Boxes = new(e => e.Kind != KvElementKind.Graph);
    private static readonly Func<IReadOnlyList<KvElement>> Keys = () => KeysOnly.Of(ctx.Selection());
    private static readonly Func<IReadOnlyList<KvElement>> Counted = () => KeyLike.Of(ctx.Selection());
    private static readonly Func<IReadOnlyList<KvElement>> NonGraphs = () => Boxes.Of(ctx.Selection());

    private static bool IsKeyLike(KvElement e) => e.Kind is KvElementKind.Key or KvElementKind.Stat;
    private static bool AllOf(KvElement[] batch, KvElementKind kind) => batch.All(e => e.Kind == kind);

    private sealed class Subset(Func<KvElement, bool> keep) {
        private readonly List<KvElement> source = [], hits = [];
        internal IReadOnlyList<KvElement> Of(IReadOnlyList<KvElement> sel) {
            bool same = sel.Count == source.Count;
            for(int i = 0; same && i < sel.Count; i++) same = ReferenceEquals(sel[i], source[i]);
            if(same) return hits;
            source.Clear();
            hits.Clear();
            foreach(KvElement e in sel) {
                source.Add(e);
                if(keep(e)) hits.Add(e);
            }
            return hits;
        }
    }

    // ---- rows: Id and label are the classic row's Lang key; every write redraws the boxes and is one step on the document

    private static readonly Func<KvElement, string> KeyOf = e => ctx.Key(e);
    private static readonly Func<IReadOnlyList<KvElement>> AllOfTab = () => ctx.All();
    private static readonly Func<object> Doc = () => ctx.UndoDocument();

    private static Bind<T> Many<T>(Func<IReadOnlyList<KvElement>> items, Func<KvElement, T> get, Action<KvElement, T> set) =>
        Bind.Many(items, get, set, KeyOf, AllOfTab).Then(_ => ctx.Dirty?.Invoke());

    // A row the classic editor rebuilt the inspector or the overlay after.
    private static Bind<T> Rebuilds<T>(Bind<T> bind) => bind.Then(_ => KeyViewerOverlay.RequestLayoutRebuild());

    private static void Opt(ControlOptions o, string id, string tip, Func<bool> visible) {
        o.Id = id;
        o.Tooltip = tip;
        o.VisibleWhen = visible;
        o.UndoDocument = Doc;
    }

    private static VisualElement Section(VisualElement p, string key) => ShellRows.Section(p, key);

    private static Control<bool> Flag(VisualElement p, string id, bool def, Func<IReadOnlyList<KvElement>> items,
        Func<KvElement, bool> get, Action<KvElement, bool> set, string tip = null, bool rebuild = false, Func<bool> visible = null) {
        Bind<bool> b = Many(items, get, set);
        return Controls.Toggle(p, id, rebuild ? Rebuilds(b) : b, o => {
            Opt(o, id, tip, visible);
            o.Default = def;
        });
    }

    private static Control<float> Num(VisualElement p, string id, float def, float min, float max, string format, float step,
        Func<IReadOnlyList<KvElement>> items, Func<KvElement, float> get, Action<KvElement, float> set) =>
        ShellRows.Fit(Controls.Slider(p, id, Many(items, get, set), o => {
            Opt(o, id, null, null);
            o.Default = def;
            o.Min = min;
            o.Max = max;
            o.Format = format;
            o.Step = step;
        }));

    // A typed number with no range, as the classic single-element fields.
    private static Control<float> Field(VisualElement p, string id, Func<KvElement, float> get, Action<KvElement, float> set) =>
        Controls.Number(p, id, Many(Sel, get, set), o => Opt(o, id, null, null));

    private static Control<Color> Colour(VisualElement p, string id, Color def, bool alpha, Func<IReadOnlyList<KvElement>> items,
        Func<KvElement, Color> get, Action<KvElement, Color> set, Func<bool> visible = null) =>
        Controls.Colour(p, id, Many(items, get, set), o => {
            Opt(o, id, null, visible);
            o.Default = def;
            o.Alpha = alpha;
        });

    private static Control<string> Text(VisualElement p, string id, Func<IReadOnlyList<KvElement>> items,
        Func<KvElement, string> get, Action<KvElement, string> set, int limit, string tip = null, Func<bool> visible = null) =>
        Controls.Text(p, id, Many(items, get, (e, v) => set(e, v ?? "")), o => {
            Opt(o, id, tip, visible);
            o.MaxLength = limit;
        });

    // Segments over known values; a value outside them reads as the fallback, as classic matched it.
    private static Control<string> Pick(VisualElement p, string id, string[] values, Func<string, string> key, string fallback,
        Func<IReadOnlyList<KvElement>> items, Func<KvElement, string> get, Action<KvElement, string> set) =>
        Controls.Choice(p, "", Many(items, e => Match(values, get(e), fallback), set), values, key, o => Opt(o, id, null, null));

    private static string Match(string[] known, string value, string fallback) {
        foreach(string k in known)
            if(string.Equals(k, value, StringComparison.OrdinalIgnoreCase)) return k;
        return fallback;
    }

    private static bool AnyOf(Func<IReadOnlyList<KvElement>> items, Func<KvElement, bool> test) {
        IReadOnlyList<KvElement> list = items();
        for(int i = 0; i < list.Count; i++)
            if(test(list[i])) return true;
        return false;
    }
}
#endif
