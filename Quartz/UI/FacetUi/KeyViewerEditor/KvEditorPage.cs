#if !IL2CPP && !QUARTZ_KEYVIEWER
using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
using UnityEngine.UIElements;
using KeyLayoutEditor = PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement>;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
/// <summary>
/// Holds the Key Viewer page's editor and the session's one layout model. Reached only from KvEditorDoor's Impl methods,
/// since it names PrismLib.UI types.
/// </summary>
internal static class KvEditorPage {
    private static KvLayoutModel model;
    /// <summary>The editor last built on the page; null until the page is first shown.</summary>
    internal static KeyLayoutEditor Editor { get; private set; }
    internal static KvLayoutModel Model => model ??= new KvLayoutModel();

    internal static VisualElement Build(VisualElement right) {
        Editor = KeyLayoutEditor.Build(right, Model, KvEditorDoor.OpenSwitcher);
        return Editor.Root;
    }

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
