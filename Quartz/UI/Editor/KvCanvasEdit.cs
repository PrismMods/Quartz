using Quartz.Features.KeyViewer.Layout;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    internal Func<bool> InputSuppressed { get; set; }
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
    internal void DuplicateSelection() {
        if(doc == null || selection.Count == 0 || string.IsNullOrEmpty(tab)) return;
        PushHistory();
        float top = TopZ();
        List<KvElement> copies = [];
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
    internal bool SelectionHidden() {
        if(selection.Count == 0) return false;
        foreach(KvElement el in selection)
            if(!el.Hidden) return false;
        return true;
    }
    internal Vector2 ViewCenter() {
        if(viewport == null || content == null) return Vector2.zero;
        Vector2 size = viewport.rect.size;
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
