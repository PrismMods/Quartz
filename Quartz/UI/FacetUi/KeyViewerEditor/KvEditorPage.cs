#if !IL2CPP && !QUARTZ_KEYVIEWER
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutEditor = PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>;
using KeyLayoutSwitcher = PrismLib.UI.Toolkit.KeyLayoutSwitcher<Quartz.Features.KeyViewer.Layout.KvElement>;
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
using LayoutGroup = PrismLib.UI.Toolkit.LayoutGroup;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
/// <summary>
/// Holds the Key Viewer page's editor, the layout switcher and the session's one layout model. Reached only from KvEditorDoor's Impl methods,
/// since it names PrismLib.UI types.
/// </summary>
internal static class KvEditorPage {
    private static KvLayoutModel model;
    /// <summary>The editor last built on the page; null until the page is first shown.</summary>
    internal static KeyLayoutEditor Editor { get; private set; }
    internal static KvLayoutModel Model => model ??= new KvLayoutModel();
    /// <summary>The layout switcher; null until first opened. Hiding keeps it.</summary>
    internal static KeyLayoutSwitcher Switcher { get; private set; }
    internal const string PageKey = "overlay.keyviewer";

    internal static void OpenSwitcher() {
        if(Switcher == null) Switcher = KeyLayoutSwitcher.Open(Model, Edit);
        else Switcher.Show();
    }

    internal static void ToggleSwitcher() {
        if(Switcher?.Visible == true) Switcher.Hide();
        else OpenSwitcher();
    }

    internal static void Shutdown() {
        FontManager.OnFontChanged -= FontChanged;
        Switcher?.Dispose();
        Switcher = null;
        Editor = null;
        model?.Dispose();
        model = null;
        (seenDoc, seenToken, seenTab) = (null, null, null);
    }

    // A card's Edit: shows the layout as one step, then the Key Viewer page.
    private static void Edit(string layout) {
        KvDocument doc = KvStore.Current;
        if(!doc.HasTab(layout)) return;
        LayoutGroup group = doc.IsFootTab(layout) ? LayoutGroup.Feet : LayoutGroup.Hands;
        KeyLayoutEditor.Pick(Model, KeyLayoutText.EditLayout, group, layout);
        Model.Committed();
        if(FacetBridge.NewMenuVisible()) QuartzShell.Go(PageKey);
        else FacetBridge.OpenNewMenu(PageKey);
    }

    internal static VisualElement Build(VisualElement right, VisualElement inspector) {
        Editor = KeyLayoutEditor.Build(right, Model, KvEditorDoor.ToggleSwitcher);   // a second click on Layouts closes it
        Quartz.Prism.PrismBridge.InvertPan(Editor, () => Quartz.Features.KeyViewer.KeyViewerOverlay.Conf?.InvertPreviewPan == true);
        InspectorMoved = Quartz.Prism.PrismBridge.InspectorIn(Editor, inspector);
        FontManager.OnFontChanged -= FontChanged;
        FontManager.OnFontChanged += FontChanged;
        return Editor.Root;
    }

    /// <summary>The editor's inspector sits in the page's settings column, not under the preview.</summary>
    internal static bool InspectorMoved { get; private set; }

    /// <summary>Elements are selected and the inspector shows them in the settings column.</summary>
    internal static bool Inspecting => InspectorMoved && Editor?.Canvas.Selection.Count > 0;

    // The preview draws in the key viewer's font: redraw when any font pick changes.
    private static void FontChanged() => Editor?.Refresh();

#if DEBUG
    /// <summary>How often Tick reached the editor; the self-test's proof that a hidden menu leaves it alone.</summary>
    internal static int TickRuns;
#endif
    // What Tick last saw: a document, a token or a tab that changed without the editor shows on the next frame.
    private static KvDocument seenDoc;
    private static object seenToken;
    private static string seenTab;

    internal static void Tick(Func<KeyCode, bool> down, Func<KeyCode, bool> held, bool ctrlOrCmd, bool shift, bool alt, bool keys) {
        KeyLayoutEditor ed = Editor;
        if(ed == null) return;
#if DEBUG
        TickRuns++;
#endif
        KvDocument doc = KvStore.Current;
        object token = Model.UndoDocument();
        string tab = Model.Editing;
        if(!ReferenceEquals(token, seenToken)) global::Facet.History.Invalidate();   // the header drops steps that went stale
        if(!ReferenceEquals(doc, seenDoc) || !ReferenceEquals(token, seenToken) || tab != seenTab) {
            bool first = seenDoc == null;
            (seenDoc, seenToken, seenTab) = (doc, token, tab);
            if(!first) ed.Refresh();
        }
        if(keys) ed.Tick(down, held, ctrlOrCmd, shift, alt);
    }
}
#endif
