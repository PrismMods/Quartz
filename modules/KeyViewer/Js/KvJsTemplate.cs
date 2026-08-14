using System.Text;
using Quartz.Core;
namespace Quartz.Features.KeyViewer.Js;
internal sealed class KvJsTemplate {
    private const char PhOpen = '\uE000';
    private const char PhClose = '\uE001';
    private static readonly Dictionary<string, KvJsTemplate> Cache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase) {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
    };
    private readonly List<object> roots;
    private KvJsTemplate(List<object> roots) => this.roots = roots;
    private sealed class TplElement {
        public string Tag;
        public List<(string Name, List<object> Parts)> Attrs = [];
        public List<object> Children = [];
    }
    private sealed class Placeholder {
        public int Index;
    }
    public static KvJsTemplate Get(string[] chunks) {
        StringBuilder sb = new();
        for(int i = 0; i < chunks.Length; i++) {
            sb.Append(chunks[i]);
            if(i < chunks.Length - 1) sb.Append(PhOpen).Append(i).Append(PhClose);
        }
        string source = sb.ToString();
        if(Cache.TryGetValue(source, out KvJsTemplate cached)) return cached;
        KvJsTemplate parsed;
        try {
            int pos = 0;
            parsed = new KvJsTemplate(ParseNodes(source, ref pos, null));
        } catch(Exception e) {
            Diag.Warn(e, "KeyViewerJs/TemplateParse");
            parsed = new KvJsTemplate([]);
        }
        if(Cache.Count > 512) Cache.Clear();
        Cache[source] = parsed;
        return parsed;
    }
    public static void ClearCache() => Cache.Clear();
    private static List<object> ParseNodes(string s, ref int pos, string closeTag) {
        List<object> nodes = [];
        while(pos < s.Length) {
            if(s[pos] == '<') {
                if(Peek(s, pos, "<!--")) {
                    int end = s.IndexOf("-->", pos, StringComparison.Ordinal);
                    pos = end < 0 ? s.Length : end + 3;
                    continue;
                }
                if(Peek(s, pos, "</")) {
                    int end = s.IndexOf('>', pos);
                    string name = s.Substring(pos + 2, (end < 0 ? s.Length : end) - pos - 2).Trim();
                    pos = end < 0 ? s.Length : end + 1;
                    if(closeTag == null || name.Equals(closeTag, StringComparison.OrdinalIgnoreCase)) return nodes;
                    continue;
                }
                TplElement el = ParseElement(s, ref pos);
                if(el != null) nodes.Add(el);
                continue;
            }
            int lt = s.IndexOf('<', pos);
            if(lt < 0) lt = s.Length;
            AddTextParts(nodes, s.Substring(pos, lt - pos));
            pos = lt;
        }
        return nodes;
    }
    private static TplElement ParseElement(string s, ref int pos) {
        pos++;
        int nameStart = pos;
        while(pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '>' && s[pos] != '/') pos++;
        TplElement el = new() { Tag = s.Substring(nameStart, pos - nameStart) };
        bool selfClosed = false;
        while(pos < s.Length) {
            while(pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
            if(pos >= s.Length) break;
            if(s[pos] == '>') {
                pos++;
                break;
            }
            if(s[pos] == '/') {
                pos++;
                if(pos < s.Length && s[pos] == '>') {
                    pos++;
                    selfClosed = true;
                    break;
                }
                continue;
            }
            int attrStart = pos;
            while(pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '=' && s[pos] != '>' && s[pos] != '/') pos++;
            string attrName = s.Substring(attrStart, pos - attrStart);
            List<object> parts = [];
            while(pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
            if(pos < s.Length && s[pos] == '=') {
                pos++;
                while(pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
                if(pos < s.Length && (s[pos] == '"' || s[pos] == '\'')) {
                    char quote = s[pos];
                    pos++;
                    int end = s.IndexOf(quote, pos);
                    if(end < 0) end = s.Length;
                    SplitParts(parts, s.Substring(pos, end - pos));
                    pos = end < s.Length ? end + 1 : end;
                } else {
                    int end = pos;
                    while(end < s.Length && !char.IsWhiteSpace(s[end]) && s[end] != '>' && !(s[end] == '/' && end + 1 < s.Length && s[end + 1] == '>')) end++;
                    SplitParts(parts, s.Substring(pos, end - pos));
                    pos = end;
                }
            } else {
                parts.Add("");
            }
            if(attrName.Length > 0 && attrName.IndexOf(PhOpen) < 0) el.Attrs.Add((attrName, parts));
        }
        if(!selfClosed && !VoidTags.Contains(el.Tag)) el.Children = ParseNodes(s, ref pos, el.Tag);
        return el;
    }
    private static void AddTextParts(List<object> nodes, string text) {
        List<object> parts = [];
        SplitParts(parts, text);
        foreach(object part in parts) {
            if(part is Placeholder) {
                nodes.Add(part);
                continue;
            }
            string collapsed = CollapseWhitespace((string)part);
            if(collapsed.Length > 0) nodes.Add(collapsed);
        }
    }
    private static void SplitParts(List<object> parts, string raw) {
        int pos = 0;
        while(pos < raw.Length) {
            int open = raw.IndexOf(PhOpen, pos);
            if(open < 0) {
                if(pos < raw.Length) parts.Add(raw.Substring(pos));
                return;
            }
            if(open > pos) parts.Add(raw.Substring(pos, open - pos));
            int close = raw.IndexOf(PhClose, open);
            if(close < 0) return;
            parts.Add(new Placeholder { Index = int.Parse(raw.Substring(open + 1, close - open - 1), System.Globalization.CultureInfo.InvariantCulture) });
            pos = close + 1;
        }
    }
    private static bool Peek(string s, int pos, string token) =>
        pos + token.Length <= s.Length && string.CompareOrdinal(s, pos, token, 0, token.Length) == 0;
    private static string CollapseWhitespace(string s) {
        StringBuilder sb = new(s.Length);
        bool ws = false;
        foreach(char c in s) {
            if(char.IsWhiteSpace(c)) {
                ws = true;
                continue;
            }
            if(ws && sb.Length > 0) sb.Append(' ');
            ws = false;
            sb.Append(c);
        }
        return sb.ToString();
    }
    public KvJsVNode Instantiate(object[] values) {
        KvJsVNode root = KvJsVNode.NewElement("div");
        foreach(object node in roots) AppendNode(root, node, values);
        return root.Children.Count == 1 && !root.Children[0].IsText ? root.Children[0] : root;
    }
    private static void AppendNode(KvJsVNode parent, object node, object[] values) {
        switch(node) {
            case string text:
                parent.Children.Add(KvJsVNode.NewText(text));
                break;
            case Placeholder ph:
                AppendValue(parent, ph.Index < values.Length ? values[ph.Index] : null);
                break;
            case TplElement el: {
                KvJsVNode vnode = KvJsVNode.NewElement(el.Tag);
                foreach((string name, List<object> parts) in el.Attrs) vnode.Attrs[name] = JoinParts(parts, values);
                foreach(object child in el.Children) AppendNode(vnode, child, values);
                parent.Children.Add(vnode);
                break;
            }
            default:
                break;
        }
    }
    private static void AppendValue(KvJsVNode parent, object value) {
        switch(value) {
            case null:
                break;
            case KvJsVNode vnode:
                parent.Children.Add(vnode);
                break;
            case System.Collections.IEnumerable list and not string:
                foreach(object item in list) AppendValue(parent, item);
                break;
            default: {
                string text = value.ToString();
                if(!string.IsNullOrEmpty(text)) parent.Children.Add(KvJsVNode.NewText(text));
                break;
            }
        }
    }
    private static string JoinParts(List<object> parts, object[] values) {
        if(parts.Count == 1 && parts[0] is string only) return only;
        StringBuilder sb = new();
        foreach(object part in parts) {
            switch(part) {
                case string s:
                    sb.Append(s);
                    break;
                case Placeholder ph: {
                    object value = ph.Index < values.Length ? values[ph.Index] : null;
                    if(value != null && value is not KvJsVNode) sb.Append(value);
                    break;
                }
                default:
                    break;
            }
        }
        return sb.ToString();
    }
}
