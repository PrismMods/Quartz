using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.KeyViewer.Layout;
internal sealed partial class KvDocument {
    internal const string DmLocalImagePrefix = "dmnote-local-image://";
    private const string DefaultTabId = "custom-quartz";
    internal const string FootTabKey = "quartzFoot";
    internal const string SelectedFootKey = "quartzSelectedFootTab";
    private const string DefaultTabName = "Quartz";
    private static readonly string[] BuiltinTabs = ["4key", "5key", "6key", "8key"];
    private static readonly (string table, KvElementKind kind)[] Tables = [
        ("keyPositions", KvElementKind.Key),
        ("statPositions", KvElementKind.Stat),
        ("graphPositions", KvElementKind.Graph),
        ("knobPositions", KvElementKind.Knob),
    ];
    private static readonly string[] EmbeddedImageFields = ["inactiveImage", "activeImage"];
    // Bounds growth across repeated imports. Existing records are counted but
    // never removed merely because an older document is already over budget.
    internal const long MaxEmbeddedImageStorageBytes = 32L * 1024 * 1024;
    internal JObject Root { get; private set; }
    private readonly Dictionary<string, Dictionary<KvElementKind, List<KvElement>>> tabs = new(StringComparer.Ordinal);
    private KvDocument(JObject root) {
        Root = root ?? [];
    }
    internal string SelectedTab {
        get {
            string sel = Root["selectedKeyType"]?.ToString();
            if(!string.IsNullOrWhiteSpace(sel) && tabs.ContainsKey(sel) && !IsFootTab(sel)) return sel;
            foreach(string tab in tabs.Keys) if(!IsFootTab(tab)) return tab;
            return DefaultTabId;
        }
        set {
            if(!string.IsNullOrWhiteSpace(value) && !IsFootTab(value)) Root["selectedKeyType"] = value;
        }
    }
    internal string SelectedFootTab {
        get {
            string sel = Root[SelectedFootKey]?.ToString();
            return !string.IsNullOrWhiteSpace(sel) && tabs.ContainsKey(sel) && IsFootTab(sel) ? sel : null;
        }
        set {
            if(string.IsNullOrWhiteSpace(value)) Root.Remove(SelectedFootKey);
            else Root[SelectedFootKey] = value;
        }
    }
    internal bool IsFootTab(string tab) {
        JToken flag = CustomTabEntry(tab)?[FootTabKey];
        if(flag == null) return false;
        try { return flag.ToObject<bool>(); }
        catch(Exception e) { Diag.Ignore(e); return false; }
    }
    internal void SetFootTab(string tab, bool foot) {
        JObject entry = CustomTabEntry(tab);
        if(entry == null) return;
        if(foot) entry[FootTabKey] = true;
        else entry.Remove(FootTabKey);
    }
    internal int HandTabCount {
        get {
            int count = 0;
            foreach(string tab in tabs.Keys) if(!IsFootTab(tab)) count++;
            return count;
        }
    }
    internal IEnumerable<string> Tabs => tabs.Keys;
    internal bool HasTab(string tab) => tab != null && tabs.ContainsKey(tab);
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
        if(string.IsNullOrWhiteSpace(tab) || !tabs.ContainsKey(tab)) return false;
        bool foot = IsFootTab(tab);
        if(!foot && HandTabCount <= 1) return false;
        List<string> order = [.. tabs.Keys];
        int index = order.IndexOf(tab);
        bool wasSelected = !foot && SelectedTab == tab;
        bool wasFootSelected = foot && SelectedFootTab == tab;
        tabs.Remove(tab);
        foreach((string table, _) in Tables) (Root[table] as JObject)?.Remove(tab);
        (Root["keys"] as JObject)?.Remove(tab);
        (Root[RenderAnchorTable] as JObject)?.Remove(tab);
        if(Root["customTabs"] is JArray custom)
            for(int i = custom.Count - 1; i >= 0; i--)
                if(custom[i] is JObject o && o["id"]?.ToString() == tab) custom.RemoveAt(i);
        order.RemoveAt(index);
        if(wasSelected)
            for(int i = Math.Min(index, order.Count - 1); i >= 0; i--)
                if(!IsFootTab(order[i])) { SelectedTab = order[i]; break; }
        if(wasFootSelected) {
            SelectedFootTab = null;
            foreach(string other in order) if(IsFootTab(other)) { SelectedFootTab = other; break; }
        }
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
        } catch(Exception e) {
            Diag.Ignore(e);
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
        } catch(Exception e) { Diag.Ignore(e); }
        string content = (Root["customCSS"] as JObject)?["content"]?.ToString() ?? "";
        return (enabled, content);
    }
    internal string MergeFrom(KvDocument other) =>
        MergeFrom(other, MaxEmbeddedImageStorageBytes, out _);
    internal string MergeFrom(KvDocument other, out IReadOnlyList<KvEmbeddedImageWarning> warnings) =>
        MergeFrom(other, MaxEmbeddedImageStorageBytes, out warnings);
    internal string MergeFrom(KvDocument other, long embeddedImageBudgetBytes) =>
        MergeFrom(other, embeddedImageBudgetBytes, out _);
    internal string MergeFrom(KvDocument other, long embeddedImageBudgetBytes,
        out IReadOnlyList<KvEmbeddedImageWarning> warnings) {
        List<KvEmbeddedImageWarning> rejectedImages = [];
        if(other == null) {
            warnings = rejectedImages.AsReadOnly();
            return null;
        }
        List<string> sourceTabs = [];
        foreach(string tab in new List<string>(other.Tabs))
            if(other.AllElements(tab).Count > 0) sourceTabs.Add(tab);
        HashSet<string> referencedImages = ReferencedEmbeddedImages(other, sourceTabs);
        Dictionary<string, string> imageIds = MergeEmbeddedImages(
            other, referencedImages, Math.Max(0, embeddedImageBudgetBytes), rejectedImages
        );
        string firstAdded = null;
        foreach(string srcTab in sourceTabs) {
            string newId = NewTabId();
            EnsureTab(newId, UniqueTabName(other.TabName(srcTab)));
            foreach((_, KvElementKind kind) in Tables)
                foreach(KvElement el in other.Elements(srcTab, kind)) {
                    KvElement clone = el.Clone();
                    RemapEmbeddedImageRefs(clone.Raw, imageIds);
                    Add(newId, clone);
                }
            firstAdded ??= newId;
        }
        SplitFootTabs();
        warnings = rejectedImages.AsReadOnly();
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
        doc.SplitFootTabs();
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
    internal void SplitFootTabs() {
        List<string> source = [];
        foreach(string tab in tabs.Keys) if(!IsFootTab(tab)) source.Add(tab);
        foreach(string tab in source) {
            List<KvElement> foot = [];
            foreach(KvElement el in Bucket(tab, KvElementKind.Key)) if(el.Foot) foot.Add(el);
            if(foot.Count == 0) continue;
            string id = NewTabId();
            EnsureTab(id, UniqueTabName(TabName(tab) + " Foot"));
            SetFootTab(id, true);
            List<KvElement> keys = Bucket(tab, KvElementKind.Key);
            List<KvElement> moved = Bucket(id, KvElementKind.Key);
            foreach(KvElement el in foot) {
                keys.Remove(el);
                moved.Add(el);
            }
            ReindexZOrder(tab);
            ReindexZOrder(id);
            if(SelectedFootTab == null || string.Equals(tab, SelectedTab, StringComparison.Ordinal))
                SelectedFootTab = id;
        }
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
