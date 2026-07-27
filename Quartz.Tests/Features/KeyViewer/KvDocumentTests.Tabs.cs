using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static partial class KvDocumentTests {
    public static void TestMergeKeepsExistingTabsAndAddsImported() {
        KvDocument mine = KvDocument.Parse("""
            {"selectedKeyType":"custom-a",
             "customTabs":[{"id":"custom-a","name":"Main"},{"id":"custom-b","name":"Alt"}],
             "keys":{"custom-a":["Z"],"custom-b":["X"]},
             "keyPositions":{
               "custom-a":[{"dx":0,"dy":0,"width":60,"height":60,"count":5,"noteColor":"#FFF","noteOpacity":80}],
               "custom-b":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """);
        KvDocument imported = KvDocument.Parse("""
            {"selectedKeyType":"custom-a",
             "customTabs":[{"id":"custom-a","name":"Main"}],
             "keys":{"custom-a":["C"]},
             "keyPositions":{"custom-a":[{"dx":9,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#0F0","noteOpacity":80}]}}
            """);
        int before = 0;
        foreach(string _ in mine.Tabs) before++;
        string added = mine.MergeFrom(imported);
        int after = 0;
        foreach(string _ in mine.Tabs) after++;
        Assert(after == before + 1, "the imported tab is added, existing tabs kept");
        Assert(added != null && mine.HasTab(added), "the added tab id is returned and real");
        Assert(added != "custom-a", "the imported tab gets a fresh id, not the colliding one");
        Assert(mine.TabName(added) != "Main" && mine.TabName(added).StartsWith("Main"), "name de-duped: " + mine.TabName(added));
        Assert(mine.Elements("custom-a", KvElementKind.Key)[0].Count == 5, "existing element data is preserved");
        Assert(mine.Elements(added, KvElementKind.Key)[0].GlobalKey == "C", "the imported binding came across");
        JObject after2 = JObject.Parse(mine.ToJson());
        Assert(((JObject)after2["keys"]!).Count == 3, "three tabs serialize");
    }
    public static void TestEmbeddedCssIsExtractedForImport() {
        KvDocument styled = KvDocument.Parse("""
            {"selectedKeyType":"t","useCustomCSS":true,
             "customCSS":{"path":"/x.css","content":".key { color: red }"},
             "keys":{"t":["Z"]},
             "keyPositions":{"t":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """);
        (bool enabled, string content) = styled.EmbeddedCss();
        Assert(enabled, "useCustomCSS is surfaced");
        Assert(content == ".key { color: red }", "the CSS content is surfaced");
        (bool none, string empty) = KvDocument.Parse("""
            {"selectedKeyType":"t","keys":{"t":["Z"]},
             "keyPositions":{"t":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """).EmbeddedCss();
        Assert(!none && empty.Length == 0, "a preset without CSS reports none");
    }
    public static void TestRenameTabIsUniqueAndReversible() {
        string preset = """
            {"selectedKeyType":"custom-a",
             "customTabs":[{"id":"custom-a","name":"16 Keys"},{"id":"custom-b","name":"16 Keys 2"}],
             "keys":{"custom-a":["Z"],"custom-b":["X"]},
             "keyPositions":{
               "custom-a":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}],
               "custom-b":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """;
        KvDocument doc = KvDocument.Parse(preset);
        Assert(doc.RenameTab("custom-a", "Main") == "Main", "a free name is taken as-is");
        Assert(doc.TabName("custom-a") == "Main", "the rename is reflected");
        string taken = doc.RenameTab("custom-a", "16 Keys 2");
        Assert(taken != "16 Keys 2" && taken.StartsWith("16 Keys 2"), "a taken name is uniquified: " + taken);
        Assert(doc.RenameTab("custom-b", "16 Keys 2") == "16 Keys 2", "renaming to the current name keeps it");
        Assert(doc.RenameTab("custom-a", "   ") == null, "a blank name is refused");
        Assert(doc.RenameTab("nope", "X") == null, "an unknown tab is refused");
        JObject after = JObject.Parse(doc.ToJson());
        bool found = false;
        foreach(JToken t in (JArray)after["customTabs"]!)
            if(t["id"]!.ToString() == "custom-b" && t["name"]!.ToString() == "16 Keys 2") found = true;
        Assert(found, "renamed tab names round-trip through customTabs");
    }
    public static void TestTabsCreateNameAndRemove() {
        KvDocument doc = KvDocument.Empty();
        string first = doc.SelectedTab;
        Assert(!doc.RemoveTab(first), "the last tab cannot be removed — SelectedTab must name a tab that exists");
        string a = doc.NewTabId();
        Assert(a.StartsWith("custom-"), "new tab ids follow DM Note's custom-{millis}");
        doc.EnsureTab(a, doc.UniqueTabName("8 Keys"));
        string b = doc.NewTabId();
        Assert(b != a, "a second id does not collide with the first");
        Assert(doc.UniqueTabName("8 Keys") == "8 Keys 2", "a duplicate preset name is suffixed — DM Note rejects duplicates");
        doc.EnsureTab(b, doc.UniqueTabName("8 Keys"));
        Assert(doc.TabName(b) == "8 Keys 2", "the registered name is what comes back");
        Assert(doc.TabName("custom-unregistered") == "custom-unregistered", "an unregistered tab falls back to its id");
        Assert(doc.CustomTabCount == 3, "every tab here is a registered custom tab");
        doc.Add(b, KvElement.Wrap([], KvElementKind.Key));
        doc.SelectedTab = b;
        Assert(doc.RemoveTab(b), "a tab that is not the last one goes");
        Assert(doc.SelectedTab == a, "removing the selected tab selects the one before it");
        JObject after = JObject.Parse(doc.ToJson());
        foreach(string table in new[] { "keys", "keyPositions" })
            Assert(after[table]![b] == null, table + " loses the removed tab");
        foreach(JToken entry in (JArray)after["customTabs"]!)
            Assert(entry["id"]!.ToString() != b, "customTabs loses the removed tab");
        Assert(after["selectedKeyType"]!.ToString() == a, "the surviving selection is written out");
    }
    public static void TestRemoveTabLeavesUnmodelledTablesAlone() {
        KvDocument doc = KvDocument.Parse(Preset);
        doc.EnsureTab("custom-other", "Other");
        doc.SelectedTab = "custom-other";
        Assert(doc.RemoveTab("4key"), "a builtin tab is removable like any other");
        JObject after = JObject.Parse(doc.ToJson());
        Assert(after["tabNoteOverrides"]!["4key"] != null, "tabNoteOverrides is left as authored");
        Assert(after["keys"]!["4key"] == null, "the tables Quartz owns are still pruned");
    }
    public static void TestRenderAnchorPersistsAndDiesWithItsTab() {
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        Assert(!doc.TryGetRenderAnchor(tab, out _, out _), "a fresh tab has no anchor until the renderer seeds it");
        doc.SetRenderAnchor(tab, 123.5f, -40f);
        Assert(doc.TryGetRenderAnchor(tab, out float x, out float y) && x == 123.5f && y == -40f,
            "the anchor reads back what was set");
        KvDocument reloaded = KvDocument.Parse(doc.ToJson());
        Assert(reloaded.TryGetRenderAnchor(tab, out x, out y) && x == 123.5f && y == -40f,
            "the anchor survives a serialize/parse round-trip");
        string second = doc.NewTabId();
        doc.EnsureTab(second, doc.UniqueTabName("Other"));
        doc.SetRenderAnchor(second, 1f, 2f);
        doc.Add(second, KvElement.Wrap([], KvElementKind.Key));
        doc.SelectedTab = second;
        Assert(doc.RemoveTab(second), "the second tab goes");
        Assert(!doc.TryGetRenderAnchor(second, out _, out _), "a removed tab's anchor is pruned with it");
        Assert(doc.TryGetRenderAnchor(tab, out _, out _), "the surviving tab keeps its anchor");
        Assert((JObject.Parse(doc.ToJson())["quartzRenderAnchors"] as JObject)?[second] == null,
            "the pruned anchor is gone from the serialized document too");
    }
    public static void TestExportKeepsOnlyTheSelectedTab() {
        KvDocument doc = KvDocument.Parse(Preset);
        doc.EnsureTab("custom-a", "Second");
        doc.Add("custom-a", KvElement.Wrap(new JObject { ["dx"] = 5f, ["dy"] = 6f }, KvElementKind.Key, "Q"));
        doc.SetRenderAnchor("4key", 1f, 2f);
        doc.SetRenderAnchor("custom-a", 3f, 4f);
        doc.SelectedTab = "custom-a";
        JObject root = JObject.Parse(doc.ToJson());
        Assert(((JObject)root["keyPositions"]!).Count == 2, "both tabs are in the saved layout");
        KvExportShaping.KeepOnlyTab(root, "custom-a");
        foreach(string table in new[] { "keys", "keyPositions", "quartzRenderAnchors" }) {
            JObject byTab = (JObject)root[table]!;
            Assert(byTab.Count == 1 && byTab["custom-a"] != null, table + " keeps only the exported tab");
        }
        Assert(root["selectedKeyType"]!.ToString() == "custom-a", "the export selects the exported tab");
        JArray custom = (JArray)root["customTabs"]!;
        Assert(custom.Count == 1 && custom[0]!["id"]!.ToString() == "custom-a", "customTabs keeps only the exported tab");
        Assert(KvDocument.Parse(root.ToString()).Elements("custom-a", KvElementKind.Key).Count == 1,
            "the single-tab export still parses as a preset");
        JObject builtin = JObject.Parse(doc.ToJson());
        KvExportShaping.KeepOnlyTab(builtin, "4key");
        Assert(builtin["customTabs"] == null, "exporting a builtin tab drops the custom tab list");
        Assert(((JObject)builtin["keyPositions"]!)["4key"] is JArray { Count: 3 }, "the builtin tab keeps its keys");
    }
}
