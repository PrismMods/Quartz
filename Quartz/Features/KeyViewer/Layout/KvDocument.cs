using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Layout;
internal sealed class KvDocument {
    private const string DefaultTabId = "custom-quartz";
    private const string DefaultTabName = "Quartz";
    private static readonly string[] BuiltinTabs = ["4key", "5key", "6key", "8key"];
    private static readonly (string table, KvElementKind kind)[] Tables = [
        ("keyPositions", KvElementKind.Key),
        ("statPositions", KvElementKind.Stat),
        ("graphPositions", KvElementKind.Graph),
        ("knobPositions", KvElementKind.Knob),
    ];
    internal JObject Root { get; private set; }
    private readonly Dictionary<string, Dictionary<KvElementKind, List<KvElement>>> tabs = new(StringComparer.Ordinal);
    private KvDocument(JObject root) {
        Root = root ?? [];
    }
    internal string SelectedTab {
        get {
            string sel = Root["selectedKeyType"]?.ToString();
            if(!string.IsNullOrWhiteSpace(sel) && tabs.ContainsKey(sel)) return sel;
            foreach(string tab in tabs.Keys) return tab;
            return DefaultTabId;
        }
        set {
            if(!string.IsNullOrWhiteSpace(value)) Root["selectedKeyType"] = value;
        }
    }
    internal IEnumerable<string> Tabs => tabs.Keys;
    internal bool HasTab(string tab) => tab != null && tabs.ContainsKey(tab);
    internal int TabCount => tabs.Count;
    internal const int MaxCustomTabs = 30;
    internal int CustomTabCount => (Root["customTabs"] as JArray)?.Count ?? 0;
    internal string TabName(string tab) {
        JObject entry = CustomTabEntry(tab);
        string name = entry?["name"]?.ToString();
        return string.IsNullOrWhiteSpace(name) ? tab : name;
    }
    private JObject CustomTabEntry(string tab) {
        if(tab == null || Root["customTabs"] is not JArray custom) return null;
        foreach(JToken entry in custom)
            if(entry is JObject o && o["id"]?.ToString() == tab) return o;
        return null;
    }
    private bool HasTabNamed(string name) {
        if(Root["customTabs"] is not JArray custom) return false;
        foreach(JToken entry in custom)
            if(entry is JObject o && string.Equals(o["name"]?.ToString(), name, StringComparison.Ordinal)) return true;
        return false;
    }
    internal string UniqueTabName(string baseName) {
        if(string.IsNullOrWhiteSpace(baseName)) baseName = "Tab";
        string candidate = baseName;
        for(int n = 2; n <= MaxCustomTabs + 1 && HasTabNamed(candidate); n++)
            candidate = baseName + " " + n.ToString(CultureInfo.InvariantCulture);
        return candidate;
    }
    internal string NewTabId() {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for(int n = 0; ; n++) {
            string id = "custom-" + (now + n).ToString(CultureInfo.InvariantCulture);
            if(!tabs.ContainsKey(id) && CustomTabEntry(id) == null) return id;
        }
    }
    internal string RenameTab(string tab, string name) {
        if(string.IsNullOrWhiteSpace(tab) || !tabs.ContainsKey(tab)) return null;
        name = name?.Trim();
        if(string.IsNullOrEmpty(name)) return null;
        JObject entry = CustomTabEntry(tab);
        string current = entry?["name"]?.ToString();
        string unique = string.Equals(current, name, StringComparison.Ordinal) ? name : UniqueTabName(name);
        if(entry != null) {
            entry["name"] = unique;
            return unique;
        }
        JArray custom = Root["customTabs"] as JArray;
        if(custom == null) Root["customTabs"] = custom = [];
        custom.Add(new JObject { ["id"] = tab, ["name"] = unique });
        return unique;
    }
    internal bool RemoveTab(string tab) {
        if(string.IsNullOrWhiteSpace(tab) || !tabs.ContainsKey(tab) || tabs.Count <= 1) return false;
        List<string> order = [.. tabs.Keys];
        int index = order.IndexOf(tab);
        bool wasSelected = SelectedTab == tab;
        tabs.Remove(tab);
        foreach((string table, _) in Tables) (Root[table] as JObject)?.Remove(tab);
        (Root["keys"] as JObject)?.Remove(tab);
        (Root[RenderAnchorTable] as JObject)?.Remove(tab);
        if(Root["customTabs"] is JArray custom)
            for(int i = custom.Count - 1; i >= 0; i--)
                if(custom[i] is JObject o && o["id"]?.ToString() == tab) custom.RemoveAt(i);
        order.RemoveAt(index);
        if(wasSelected && order.Count > 0) SelectedTab = order[Math.Max(0, index - 1)];
        return true;
    }
    internal bool TryGetRenderAnchor(string tab, out float x, out float y) {
        x = 0f;
        y = 0f;
        if(tab == null || Root[RenderAnchorTable] is not JObject anchors || anchors[tab] is not JObject a) return false;
        JToken ax = a["x"], ay = a["y"];
        if(ax == null || ay == null) return false;
        try {
            x = ax.ToObject<float>();
            y = ay.ToObject<float>();
            return true;
        } catch {
            return false;
        }
    }
    internal void SetRenderAnchor(string tab, float x, float y) {
        if(string.IsNullOrWhiteSpace(tab)) return;
        if(Root[RenderAnchorTable] is not JObject anchors) Root[RenderAnchorTable] = anchors = [];
        anchors[tab] = new JObject { ["x"] = x, ["y"] = y };
    }
    private const string RenderAnchorTable = "quartzRenderAnchors";
    internal (bool Enabled, string Content) EmbeddedCss() {
        bool enabled = false;
        try {
            enabled = Root["useCustomCSS"]?.ToObject<bool>() ?? false;
        } catch { }
        string content = (Root["customCSS"] as JObject)?["content"]?.ToString() ?? "";
        return (enabled, content);
    }
    internal string MergeFrom(KvDocument other) {
        if(other == null) return null;
        string firstAdded = null;
        foreach(string srcTab in new List<string>(other.Tabs)) {
            if(other.AllElements(srcTab).Count == 0) continue;
            string newId = NewTabId();
            EnsureTab(newId, UniqueTabName(other.TabName(srcTab)));
            foreach((_, KvElementKind kind) in Tables)
                foreach(KvElement el in other.Elements(srcTab, kind))
                    Add(newId, el.Clone());
            firstAdded ??= newId;
        }
        return firstAdded;
    }
    internal static KvDocument Empty() {
        KvDocument doc = new([]);
        doc.EnsureTab(DefaultTabId, DefaultTabName);
        doc.SelectedTab = DefaultTabId;
        return doc;
    }
    internal static KvDocument Parse(string json) {
        if(string.IsNullOrWhiteSpace(json)) return Empty();
        JObject root = JObject.Parse(json);
        if(root["keyPositions"] is not JObject && root["positions"] is JObject legacy) {
            root["keyPositions"] = legacy;
            root.Remove("positions");
        }
        if(root["keys"] is not JObject && root["keyPositions"] is not JObject)
            throw new FormatException("Not a DM Note preset: no keys or keyPositions object.");
        KvDocument doc = new(root);
        doc.Load();
        if(doc.tabs.Count == 0) doc.EnsureTab(DefaultTabId, DefaultTabName);
        return doc;
    }
    private void Load() {
        tabs.Clear();
        JObject keyNames = Root["keys"] as JObject;
        foreach((string table, KvElementKind kind) in Tables) {
            if(Root[table] is not JObject byTab) continue;
            foreach(JProperty prop in byTab.Properties()) {
                if(prop.Value is not JArray arr) continue;
                List<KvElement> list = Bucket(prop.Name, kind);
                JArray names = kind == KvElementKind.Key ? keyNames?[prop.Name] as JArray : null;
                for(int i = 0; i < arr.Count; i++) {
                    if(arr[i] is not JObject raw) continue;
                    JObject geometry = raw["position"] as JObject ?? raw;
                    string name = names != null && i < names.Count ? names[i]?.ToString() ?? "" : "";
                    KvElement el = KvElement.Wrap(geometry, kind, name);
                    list.Add(el);
                }
            }
        }
        if(keyNames != null)
            foreach(JProperty prop in keyNames.Properties()) Bucket(prop.Name, KvElementKind.Key);
    }
    private List<KvElement> Bucket(string tab, KvElementKind kind) {
        if(!tabs.TryGetValue(tab, out Dictionary<KvElementKind, List<KvElement>> byKind))
            tabs[tab] = byKind = [];
        if(!byKind.TryGetValue(kind, out List<KvElement> list)) byKind[kind] = list = [];
        return list;
    }
    internal List<KvElement> Elements(string tab, KvElementKind kind) => Bucket(tab, kind);
    internal List<KvElement> BoundKeyElements(string tab) {
        List<KvElement> result = [];
        foreach(KvElement el in Bucket(tab, KvElementKind.Key))
            if(!el.Hidden && !string.IsNullOrEmpty(el.GlobalKey)) result.Add(el);
        return result;
    }
    internal List<KvElement> AllElements(string tab) {
        List<KvElement> all = [];
        foreach((_, KvElementKind kind) in Tables) all.AddRange(Bucket(tab, kind));
        all.Sort((a, b) => a.Z.CompareTo(b.Z));
        return all;
    }
    internal void EnsureTab(string tab, string displayName = null) {
        if(string.IsNullOrWhiteSpace(tab)) return;
        Bucket(tab, KvElementKind.Key);
        if(displayName == null || Array.IndexOf(BuiltinTabs, tab) >= 0) return;
        JArray custom = Root["customTabs"] as JArray;
        if(custom == null) Root["customTabs"] = custom = [];
        foreach(JToken entry in custom)
            if(entry is JObject o && o["id"]?.ToString() == tab) return;
        custom.Add(new JObject { ["id"] = tab, ["name"] = displayName });
    }
    internal void Add(string tab, KvElement element) {
        if(element == null) return;
        Bucket(tab, element.Kind).Add(element);
    }
    internal bool Remove(string tab, KvElement element) =>
        element != null && Bucket(tab, element.Kind).Remove(element);
    internal void Clear(string tab) {
        foreach((_, KvElementKind kind) in Tables) Bucket(tab, kind).Clear();
    }
    internal void ReindexZOrder(string tab) {
        List<KvElement> all = AllElements(tab);
        for(int i = 0; i < all.Count; i++) all[i].Z = i;
    }
    private void Flush() {
        JObject keyNames = Root["keys"] as JObject;
        if(keyNames == null) Root["keys"] = keyNames = [];
        foreach((string table, KvElementKind kind) in Tables) {
            JObject byTab = Root[table] as JObject;
            bool any = false;
            foreach(string tab in tabs.Keys) if(Bucket(tab, kind).Count > 0) any = true;
            if(!any && byTab == null && kind == KvElementKind.Knob) continue;
            if(byTab == null) Root[table] = byTab = [];
            foreach(string tab in tabs.Keys) {
                List<KvElement> list = Bucket(tab, kind);
                JArray arr = [];
                foreach(KvElement el in list) arr.Add(Container(el));
                byTab[tab] = arr;
            }
        }
        foreach(string tab in tabs.Keys) {
            JArray names = [];
            foreach(KvElement el in Bucket(tab, KvElementKind.Key)) names.Add(el.GlobalKey ?? "");
            keyNames[tab] = names;
        }
        Root["selectedKeyType"] = SelectedTab;
    }
    private static JObject Container(KvElement el) {
        JObject parent = el.Raw.Parent is JProperty { Name: "position" } prop && prop.Parent is JObject outer
            ? outer
            : el.Raw;
        return parent;
    }
    internal string ToJson(bool pretty = false) {
        Flush();
        return Root.ToString(pretty ? Formatting.Indented : Formatting.None);
    }
}
