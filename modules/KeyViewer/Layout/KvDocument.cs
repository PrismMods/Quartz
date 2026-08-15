using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.KeyViewer.Layout;
internal enum KvEmbeddedImageRejectionReason {
    Missing,
    Invalid,
    OverBudget,
    MalformedDestination,
}
internal readonly struct KvEmbeddedImageWarning {
    private const int MaxSourceIdCharacters = 96;
    internal string SourceId { get; }
    internal KvEmbeddedImageRejectionReason Reason { get; }
    internal string Message => "Embedded image " + JsonConvert.SerializeObject(SafeSourceId(SourceId))
        + " was skipped: " + ReasonText + ".";
    internal KvEmbeddedImageWarning(string sourceId, KvEmbeddedImageRejectionReason reason) {
        SourceId = sourceId ?? "";
        Reason = reason;
    }
    private string ReasonText => Reason switch {
        KvEmbeddedImageRejectionReason.Missing => "its payload is missing",
        KvEmbeddedImageRejectionReason.Invalid => "its payload is invalid or too large",
        KvEmbeddedImageRejectionReason.OverBudget => "the embedded-image storage budget is exhausted",
        KvEmbeddedImageRejectionReason.MalformedDestination => "the current embedded-image table is malformed",
        _ => "it could not be imported",
    };
    private static string SafeSourceId(string value) {
        if(string.IsNullOrEmpty(value)) return "(empty)";
        int length = Math.Min(value.Length, MaxSourceIdCharacters);
        System.Text.StringBuilder safe = new(length + (value.Length > length ? 1 : 0));
        for(int i = 0; i < length; i++) {
            char c = value[i];
            UnicodeCategory category = char.GetUnicodeCategory(c);
            safe.Append(category is UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.Surrogate ? '?' : c);
        }
        if(value.Length > length) safe.Append('…');
        return safe.ToString();
    }
}
internal sealed class KvDocument {
    internal const string DmLocalImagePrefix = "dmnote-local-image://";
    private const string DefaultTabId = "custom-quartz";
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
        warnings = rejectedImages.AsReadOnly();
        return firstAdded;
    }
    internal bool TryEmbeddedImage(string reference, out string dataBase64, out string extension) {
        return TryEmbeddedImage(reference, out dataBase64, out extension, out _);
    }
    internal bool TryEmbeddedImage(string reference, out string dataBase64, out string extension,
        out object contentScope) {
        dataBase64 = null;
        extension = null;
        contentScope = null;
        if(!TryImageId(reference, out string id) || Root["embeddedLocalImages"] is not JArray images)
            return false;
        foreach(JToken token in images) {
            if(token is not JObject image
                || !string.Equals(image["imageId"]?.ToString(), id, StringComparison.Ordinal)) continue;
            JToken dataToken = image["dataBase64"];
            string data = dataToken?.ToString();
            if(string.IsNullOrWhiteSpace(data)) return false;
            dataBase64 = data;
            extension = image["extension"]?.ToString();
            contentScope = dataToken;
            return true;
        }
        return false;
    }
    private static HashSet<string> ReferencedEmbeddedImages(KvDocument document, IEnumerable<string> sourceTabs) {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach(string tab in sourceTabs)
            foreach((_, KvElementKind kind) in Tables)
                foreach(KvElement element in document.Elements(tab, kind))
                    foreach(string field in EmbeddedImageFields)
                        if(TryImageId(element.Raw[field]?.ToString(), out string id)) result.Add(id);
        return result;
    }
    private Dictionary<string, string> MergeEmbeddedImages(
        KvDocument other, HashSet<string> referenced, long budgetBytes,
        List<KvEmbeddedImageWarning> warnings
    ) {
        Dictionary<string, string> remap = new(StringComparer.Ordinal);
        if(referenced.Count == 0) return remap;
        JToken destinationToken = Root["embeddedLocalImages"];
        if(destinationToken != null
            && destinationToken.Type != JTokenType.Null
            && destinationToken is not JArray) {
            AddWarnings(referenced, KvEmbeddedImageRejectionReason.MalformedDestination, warnings);
            return remap;
        }
        if(other.Root["embeddedLocalImages"] is not JArray source || source.Count == 0) {
            AddWarnings(referenced, KvEmbeddedImageRejectionReason.Missing, warnings);
            return remap;
        }
        JArray destination = destinationToken as JArray;
        Dictionary<string, JObject> existing = new(StringComparer.Ordinal);
        Dictionary<string, KvEmbeddedImageRejectionReason> rejections = new(StringComparer.Ordinal);
        long usedBytes = 0;
        if(destination != null) {
            foreach(JToken token in destination) {
                usedBytes = SaturatingAdd(usedBytes, StoredTokenBytes(token));
                if(token is JObject image && image["imageId"]?.ToString() is { Length: > 0 } id)
                    existing.TryAdd(id, image);
            }
        }
        foreach(JToken token in source) {
            if(token is not JObject sourceImage
                || sourceImage["imageId"]?.ToString() is not { Length: > 0 } sourceId
                || !referenced.Contains(sourceId)
                || remap.ContainsKey(sourceId)) continue;
            string data = sourceImage["dataBase64"]?.ToString();
            if(!IsValidBase64(data)) {
                rejections.TryAdd(sourceId, KvEmbeddedImageRejectionReason.Invalid);
                continue;
            }
            string destinationId = sourceId;
            if(existing.TryGetValue(sourceId, out JObject sameId)) {
                if(string.Equals(
                    sameId.ToString(Formatting.None), sourceImage.ToString(Formatting.None),
                    StringComparison.Ordinal
                )) {
                    remap[sourceId] = sourceId;
                    rejections.Remove(sourceId);
                    continue;
                }
                do destinationId = Guid.NewGuid().ToString();
                while(existing.ContainsKey(destinationId));
            }
            JObject copy = (JObject)sourceImage.DeepClone();
            copy["imageId"] = destinationId;
            long copyBytes = StoredTokenBytes(copy);
            if(copyBytes > budgetBytes || usedBytes > budgetBytes - copyBytes) {
                rejections[sourceId] = KvEmbeddedImageRejectionReason.OverBudget;
                continue;
            }
            if(destination == null) Root["embeddedLocalImages"] = destination = [];
            destination.Add(copy);
            existing[destinationId] = copy;
            usedBytes += copyBytes;
            remap[sourceId] = destinationId;
            rejections.Remove(sourceId);
        }
        foreach(string sourceId in referenced) {
            if(remap.ContainsKey(sourceId)) continue;
            KvEmbeddedImageRejectionReason reason = rejections.TryGetValue(sourceId, out var rejection)
                ? rejection
                : KvEmbeddedImageRejectionReason.Missing;
            warnings.Add(new KvEmbeddedImageWarning(sourceId, reason));
        }
        return remap;
    }
    private static void AddWarnings(IEnumerable<string> sourceIds,
        KvEmbeddedImageRejectionReason reason, List<KvEmbeddedImageWarning> warnings) {
        foreach(string sourceId in sourceIds) warnings.Add(new KvEmbeddedImageWarning(sourceId, reason));
    }
    private static void RemapEmbeddedImageRefs(JObject position, Dictionary<string, string> remap) {
        if(position == null) return;
        foreach(string field in EmbeddedImageFields) {
            if(!TryImageId(position[field]?.ToString(), out string sourceId)) continue;
            if(remap.TryGetValue(sourceId, out string destinationId))
                position[field] = DmLocalImagePrefix + destinationId;
            else position.Remove(field);
        }
    }
    internal static long StoredTokenBytes(JToken token) {
        try { return System.Text.Encoding.UTF8.GetByteCount(token.ToString(Formatting.None)); }
        catch(Exception e) { Diag.Ignore(e); return long.MaxValue; }
    }
    internal static long SaturatingAdd(long left, long right) =>
        right >= long.MaxValue - left ? long.MaxValue : left + right;
    private static bool IsValidBase64(string value) {
        if(string.IsNullOrWhiteSpace(value) || value.Length > KvImageSafety.MaxBase64Characters) return false;
        long chars = 0;
        int padding = 0;
        bool sawPadding = false;
        foreach(char c in value) {
            if(char.IsWhiteSpace(c)) continue;
            if(c == '=') {
                sawPadding = true;
                if(++padding > 2) return false;
            } else {
                bool valid = c is >= 'A' and <= 'Z'
                    || c is >= 'a' and <= 'z'
                    || c is >= '0' and <= '9'
                    || c is '+' or '/';
                if(sawPadding || !valid) return false;
            }
            chars++;
        }
        long decodedBytes = chars / 4 * 3 - padding;
        return chars > 0 && chars % 4 == 0 && decodedBytes <= KvImageSafety.MaxEncodedBytes;
    }
    internal static bool TryImageId(string reference, out string id) {
        id = null;
        if(string.IsNullOrWhiteSpace(reference)) return false;
        string value = reference.Trim();
        if(!value.StartsWith(DmLocalImagePrefix, StringComparison.OrdinalIgnoreCase)) return false;
        id = value.Substring(DmLocalImagePrefix.Length);
        return id.Length > 0;
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
