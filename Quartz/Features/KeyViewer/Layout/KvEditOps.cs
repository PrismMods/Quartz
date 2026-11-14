using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.KeyViewer.Layout;
// The key viewer editor's element operations on a document. A tab id that is not in the
// document reads as empty and is never created. Keys are positions within a kind's bucket:
// they stay valid across a reparse and across field and draw-order edits, not across a delete.
internal static class KvEditOps {
    internal static string Key(KvDocument doc, string tab, KvElement e) {
        if(doc == null || e == null || !doc.HasTab(tab)) return null;
        int index = doc.Elements(tab, e.Kind).IndexOf(e);
        return index < 0 ? null : tab + "/" + (int)e.Kind + ":" + index;
    }
    internal static KvElement Find(KvDocument doc, string tab, string key) {
        if(doc == null || key == null || !doc.HasTab(tab) || !key.StartsWith(tab + "/", StringComparison.Ordinal)) return null;
        string[] parts = key.Substring(tab.Length + 1).Split(':');
        if(parts.Length != 2 || !int.TryParse(parts[0], out int kind) || !int.TryParse(parts[1], out int index)
            || !Enum.IsDefined(typeof(KvElementKind), kind)) return null;
        List<KvElement> list = doc.Elements(tab, (KvElementKind)kind);
        return index >= 0 && index < list.Count ? list[index] : null;
    }
    internal static List<KvElement> Duplicate(KvDocument doc, string tab, IReadOnlyList<KvElement> items) {
        List<KvElement> copies = [];
        if(doc == null || items == null || !doc.HasTab(tab)) return copies;
        List<KvElement> ordered = [.. items];
        ordered.Sort((a, b) => a.Z.CompareTo(b.Z));
        foreach(KvElement e in ordered) copies.Add(e.Clone());
        return Place(doc, tab, copies);
    }
    internal static void Delete(KvDocument doc, string tab, IReadOnlyList<KvElement> items) {
        if(doc == null || items == null || !doc.HasTab(tab)) return;
        foreach(KvElement e in items) doc.Remove(tab, e);
        doc.ReindexZOrder(tab);
    }
    internal static string Copy(IReadOnlyList<KvElement> items) {
        JArray clip = [];
        if(items != null)
            foreach(KvElement e in items.OrderBy(e => e.Z))
                clip.Add(new JObject { ["kind"] = e.Kind.ToString(), ["name"] = e.GlobalKey ?? "", ["raw"] = e.Container.DeepClone() });
        return clip.ToString(Newtonsoft.Json.Formatting.None);
    }
    internal static List<KvElement> Paste(KvDocument doc, string tab, string clip) {
        List<KvElement> pasted = [];
        try {
            if(string.IsNullOrWhiteSpace(clip) || JToken.Parse(clip) is not JArray arr) return null;
            foreach(JToken item in arr) {
                if(item is not JObject o || o["raw"] is not JObject raw
                    || !Enum.TryParse(o["kind"]?.ToString(), out KvElementKind kind)
                    || !Enum.IsDefined(typeof(KvElementKind), kind)) return null;
                JObject outer = (JObject)raw.DeepClone();
                pasted.Add(KvElement.Wrap(outer["position"] as JObject ?? outer, kind, o["name"]?.ToString() ?? ""));
            }
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
        if(doc == null || !doc.HasTab(tab)) return [];
        return Place(doc, tab, pasted);
    }
    internal static void SetOrder(KvDocument doc, string tab, IReadOnlyList<KvElement> drawOrder) {
        if(doc == null || drawOrder == null || !doc.HasTab(tab)) return;
        for(int i = 0; i < drawOrder.Count; i++) drawOrder[i].Z = i;
        doc.ReindexZOrder(tab);
    }
    internal static void SetHidden(IReadOnlyList<KvElement> items, bool hidden) {
        if(items == null) return;
        foreach(KvElement e in items) e.Hidden = hidden;
    }
    // Offsets the new elements and stacks them, in order, above everything already on the tab.
    private static List<KvElement> Place(KvDocument doc, string tab, List<KvElement> added) {
        float top = 0f;
        foreach(KvElement e in doc.AllElements(tab)) top = Math.Max(top, e.Z);
        foreach(KvElement e in added) {
            e.MoveTo(e.X + KvSnap.PasteOffset, e.Y + KvSnap.PasteOffset);
            e.Z = ++top;
            doc.Add(tab, e);
        }
        doc.ReindexZOrder(tab);
        return added;
    }
}
