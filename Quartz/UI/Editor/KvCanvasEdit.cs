using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
namespace Quartz.UI.Editor;
/// <summary>
/// Structural edits driven from the inspector and its toolbar rather than from a gesture.
/// Each entry point snapshots history itself, so a panel edit and a drag rewind alike.
/// </summary>
internal sealed partial class KvCanvas {
    /// <summary>
    /// Set while the inspector is capturing a rebind. Every canvas shortcut would otherwise
    /// also fire on the key being bound: Delete would delete the element being rebound, Escape
    /// would drop the selection the panel is editing, an arrow key would nudge it.
    /// </summary>
    internal Func<bool> InputSuppressed { get; set; }
    /// <summary>Place <paramref name="el"/> on top of the current tab and select it.</summary>
    internal void AddElement(KvElement el) {
        if(doc == null || el == null || string.IsNullOrEmpty(tab)) return;
        PushHistory();
        el.Z = TopZ() + 1f;
        doc.Add(tab, el);
        doc.ReindexZOrder(tab);
        Rebuild();
        SetSelection([el]);
        Mutated();
    }
    /// <summary>
    /// Clone the selection in place and select the copies. Deliberately not Copy() + Paste():
    /// duplicating must not overwrite a clipboard the user filled for something else.
    /// </summary>
    internal void DuplicateSelection() {
        if(doc == null || selection.Count == 0 || string.IsNullOrEmpty(tab)) return;
        PushHistory();
        float top = TopZ();
        List<KvElement> copies = [];
        // AllElements hands back a fresh list, so adding to the document while walking it is
        // safe, and the copies land in painter order rather than selection order.
        foreach(KvElement src in doc.AllElements(tab)) {
            if(!selection.Contains(src)) continue;
            KvElement copy = src.Clone();
            copy.MoveTo(copy.X + KvSnap.PasteOffset, copy.Y + KvSnap.PasteOffset);
            copy.Z = ++top;
            doc.Add(tab, copy);
            copies.Add(copy);
        }
        if(copies.Count == 0) return;
        doc.ReindexZOrder(tab);
        Rebuild();
        SetSelection(copies);
        Mutated();
    }
    internal void SetSelectionHidden(bool hidden) {
        if(doc == null || selection.Count == 0) return;
        PushHistory();
        foreach(KvElement el in selection) el.Hidden = hidden;
        Refresh();
        Mutated();
    }
    /// <summary>True when every selected element is hidden, which is what the toolbar's
    /// Hide/Show button flips.</summary>
    internal bool SelectionHidden() {
        if(selection.Count == 0) return false;
        foreach(KvElement el in selection)
            if(!el.Hidden) return false;
        return true;
    }
    /// <summary>
    /// Layout-space point at the middle of the current view, so an added element lands where
    /// the user is looking rather than at an origin that may be scrolled off screen.
    /// </summary>
    internal Vector2 ViewCenter() {
        if(viewport == null || content == null) return Vector2.zero;
        Vector2 size = viewport.rect.size;
        // Inverse of LayoutToOverlay, evaluated at the viewport's centre.
        Vector2 d = (new Vector2(size.x * 0.5f, -size.y * 0.5f) - content.anchoredPosition)
            / Mathf.Max(0.01f, zoom);
        return new Vector2(d.x, -d.y);
    }
    private float TopZ() {
        float top = 0f;
        foreach(KvElement el in doc.AllElements(tab)) top = Mathf.Max(top, el.Z);
        return top;
    }
}
