using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Layout;
/// <summary>
/// A KeyViewer layout, stored as — and serialized to — a real DM Note preset.
///
/// Quartz does not define its own layout schema. DM Note's preset format is the native format,
/// so "import a DM Note preset", "export to DM Note", and "save my layout" are one code path
/// and no lossy mapping layer exists to drift. Quartz-only concepts ride along as extra keys
/// (see <see cref="KvElement"/>), which DM Note ignores and preserves.
///
/// The whole source document is retained. <see cref="Root"/> holds top-level tables Quartz
/// neither reads nor renders — knobPositions, customCSS/customJS, fontSettings, noteSettings,
/// tabNoteOverrides, embeddedLocalFonts/Images/Sounds — and they are written back untouched.
/// </summary>
internal sealed class KvDocument {
    private const string DefaultTabId = "custom-quartz";
    private const string DefaultTabName = "Quartz";
    /// <summary>DM Note's hardcoded BUILTIN_TAB_IDS. Custom tabs must not collide with these.</summary>
    private static readonly string[] BuiltinTabs = ["4key", "5key", "6key", "8key"];
    private static readonly (string table, KvElementKind kind)[] Tables = [
        ("keyPositions", KvElementKind.Key),
        ("statPositions", KvElementKind.Stat),
        ("graphPositions", KvElementKind.Graph),
        ("knobPositions", KvElementKind.Knob),
    ];
    /// <summary>The backing preset document, including every table Quartz does not model.</summary>
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
    /// <summary>DM Note's MAX_CUSTOM_TABS. Shared with any custom tab an imported preset brought.</summary>
    internal const int MaxCustomTabs = 30;
    /// <summary>
    /// Registered custom tabs, which is what DM Note's create counts against
    /// <see cref="MaxCustomTabs"/> — the four builtins are implicit and never listed.
    /// </summary>
    internal int CustomTabCount => (Root["customTabs"] as JArray)?.Count ?? 0;
    /// <summary>
    /// The name registered for <paramref name="tab"/>, falling back to the id. DM Note synthesizes
    /// "Custom 1"-style names for ids it finds unregistered, so a bare id surfaces here only for a
    /// tab that arrived without one.
    /// </summary>
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
    /// <summary>
    /// <paramref name="baseName"/>, suffixed until no other tab carries it. DM Note's create
    /// rejects a duplicate name outright, so two tabs generated from the same preset have to be
    /// told apart here or the second one could not be recreated in the app this format belongs to.
    /// </summary>
    internal string UniqueTabName(string baseName) {
        if(string.IsNullOrWhiteSpace(baseName)) baseName = "Tab";
        string candidate = baseName;
        for(int n = 2; n <= MaxCustomTabs + 1 && HasTabNamed(candidate); n++)
            candidate = baseName + " " + n.ToString(CultureInfo.InvariantCulture);
        return candidate;
    }
    /// <summary>
    /// A fresh custom-tab id, in DM Note's own `custom-{unix millis}` shape so a tab Quartz made is
    /// indistinguishable from one its editor made. The scan exists because two tabs created inside
    /// the same millisecond would otherwise land on the same id and merge.
    /// </summary>
    internal string NewTabId() {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for(int n = 0; ; n++) {
            string id = "custom-" + (now + n).ToString(CultureInfo.InvariantCulture);
            if(!tabs.ContainsKey(id) && CustomTabEntry(id) == null) return id;
        }
    }
    /// <summary>
    /// Rename <paramref name="tab"/>, reporting the name it ended up with (which may be suffixed to
    /// keep it unique) or null if the tab does not exist or the name is blank. DM Note has no rename
    /// of its own, but two tabs generated from the same preset get the same base name — so renaming
    /// is what tells "16 Keys" and "16 Keys 2" apart.
    ///
    /// A custom tab carries its name in customTabs[]; a builtin has none, so one is registered.
    /// Quartz only ever authors custom tabs, so the builtin branch is for an imported preset.
    /// </summary>
    internal string RenameTab(string tab, string name) {
        if(string.IsNullOrWhiteSpace(tab) || !tabs.ContainsKey(tab)) return null;
        name = name?.Trim();
        if(string.IsNullOrEmpty(name)) return null;
        JObject entry = CustomTabEntry(tab);
        // Uniqueness is checked against every OTHER tab, so renaming a tab to the name it already
        // has is a no-op rather than gaining a " 2" suffix off its own entry.
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
    /// <summary>
    /// Drop <paramref name="tab"/> and everything on it, reporting whether it went.
    ///
    /// The last tab is refused: <see cref="SelectedTab"/> has to name a tab that exists, and a
    /// document with none leaves the editor bound to nothing with no way back. Selection moves to
    /// the tab before the removed one, which is where DM Note's custom_tabs_delete puts it.
    /// </summary>
    internal bool RemoveTab(string tab) {
        if(string.IsNullOrWhiteSpace(tab) || !tabs.ContainsKey(tab) || tabs.Count <= 1) return false;
        List<string> order = [.. tabs.Keys];
        int index = order.IndexOf(tab);
        bool wasSelected = SelectedTab == tab;
        tabs.Remove(tab);
        // Flush rebuilds these from the tabs it knows about but never prunes the ones it does not,
        // so an entry left here would outlive the tab and be written back on every save.
        foreach((string table, _) in Tables) (Root[table] as JObject)?.Remove(tab);
        (Root["keys"] as JObject)?.Remove(tab);
        if(Root["customTabs"] is JArray custom)
            for(int i = custom.Count - 1; i >= 0; i--)
                if(custom[i] is JObject o && o["id"]?.ToString() == tab) custom.RemoveAt(i);
        // tabNoteOverrides and the other tab-keyed tables Quartz does not model are left alone:
        // DM Note's own delete does not prune them either, and this document's contract is that
        // what it does not model, it does not touch.
        order.RemoveAt(index);
        // Only when it was the one on screen: deleting a tab the user is not looking at must not
        // move them off the one they are.
        if(wasSelected && order.Count > 0) SelectedTab = order[Math.Max(0, index - 1)];
        return true;
    }
    /// <summary>
    /// The preset's embedded custom CSS, as (enabled, content). DM Note stores its keyviewer CSS in
    /// the top-level customCSS table, not on the elements, so importing only the geometry leaves a
    /// CSS-styled preset looking plain. The caller wires this into the CSS engine's own state.
    /// </summary>
    internal (bool Enabled, string Content) EmbeddedCss() {
        bool enabled = false;
        try {
            enabled = Root["useCustomCSS"]?.ToObject<bool>() ?? false;
        } catch { }
        string content = (Root["customCSS"] as JObject)?["content"]?.ToString() ?? "";
        return (enabled, content);
    }
    /// <summary>
    /// Copy every non-empty tab of <paramref name="other"/> in as new tabs, keeping this document's
    /// own, and return the first added tab's id (null if nothing was added). Import adds to a
    /// collection rather than replacing it — a multi-tab layout must survive importing one more
    /// preset. Each incoming tab gets a fresh id and a de-duplicated name so it can never overwrite
    /// or merge into an existing one.
    /// </summary>
    internal string MergeFrom(KvDocument other) {
        if(other == null) return null;
        string firstAdded = null;
        // Snapshot the source tab ids: EnsureTab mutates this.tabs, and other could be this.
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
    /// <summary>
    /// Parse a DM Note preset. Throws <see cref="FormatException"/> when the document is not
    /// recognisably a preset, so an import of the wrong file reports rather than silently
    /// producing an empty layout.
    /// </summary>
    internal static KvDocument Parse(string json) {
        if(string.IsNullOrWhiteSpace(json)) return Empty();
        JObject root = JObject.Parse(json);
        // "positions" is an older DM Note spelling of "keyPositions"; normalize on read so
        // everything downstream sees one name, and write the modern one back out.
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
                    // Stat/graph/knob objects nest their geometry under "position" in some
                    // DM Note versions and flatten it in others. Keep the outer object as the
                    // element so sibling discriminators (statType, graphType, axisId) survive.
                    JObject geometry = raw["position"] as JObject ?? raw;
                    string name = names != null && i < names.Count ? names[i]?.ToString() ?? "" : "";
                    KvElement el = KvElement.Wrap(geometry, kind, name);
                    list.Add(el);
                }
            }
        }
        // A tab can exist in keys[] with no positions yet (DM Note creates custom tabs empty).
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
    /// <summary>
    /// The key elements on <paramref name="tab"/> that reach the game: bound to something, and
    /// not hidden.
    ///
    /// This is the set the renderer turns into boxes, and anything reasoning about "the keys the
    /// viewer shows" has to agree with it. A hidden element is not drawn, cannot light and never
    /// counts, so its binding is not a key in use; an unbound one has no key to speak of. Foot
    /// keys are not a case here — a foot key is an ordinary element that sits lower.
    /// </summary>
    internal List<KvElement> BoundKeyElements(string tab) {
        List<KvElement> result = [];
        foreach(KvElement el in Bucket(tab, KvElementKind.Key))
            if(!el.Hidden && !string.IsNullOrEmpty(el.GlobalKey)) result.Add(el);
        return result;
    }
    /// <summary>Every element on <paramref name="tab"/>, painter order (ascending zIndex).</summary>
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
        // Only custom tabs are listed; DM Note treats the four builtins as implicit and
        // synthesizes names for unregistered ones ("Custom 1", ...), so registering keeps
        // the user's tab name across an export.
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
    /// <summary>
    /// Renumber zIndex to a dense 0..n-1 in current painter order. Mirrors DM Note's
    /// reindexZOrder so exported layouts have the z-order shape its layer panel expects.
    /// </summary>
    internal void ReindexZOrder(string tab) {
        List<KvElement> all = AllElements(tab);
        for(int i = 0; i < all.Count; i++) all[i].Z = i;
    }
    /// <summary>
    /// Flush the in-memory element lists back into <see cref="Root"/>, rebuilding every table
    /// Quartz owns and leaving the rest of the document alone.
    /// </summary>
    private void Flush() {
        JObject keyNames = Root["keys"] as JObject;
        if(keyNames == null) Root["keys"] = keyNames = [];
        foreach((string table, KvElementKind kind) in Tables) {
            JObject byTab = Root[table] as JObject;
            bool any = false;
            foreach(string tab in tabs.Keys) if(Bucket(tab, kind).Count > 0) any = true;
            // knobPositions uses skip_serializing_if in DM Note; don't materialize an empty one.
            if(!any && byTab == null && kind == KvElementKind.Knob) continue;
            if(byTab == null) Root[table] = byTab = [];
            foreach(string tab in tabs.Keys) {
                List<KvElement> list = Bucket(tab, kind);
                JArray arr = [];
                foreach(KvElement el in list) arr.Add(Container(el));
                byTab[tab] = arr;
            }
        }
        // keys[tab] must stay index-parallel and equal-length with keyPositions[tab].
        // Nothing in DM Note validates this — a mismatch loads and renders garbage.
        foreach(string tab in tabs.Keys) {
            JArray names = [];
            foreach(KvElement el in Bucket(tab, KvElementKind.Key)) names.Add(el.GlobalKey ?? "");
            keyNames[tab] = names;
        }
        Root["selectedKeyType"] = SelectedTab;
    }
    /// <summary>
    /// The object to serialize for <paramref name="el"/>. Elements whose source nested geometry
    /// under "position" are written back in that same shape, so the document keeps whatever
    /// convention it arrived with.
    /// </summary>
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
