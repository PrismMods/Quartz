namespace Quartz.Features.KeyViewer.Js;
internal sealed class KvJsVNode {
    public string Tag = "";
    public string Text;
    public Dictionary<string, string> Attrs;
    public List<KvJsVNode> Children;
    public bool IsText => Tag.Length == 0;
    public string Attr(string name) =>
        Attrs != null && Attrs.TryGetValue(name, out string value) ? value : null;
    public static KvJsVNode NewText(string text) => new() { Text = text };
    public static bool Same(KvJsVNode a, KvJsVNode b) {
        if(ReferenceEquals(a, b)) return true;
        if(a == null || b == null || a.Tag != b.Tag || a.Text != b.Text) return false;
        Dictionary<string, string> aAttrs = a.Attrs, bAttrs = b.Attrs;
        int attrs = aAttrs?.Count ?? 0;
        if(attrs != (bAttrs?.Count ?? 0)) return false;
        if(aAttrs != null && bAttrs != null) {
            foreach(KeyValuePair<string, string> pair in aAttrs) {
                if(!bAttrs.TryGetValue(pair.Key, out string other) || other != pair.Value) return false;
            }
        }
        List<KvJsVNode> aKids = a.Children, bKids = b.Children;
        int children = aKids?.Count ?? 0;
        if(children != (bKids?.Count ?? 0)) return false;
        if(aKids == null || bKids == null) return true;
        for(int i = 0; i < children; i++) {
            if(!Same(aKids[i], bKids[i])) return false;
        }
        return true;
    }
    public static KvJsVNode NewElement(string tag) => new() {
        Tag = tag,
        Attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        Children = [],
    };
}
