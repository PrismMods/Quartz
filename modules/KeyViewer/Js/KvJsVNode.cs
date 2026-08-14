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
    public static KvJsVNode NewElement(string tag) => new() {
        Tag = tag,
        Attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        Children = [],
    };
}
