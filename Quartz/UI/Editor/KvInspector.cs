using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
/// <summary>
/// The property panel behind <see cref="KvCanvas"/>: what DM Note calls its PropertiesPanel.
/// The canvas can place an element; this is what makes it look like anything.
///
/// Content is rebuilt into a host rect on every selection change. That host is the pane on the
/// right of the canvas, which is where DM Note puts its own — and roughly a third of the width
/// the shared row builders assume, so every row here goes through <see cref="KvWidgets"/>
/// rather than <see cref="GenerateUI"/>.
///
/// Sections are plain header rows, never <c>GenerateUI.Collapsible</c> — Collapsible registers
/// into a static list cleared only on a full page rebuild, so one leaked section per selection
/// change would accumulate for the life of the menu.
/// </summary>
internal sealed partial class KvInspector {
    private enum InspTab {
        Element,
        Style,
        Note,
        Counter,
        /// <summary>
        /// The whole viewer rather than the selection: the settings the DM Note renderer draws
        /// every tab through. Offered on every tab list, including the empty one, because it is
        /// the editor's only route to them and a selection is not a precondition for any of them.
        /// </summary>
        Settings,
    }
    private static readonly InspTab[] KeyTabs = [InspTab.Element, InspTab.Style, InspTab.Note, InspTab.Counter, InspTab.Settings];
    private static readonly InspTab[] StatTabs = [InspTab.Element, InspTab.Style, InspTab.Counter, InspTab.Settings];
    private static readonly InspTab[] PlainTabs = [InspTab.Element, InspTab.Style, InspTab.Settings];
    /// <summary>Nothing selected: only the tab that does not describe a selection can say anything.</summary>
    private static readonly InspTab[] EmptyTabs = [InspTab.Element, InspTab.Settings];
    private readonly KvCanvas canvas;
    private InspTab tab = InspTab.Element;
    private bool listening;
    private bool ghostListening;
    /// <summary>
    /// The control currently mid-stream, or null. A slider drag, a colour drag and a keystroke
    /// each raise OnChanged repeatedly; the snapshot has to be the state before the first one,
    /// so it is taken once and not again until that control finishes.
    ///
    /// Keyed by control rather than a bare flag because not every path raises a completion:
    /// the colour picker's hex box abandons its stream on unparseable text. A flag would stay
    /// stuck and cost the *next* control its undo step; an owner only ever coalesces a control
    /// with itself, which is what a single edit session should do anyway.
    /// </summary>
    private string streaming;
    private KeyCaptureRunner capture;
    /// <summary>Where <see cref="Push"/> rebuilds. Owned by the page, not by this class.</summary>
    private RectTransform host;
    /// <summary>The tab strip's own rect, rebuilt with the panel because the tab list depends on
    /// what is selected.</summary>
    private RectTransform tabsHost;
    /// <summary>
    /// The Settings tab's content, built once by the page and only shown or hidden here. Not
    /// rebuilt per tab switch: the page's widgets are the only reference to the UIObjects behind
    /// them, and this class disposes nothing it did not build.
    /// </summary>
    private RectTransform settingsHost;
    private Action onSettingsShown;
    /// <summary>
    /// The panel's own scroll. Reset when the panel starts describing something else — a new
    /// selection, a new tab — because tabs of unequal length share one viewport and a switch from
    /// a long one to a short one would otherwise leave the view parked past the end of the new
    /// content. Not reset by <see cref="Push"/> itself: a rebuild in place is the same panel, and
    /// throwing the user back to the top for ticking a checkbox halfway down it is not.
    /// </summary>
    private UIScrollController scroll;
    /// <summary>
    /// Widgets built into <see cref="host"/>, disposed before each rebuild. UIInput registers
    /// itself into a static tick list, so dropping one without disposing it leaks for the life
    /// of the menu — the rest die with their GameObjects.
    /// </summary>
    private readonly List<UIObject> tracked = [];
    private KvInspector(KvCanvas canvas) => this.canvas = canvas;
    internal static KvInspector Attach(KvCanvas canvas) {
        KvInspector insp = new(canvas);
        canvas.SelectionChanged += insp.OnSelectionChanged;
        // Undo/redo availability moves with the document, not with the selection.
        canvas.Changed += insp.SyncToolbar;
        // A rebind swallows the key it captures; without this the canvas would act on it too.
        canvas.InputSuppressed = () => insp.listening || insp.ghostListening;
        insp.capture = canvas.Rect.gameObject.AddComponent<KeyCaptureRunner>();
        insp.capture.IsListening = () => insp.listening || insp.ghostListening;
        insp.capture.ShouldCancel = () => {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            TMP_InputField field = sel != null ? sel.GetComponent<TMP_InputField>() : null;
            return field != null && field.isFocused;
        };
        insp.capture.OnCaptured = key => {
            if(insp.ghostListening) insp.BindGhost(key);
            else insp.BindKey(key);
        };
        insp.capture.OnCancelled = () => {
            insp.listening = false;
            insp.ghostListening = false;
            insp.Push();
        };
        return insp;
    }
    /// <summary>
    /// Hand over the Settings tab's content. Call before <see cref="BindHost"/>, which draws the
    /// first state and would otherwise have no tab to show.
    /// </summary>
    internal void BindSettings(RectTransform hostRect, Action onShown) {
        settingsHost = hostRect;
        onSettingsShown = onShown;
    }
    /// <summary>Point the inspector at the rects it rebuilds into, and draw its first state.</summary>
    internal void BindHost(RectTransform tabsRect, RectTransform hostRect, UIScrollController scrollController) {
        tabsHost = tabsRect;
        host = hostRect;
        scroll = scrollController;
        Push();
    }
    internal void Dispose() {
        canvas.SelectionChanged -= OnSelectionChanged;
        canvas.Changed -= SyncToolbar;
        canvas.InputSuppressed = null;
        DisposeTracked();
        host = null;
        tabsHost = null;
        settingsHost = null;
        onSettingsShown = null;
        scroll = null;
    }
    private void OnSelectionChanged() {
        listening = false;
        ghostListening = false;
        SyncToolbar();
        Push();
        // The tab survives a selection change, so Settings can still be on screen — and it does
        // not describe the selection, which makes clicking the canvas no reason to throw away the
        // user's place in it. Read after Push, which is what resolves a tab the new selection has
        // no room for.
        if(tab != InspTab.Settings) scroll?.ScrollTo(0f);
    }
    private void DisposeTracked() {
        foreach(UIObject obj in tracked) obj?.Dispose();
        tracked.Clear();
    }
    /// <summary>Rebuild the inspector for the current selection.</summary>
    internal void Push() {
        // The control that could have been mid-stream is about to be destroyed, so no
        // completion is coming to release the guard.
        streaming = null;
        DisposeTracked();
        if(host == null) return;
        GenerateUI.ClearChildren(host);
        InspTab[] tabs = TabsForSelection();
        if(Array.IndexOf(tabs, tab) < 0) tab = InspTab.Element;
        if(tabsHost != null) {
            GenerateUI.ClearChildren(tabsHost);
            // KvTabs, not Segments: the strip at the head of the panel is DM Note's tab control,
            // which is a track with a pill in it. The segmented controls inside the panel below
            // are a different thing and stay on the shared builder.
            KvTabs.Build(tabsHost, tabs, TabName, TabKey, tab, t => {
                tab = t;
                Push();
                scroll?.ScrollTo(0f);
            });
        }
        bool settings = tab == InspTab.Settings && settingsHost != null;
        if(settingsHost != null) settingsHost.gameObject.SetActive(settings);
        host.gameObject.SetActive(!settings);
        if(settings) {
            onSettingsShown?.Invoke();
            return;
        }
        Build(host, tracked);
    }
    /// <summary>
    /// The tabs a selection has anything to say through. A batch is gated on what it *contains*
    /// rather than on its first element, which is DM Note's own rule: it offers Note only once a
    /// real key is in the selection, and drops Counter for a graph-only one. Reading the richest
    /// kind present gives the same three answers off the one table the single tabs already use —
    /// so a batch of keys and graphs still edits its keys' notes, and only the graphs are skipped
    /// on the way out.
    /// </summary>
    private InspTab[] TabsForSelection() {
        IReadOnlyList<KvElement> sel = canvas.Selection;
        if(sel.Count == 0) return EmptyTabs;
        if(sel.Count == 1) return TabsFor(sel[0].Kind);
        bool key = false, stat = false;
        foreach(KvElement el in sel) {
            if(el.Kind == KvElementKind.Key) key = true;
            else if(el.Kind == KvElementKind.Stat) stat = true;
        }
        return key ? KeyTabs : stat ? StatTabs : PlainTabs;
    }
    private void Build(RectTransform root, List<UIObject> tracked) {
        IReadOnlyList<KvElement> sel = canvas.Selection;
        if(sel.Count == 0) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(root, 30f), "KVI_EMPTY",
                "Select an element on the canvas to edit it. Drag a box around several to edit them together.",
                17f, 0.45f
            );
            return;
        }
        // Copied out of the canvas's live selection list, which SetSelection refills in place —
        // the rows below close over this array for the life of the panel.
        KvElement[] batch = [.. sel];
        TextMeshProUGUI title = GenerateUI.AddText(GenerateUI.Row(root, 36f));
        title.fontSize = 24f;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.text = batch.Length == 1
            ? TitleFor(batch[0])
            : string.Format(MainCore.Tr.Get("KVI_MULTI_TITLE", "{0} elements selected"), batch.Length);
        switch(tab) {
            case InspTab.Style:
                BuildStyleTab(root, tracked, batch);
                break;
            // Note and Counter are offered for a selection that merely contains a key or a stat,
            // so each is handed only the elements that read its fields. The tab list guarantees
            // those subsets are non-empty; the builders re-check anyway, because a tab that
            // survives a selection change is resolved by Push rather than by this switch.
            case InspTab.Note:
                BuildNoteTab(root, tracked, OfKind(batch, KvElementKind.Key));
                break;
            case InspTab.Counter:
                BuildCounterTab(root, tracked, KeyLike(batch));
                break;
            default:
                BuildElementTab(root, tracked, batch);
                break;
        }
    }
    private string TitleFor(KvElement el) {
        if(listening) return MainCore.Tr.Get("KVI_LISTENING", "Press a key... (Esc cancels)");
        if(ghostListening) return MainCore.Tr.Get("KVI_GHOST_LISTENING", "Press the ghost key... (Esc cancels)");
        return string.Format(MainCore.Tr.Get("KVI_TITLE", "Editing {0}"), KindName(el));
    }
    private static string KindName(KvElement el) => el.Kind switch {
        KvElementKind.Key => MainCore.Tr.Get("KVI_KIND_KEY", "key"),
        KvElementKind.Stat => MainCore.Tr.Get("KVI_KIND_STAT", "stat"),
        KvElementKind.Graph => MainCore.Tr.Get("KVI_KIND_GRAPH", "graph"),
        _ => MainCore.Tr.Get("KVI_KIND_KNOB", "knob"),
    };
    private static InspTab[] TabsFor(KvElementKind kind) => kind switch {
        KvElementKind.Key => KeyTabs,
        // A stat never spawns a note (ParseDmNoteSpec short-circuits on IsStat), and a graph
        // and a knob have neither notes nor counters, so those tabs would be inert.
        KvElementKind.Stat => StatTabs,
        _ => PlainTabs,
    };
    private static string TabName(InspTab t) => t switch {
        InspTab.Style => MainCore.Tr.Get("KVI_TAB_STYLE", "Style"),
        InspTab.Note => MainCore.Tr.Get("KVI_TAB_NOTE", "Rain"),
        InspTab.Counter => MainCore.Tr.Get("KVI_TAB_COUNTER", "Counter"),
        InspTab.Settings => MainCore.Tr.Get("KVI_TAB_SETTINGS", "Settings"),
        _ => MainCore.Tr.Get("KVI_TAB_ELEMENT", "Element"),
    };
    private static string TabKey(InspTab t) => t switch {
        InspTab.Style => "KVI_TAB_STYLE",
        InspTab.Note => "KVI_TAB_NOTE",
        InspTab.Counter => "KVI_TAB_COUNTER",
        InspTab.Settings => "KVI_TAB_SETTINGS",
        _ => "KVI_TAB_ELEMENT",
    };
    // ---- edit plumbing -----------------------------------------------------------
    /// <summary>A discrete edit: one snapshot, one repaint, one save.</summary>
    private void Edit(Action apply) {
        streaming = null;
        canvas.PushHistory();
        apply();
        canvas.Refresh();
        canvas.Mutated();
    }
    /// <summary>A frame of a streaming edit from <paramref name="owner"/>. Repaints without
    /// saving; see <see cref="streaming"/>.</summary>
    private void Stream(string owner, Action apply) {
        if(streaming != owner) {
            streaming = owner;
            canvas.PushHistory();
        }
        apply();
        canvas.Refresh();
    }
    /// <summary>The end of a streaming edit: the point the layout is actually written out.</summary>
    private void Commit(string owner, Action apply) {
        if(streaming != owner) {
            streaming = owner;
            canvas.PushHistory();
        }
        apply();
        streaming = null;
        canvas.Refresh();
        canvas.Mutated();
    }
    // ---- row builders ------------------------------------------------------------
    private static TextMeshProUGUI Header(RectTransform root, string key, string text) =>
        KvWidgets.Header(root, key, text);
    private UISlider Num(
        RectTransform root, List<UIObject> tracked, string label, string id,
        float def, float min, float max, float value, string format, float step, Action<float> write
    ) {
        float Snap(float v) => Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
        UISlider s = KvWidgets.Slider(
            GenerateUI.Row(root), def, min, max, value, Snap, null, null, label, id
        );
        s.Format = format;
        s.OnChanged = v => Stream(id, () => write(v));
        s.OnComplete = v => Commit(id, () => write(v));
        tracked.Add(s);
        return s;
    }
    private UIToggle Flag(
        RectTransform root, List<UIObject> tracked, string label, string id,
        bool def, bool value, Action<bool> write, bool rebuild = false
    ) {
        UIToggle t = KvWidgets.Toggle(
            GenerateUI.Row(root), def, value,
            v => {
                Edit(() => write(v));
                if(rebuild) Push();
            },
            label, id
        );
        tracked.Add(t);
        return t;
    }
    private UIColorPicker Colour(
        RectTransform root, List<UIObject> tracked, string label, string id,
        Color def, Color value, Action<Color> write, bool showAlpha
    ) {
        UIColorPicker p = KvWidgets.ColorPicker(
            GenerateUI.Row(root), def, value,
            c => Stream(id, () => write(c)),
            c => Commit(id, () => write(c)),
            label, id, showAlpha
        );
        tracked.Add(p);
        return p;
    }
    /// <summary>Width of the caption a <see cref="NumField"/> reserves on the left of its row.
    /// Every label that reaches one is short — X, Y, Width, Height, Presses — so the field keeps
    /// the rest of a pane that has none to spare.</summary>
    private const float NumFieldCaptionW = 84f;
    /// <summary>
    /// A numeric text field. Writes live per keystroke so the canvas tracks typing, and saves
    /// on end-edit — the one point at which the user is done with the field.
    ///
    /// The caption is added here because UIInput has none: its placeholder is its only text and
    /// that shows solely while the field is empty, which a coordinate never is. Without this
    /// the row would be an unlabelled box.
    /// </summary>
    private UIInput NumField(
        RectTransform root, List<UIObject> tracked, string label, string id,
        float value, Action<float> write
    ) {
        RectTransform row = GenerateUI.Row(root);
        UIInput input = KvWidgets.Input(
            row, "", Fmt(value),
            v => {
                if(TryNum(v, out float parsed)) Stream(id, () => write(parsed));
            },
            label, MainCore.Spr.Get(UISprite.Text128), id
        );
        input.InputField.characterLimit = 10;
        input.InputField.onEndEdit.AddListener(v => {
            // Unparseable text leaves the last good value in place; the stream still ends, so
            // a half-typed number is one undo step rather than an open one.
            if(TryNum(v, out float parsed)) Commit(id, () => write(parsed));
            else if(streaming == id) streaming = null;
        });
        input.Rect.offsetMin = new Vector2(NumFieldCaptionW, 0f);
        TextMeshProUGUI caption = GenerateUI.AddText(row);
        caption.fontSize = 19f;
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.raycastTarget = false;
        GenerateUI.LocalizeById(caption, id, label);
        RectTransform capRect = caption.rectTransform;
        capRect.anchorMin = new Vector2(0f, 0f);
        capRect.anchorMax = new Vector2(0f, 1f);
        capRect.pivot = new Vector2(0f, 0.5f);
        capRect.offsetMin = new Vector2(12f, 0f);
        capRect.offsetMax = Vector2.zero;
        capRect.sizeDelta = new Vector2(NumFieldCaptionW - 12f, 0f);
        tracked.Add(input);
        return input;
    }
    /// <summary>Invariant parse: a comma-decimal locale must not make "1.5" unreadable.</summary>
    private static bool TryNum(string s, out float value) => float.TryParse(
        s, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out value
    );
    private static string Fmt(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private UIButton Btn(RectTransform root, List<UIObject> tracked, string label, string id, Action click) {
        UIButton b = KvWidgets.Button(GenerateUI.Row(root, 44f), click, label, id);
        tracked.Add(b);
        return b;
    }
    private static void Segments<T>(
        RectTransform root, IReadOnlyList<T> values, Func<T, string> name, Func<T, string> key,
        T value, Action<T> onChanged
    ) => KvWidgets.Segments(root, values, name, key, value, onChanged);
}
