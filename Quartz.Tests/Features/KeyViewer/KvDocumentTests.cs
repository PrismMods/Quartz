using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static class KvDocumentTests {
    /// <summary>
    /// A preset exercising every class of field Quartz does not model: top-level tables it
    /// never reads, per-element fields it never renders, a gradient noteColor union, an
    /// opaque groupId/layerName, a counter animation curve, and a knob element.
    /// </summary>
    private const string Preset = """
        {
          "selectedKeyType": "4key",
          "keys": { "4key": ["LEFT SHIFT", "25", "Z"] },
          "keyPositions": {
            "4key": [
              { "dx": 0, "dy": 0, "width": 60, "height": 60, "count": 1234,
                "noteColor": "#24BBB4", "noteOpacity": 80,
                "soundPath": "dmnote-local-sound://abc", "soundVolume": 85, "soundEnabled": true,
                "noteBorderRadius": 2, "noteBorderWidth": 1, "noteBorderColor": "#FF0000",
                "noteBorderOpacity": 100, "noteBorderSide": "vertical",
                "fontWeight": 700, "fontItalic": true, "fontFamily": "Pretendard",
                "useInlineStyles": true, "layerName": "my layer", "groupId": "grp-7",
                "counter": {
                  "enabled": true, "fontSize": 16, "fontWeight": 700, "fontFamily": "X",
                  "fontItalic": false, "fontStrikethrough": false,
                  "fill": { "idle": "#797979", "active": "#FFFFFF" },
                  "animation": { "enabled": true, "presetId": "user-bounce",
                                 "bezier": [0.25, -1.5, 0.45, 1.9], "scale": 1.4, "durationMs": 250 }
                } },
              { "dx": 70, "dy": 0, "width": 60, "height": 60, "count": 0,
                "noteColor": { "type": "gradient", "top": "#FF0000", "bottom": "#0000FF" },
                "noteOpacity": 80 },
              { "dx": 140, "dy": 0, "width": 60, "height": 60, "count": 7,
                "noteColor": "#FFFFFF", "noteOpacity": 80 }
            ]
          },
          "statPositions": {
            "4key": [ { "statType": "kps", "dx": 0, "dy": 100, "width": 100, "height": 30,
                        "count": 0, "noteColor": "#FFFFFF", "noteOpacity": 80 } ]
          },
          "knobPositions": {
            "4key": [ { "axisId": "HIDA:1:2:3:4", "sensitivity": 1.5, "reverse": true,
                        "dx": 0, "dy": 200, "width": 60, "height": 60,
                        "count": 0, "noteColor": "#FFFFFF", "noteOpacity": 80 } ]
          },
          "backgroundColor": "#101014",
          "noteSettings": { "speed": 1000 },
          "noteEffect": true,
          "customTabs": [],
          "useCustomCSS": true,
          "customCSS": { "path": "/tmp/x.css", "content": ".key { color: red }" },
          "useCustomJS": false,
          "customJS": { "path": null, "content": "", "plugins": [] },
          "fontSettings": { "customFonts": [ { "id": "f1", "name": "Pretendard" } ] },
          "tabNoteOverrides": { "4key": { "speed": 900 } },
          "embeddedLocalSounds": [ { "soundId": "abc", "extension": "wav", "dataBase64": "AAAA" } ],
          "laboratoryEnabled": false,
          "someFutureDmNoteField": { "added": "after this code was written" }
        }
        """;
    public static void TestMergeKeepsExistingTabsAndAddsImported() {
        // The user's layout: two tabs.
        KvDocument mine = KvDocument.Parse("""
            {"selectedKeyType":"custom-a",
             "customTabs":[{"id":"custom-a","name":"Main"},{"id":"custom-b","name":"Alt"}],
             "keys":{"custom-a":["Z"],"custom-b":["X"]},
             "keyPositions":{
               "custom-a":[{"dx":0,"dy":0,"width":60,"height":60,"count":5,"noteColor":"#FFF","noteOpacity":80}],
               "custom-b":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """);
        // An imported preset whose tab name collides with an existing one.
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
        // Its name is de-duplicated off the existing "Main".
        Assert(mine.TabName(added) != "Main" && mine.TabName(added).StartsWith("Main"), "name de-duped: " + mine.TabName(added));
        // The existing tabs' data is untouched.
        Assert(mine.Elements("custom-a", KvElementKind.Key)[0].Count == 5, "existing element data is preserved");
        // The imported element came across.
        Assert(mine.Elements(added, KvElementKind.Key)[0].GlobalKey == "C", "the imported binding came across");
        // And a full round-trip still parses.
        JObject after2 = JObject.Parse(mine.ToJson());
        Assert(((JObject)after2["keys"]!).Count == 3, "three tabs serialize");
    }
    public static void TestEmbeddedCssIsExtractedForImport() {
        // A CSS-styled DM Note preset keeps its look in the top-level customCSS table, not on the
        // elements — importing must surface it or the preset renders as bare boxes.
        KvDocument styled = KvDocument.Parse("""
            {"selectedKeyType":"t","useCustomCSS":true,
             "customCSS":{"path":"/x.css","content":".key { color: red }"},
             "keys":{"t":["Z"]},
             "keyPositions":{"t":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """);
        (bool enabled, string content) = styled.EmbeddedCss();
        Assert(enabled, "useCustomCSS is surfaced");
        Assert(content == ".key { color: red }", "the CSS content is surfaced");
        // A preset with no CSS reports none, so an import can clear stale CSS rather than inherit it.
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
        // A plain rename lands the requested name.
        Assert(doc.RenameTab("custom-a", "Main") == "Main", "a free name is taken as-is");
        Assert(doc.TabName("custom-a") == "Main", "the rename is reflected");
        // Colliding with another tab's name is suffixed, not rejected — the whole point is telling
        // two same-layout tabs apart.
        string taken = doc.RenameTab("custom-a", "16 Keys 2");
        Assert(taken != "16 Keys 2" && taken.StartsWith("16 Keys 2"), "a taken name is uniquified: " + taken);
        // Renaming a tab to the name it already holds is a no-op, not a self-collision suffix.
        Assert(doc.RenameTab("custom-b", "16 Keys 2") == "16 Keys 2", "renaming to the current name keeps it");
        // Blank and unknown are refused.
        Assert(doc.RenameTab("custom-a", "   ") == null, "a blank name is refused");
        Assert(doc.RenameTab("nope", "X") == null, "an unknown tab is refused");
        // The new names survive serialization in customTabs.
        JObject after = JObject.Parse(doc.ToJson());
        bool found = false;
        foreach(JToken t in (JArray)after["customTabs"]!)
            if(t["id"]!.ToString() == "custom-b" && t["name"]!.ToString() == "16 Keys 2") found = true;
        Assert(found, "renamed tab names round-trip through customTabs");
    }
    public static void TestRoundTripPreservesUnmodelledData() {
        KvDocument doc = KvDocument.Parse(Preset);
        // Edit something, the way the editor would.
        KvElement first = doc.Elements("4key", KvElementKind.Key)[0];
        first.MoveTo(11f, 22f);
        JObject after = JObject.Parse(doc.ToJson());
        JObject before = JObject.Parse(Preset);
        // The edit landed.
        JObject k0 = (JObject)after["keyPositions"]!["4key"]![0]!;
        Assert(k0["dx"]!.ToObject<float>() == 11f, "dx written");
        Assert(k0["dy"]!.ToObject<float>() == 22f, "dy written");
        // Top-level tables Quartz never reads survive byte-for-byte.
        foreach(string table in new[] {
            "backgroundColor", "noteSettings", "noteEffect", "useCustomCSS", "customCSS",
            "useCustomJS", "customJS", "fontSettings", "tabNoteOverrides",
            "embeddedLocalSounds", "laboratoryEnabled", "someFutureDmNoteField",
        }) {
            Assert(JToken.DeepEquals(before[table], after[table]), table + " survives round trip");
        }
        // Per-element fields Quartz never renders survive.
        JObject b0 = (JObject)before["keyPositions"]!["4key"]![0]!;
        foreach(string field in new[] {
            "soundPath", "soundVolume", "soundEnabled", "noteBorderRadius", "noteBorderWidth",
            "noteBorderColor", "noteBorderOpacity", "noteBorderSide", "fontWeight", "fontItalic",
            "fontFamily", "useInlineStyles", "layerName", "groupId", "counter",
        }) {
            Assert(JToken.DeepEquals(b0[field], k0[field]), "keyPosition." + field + " survives");
        }
        // count is a live counter DM Note persists; losing it silently resets the user's stats.
        Assert(k0["count"]!.ToObject<int>() == 1234, "count preserved");
        // noteColor is a string|gradient union, not a string.
        JToken grad = after["keyPositions"]!["4key"]![1]!["noteColor"]!;
        Assert(grad["type"]!.ToString() == "gradient", "gradient noteColor stays a gradient object");
        // Knobs are unrendered by Quartz but must survive an edit-and-export.
        JObject knob = (JObject)after["knobPositions"]!["4key"]![0]!;
        Assert(knob["axisId"]!.ToString() == "HIDA:1:2:3:4", "knob axisId survives");
        Assert(knob["sensitivity"]!.ToObject<float>() == 1.5f, "knob sensitivity survives");
        // Stats keep their discriminator, flattened alongside geometry.
        Assert(after["statPositions"]!["4key"]![0]!["statType"]!.ToString() == "kps", "statType survives");
    }
    public static void TestRoundTripKeepsArraysParallel() {
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements("4key", KvElementKind.Key);
        Assert(keys.Count == 3, "parsed key count");
        Assert(keys[0].GlobalKey == "LEFT SHIFT", "globalKey mirrored from keys[]");
        // DM Note's numeric escapes are opaque strings, not indices. 25 is VK_HANJA, the
        // physical Right Ctrl — parsing it as a number would bind the wrong key.
        Assert(keys[1].GlobalKey == "25", "numeric globalKey stays opaque");
        // Removing an element must renumber both arrays together; nothing in DM Note
        // validates the invariant, and a mismatch loads and renders garbage.
        doc.Remove("4key", keys[1]);
        JObject after = JObject.Parse(doc.ToJson());
        JArray names = (JArray)after["keys"]!["4key"]!;
        JArray positions = (JArray)after["keyPositions"]!["4key"]!;
        Assert(names.Count == 2, "keys[] shrank");
        Assert(positions.Count == 2, "keyPositions[] shrank");
        Assert(names[0].ToString() == "LEFT SHIFT", "surviving name 0");
        Assert(names[1].ToString() == "Z", "surviving name 1");
        Assert(positions[0]["dx"]!.ToObject<float>() == 0f, "surviving position 0 tracks its name");
        Assert(positions[1]["dx"]!.ToObject<float>() == 140f, "surviving position 1 tracks its name");
    }
    public static void TestAuthoredElementsCarryRequiredFields() {
        // DM Note's KeyPosition has no serde defaults for these seven. A position object
        // missing any one fails the entire preset load with "invalid-preset" — so anything
        // Quartz authors from scratch must carry them or the export is unopenable.
        KvDocument doc = KvDocument.Empty();
        KvElement el = KvElement.Wrap([], KvElementKind.Key, "Z");
        doc.Add(doc.SelectedTab, el);
        JObject pos = (JObject)JObject.Parse(doc.ToJson())["keyPositions"]![doc.SelectedTab]![0]!;
        foreach(string required in new[] {
            "dx", "dy", "width", "height", "count", "noteColor", "noteOpacity",
        }) {
            Assert(pos[required] != null, "authored element carries required field " + required);
        }
    }
    /// <summary>
    /// DM Note's deserializer is asymmetric: it accepts an integer where an f64 is declared, but
    /// REJECTS a float where an integer is declared — and one bad field fails the entire preset
    /// load with "invalid-preset", so a single 0.0 makes the whole export unopenable. These are the
    /// integer-typed fields on its KeyPosition struct (count, noteOpacity, noteGlowOpacity,
    /// noteBorderOpacity: u32; zIndex: Option&lt;i32&gt;).
    /// </summary>
    public static void TestIntegerFieldsNeverSerializeAsFloats() {
        string[] intFields = ["count", "noteOpacity", "noteGlowOpacity", "noteBorderOpacity", "zIndex"];
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        KvElement el = KvElement.Wrap([], KvElementKind.Key, "Z");
        doc.Add(tab, el);
        // The editor's own z-order paths, both of which used to write a float.
        el.Z = 3f;
        doc.ReindexZOrder(tab);
        JObject pos = (JObject)JObject.Parse(doc.ToJson())["keyPositions"]![tab]![0]!;
        foreach(string field in intFields) {
            JToken t = pos[field];
            if(t == null || t.Type == JTokenType.Null) continue;
            Assert(t.Type == JTokenType.Integer,
                field + " must serialize as an integer, not " + t.Type + " — a float fails DM Note's whole preset load");
        }
    }
    public static void TestParseRejectsNonPresets() {
        bool rejected = false;
        try {
            KvDocument.Parse("""{"hello":"world"}""");
        } catch(FormatException) {
            rejected = true;
        }
        Assert(rejected, "a non-preset json is rejected rather than silently becoming an empty layout");
        // "positions" is DM Note's older spelling of "keyPositions".
        KvDocument legacy = KvDocument.Parse("""
            {"keys":{"4key":["Z"]},
             "positions":{"4key":[{"dx":5,"dy":0,"width":60,"height":60,
                                   "count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """);
        Assert(legacy.Elements("4key", KvElementKind.Key).Count == 1, "legacy positions key parsed");
        JObject after = JObject.Parse(legacy.ToJson());
        Assert(after["keyPositions"] != null, "legacy positions normalized to keyPositions");
    }
    /// <summary>
    /// The foot row has to be findable again to regrow or drop it, and the flags it actually
    /// renders with — counter off, excluded from the total — are all things a user can set on a key
    /// they placed themselves. So the marker is explicit, and it is the only thing foot removal may
    /// key off.
    /// </summary>
    public static void TestFootMarkerIsExplicitAndRoundTrips() {
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements("4key", KvElementKind.Key);
        Assert(!keys[0].Foot, "an element is not a foot key unless it says so");
        // A hand-authored key configured the way a foot key looks is still not one.
        keys[0].CountInTotal = false;
        Assert(!keys[0].Foot, "excluding a key from the total does not make it a foot key");
        keys[1].Foot = true;
        Assert(keys[1].Raw["quartzFoot"]!.ToObject<bool>(), "the marker is written where DM Note ignores it");
        KvDocument reparsed = KvDocument.Parse(doc.ToJson());
        List<KvElement> back = reparsed.Elements("4key", KvElementKind.Key);
        Assert(!back[0].Foot && back[1].Foot, "the marker survives a DM Note round trip");
        Assert(!back[0].CountInTotal, "quartzCountInTotal and quartzFoot are independent");
        // Cleared rather than written false, so a layout with no foot row carries no trace of one.
        back[1].Foot = false;
        Assert(back[1].Raw["quartzFoot"] == null, "clearing the marker removes the key");
    }
    /// <summary>
    /// Per-key KPS is a Quartz extension on a schema Quartz does not own, so it has to cost a
    /// DM Note preset nothing until someone turns it on: off is the absence of the key, not a
    /// false written onto every element of every export.
    /// </summary>
    public static void TestPerKeyKpsIsOptOutAndRoundTrips() {
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements("4key", KvElementKind.Key);
        // A preset authored in DM Note carries no such key, so every element it loads reads off —
        // which is what keeps DM Note mode rendering exactly as it did.
        Assert(!keys[0].PerKeyKps, "an element shows its total unless it says otherwise");
        Assert(keys[0].Raw["quartzPerKeyKps"] == null, "the default writes nothing");
        keys[0].PerKeyKps = true;
        Assert(keys[0].Raw["quartzPerKeyKps"]!.ToObject<bool>(), "the flag is written where DM Note ignores it");
        KvDocument reparsed = KvDocument.Parse(doc.ToJson());
        List<KvElement> back = reparsed.Elements("4key", KvElementKind.Key);
        Assert(back[0].PerKeyKps && !back[1].PerKeyKps, "the flag survives a DM Note round trip, per element");
        // Cleared rather than written false, so an untouched export stays byte-identical to DM
        // Note's own and a layout with no per-key KPS carries no trace of it.
        back[0].PerKeyKps = false;
        Assert(back[0].Raw["quartzPerKeyKps"] == null, "clearing the flag removes the key");
        // It rides alongside the other extensions rather than implying any of them.
        keys[1].PerKeyKps = true;
        Assert(keys[1].CountInTotal && !keys[1].Foot, "per-key KPS is independent of the total and the foot marker");
    }
    public static void TestTabsCreateNameAndRemove() {
        KvDocument doc = KvDocument.Empty();
        string first = doc.SelectedTab;
        Assert(!doc.RemoveTab(first), "the last tab cannot be removed — SelectedTab must name a tab that exists");
        // DM Note's own id shape, and distinct even for two tabs made in the same millisecond.
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
        // Selection lands on the tab before the removed one, as DM Note's custom_tabs_delete does.
        Assert(doc.SelectedTab == a, "removing the selected tab selects the one before it");
        JObject after = JObject.Parse(doc.ToJson());
        // Flush rebuilds these from the tabs it knows about but never prunes ones it does not, so a
        // leftover entry would be written back on every save and re-materialize the tab on import.
        foreach(string table in new[] { "keys", "keyPositions" })
            Assert(after[table]![b] == null, table + " loses the removed tab");
        foreach(JToken entry in (JArray)after["customTabs"]!)
            Assert(entry["id"]!.ToString() != b, "customTabs loses the removed tab");
        Assert(after["selectedKeyType"]!.ToString() == a, "the surviving selection is written out");
    }
    /// <summary>
    /// The Key Limiter sync reads the layout through this, so it has to answer with the elements
    /// the renderer draws and nothing else. A hidden element is not drawn, never lights and cannot
    /// count, so allowing its key would let through a key the viewer does not show.
    /// </summary>
    public static void TestBoundKeyElementsMatchWhatTheViewerDraws() {
        const string tab = "4key";
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements(tab, KvElementKind.Key);
        Assert(doc.BoundKeyElements(tab).Count == 3, "every bound, visible key is one the viewer shows");
        keys[0].Hidden = true;
        List<KvElement> visible = doc.BoundKeyElements(tab);
        Assert(visible.Count == 2 && !visible.Contains(keys[0]), "a hidden key is not a key the viewer shows");
        // "25" is DM Note's opaque name for Right Ctrl — a binding like any other. Only an empty
        // one is unbound.
        keys[1].GlobalKey = "";
        Assert(doc.BoundKeyElements(tab).Count == 1, "an unbound element carries no key to sync");
        // Foot keys are not a case here, and must not become one: Simple mode syncs its foot keys,
        // and a layout foot key is an ordinary element that happens to sit lower.
        keys[2].Foot = true;
        keys[0].Hidden = false;
        List<KvElement> back = doc.BoundKeyElements(tab);
        Assert(back.Count == 2 && back[0] == keys[0] && back[1] == keys[2],
            "unhiding puts it back, foot keys included, in document order");
        Assert(doc.Elements(tab, KvElementKind.Stat).Count == 1 && doc.Elements(tab, KvElementKind.Knob).Count == 1,
            "the stat and knob this preset carries are bound to no key and stay out");
    }
    /// <summary>
    /// Removing a tab must not reach into the tab-keyed tables Quartz does not model. DM Note's own
    /// delete leaves them, and this document's whole contract is that what it does not model, it
    /// does not touch.
    /// </summary>
    public static void TestRemoveTabLeavesUnmodelledTablesAlone() {
        KvDocument doc = KvDocument.Parse(Preset);
        doc.EnsureTab("custom-other", "Other");
        doc.SelectedTab = "custom-other";
        Assert(doc.RemoveTab("4key"), "a builtin tab is removable like any other");
        JObject after = JObject.Parse(doc.ToJson());
        Assert(after["tabNoteOverrides"]!["4key"] != null, "tabNoteOverrides is left as authored");
        Assert(after["keys"]!["4key"] == null, "the tables Quartz owns are still pruned");
    }
    // ---- DM Note import contract ------------------------------------------------
    // The rules below are what DM Note's Rust deserializer ACTUALLY enforces, read from its
    // v1.6.1 source (src-tauri/src/models/mod.rs) and confirmed by running the real
    // serde_json::from_str::<PresetFile> against Quartz exports. A violation fails the ENTIRE
    // preset load ("invalid-preset"), not just the offending element — so these are export
    // blockers, and this test exists so a schema drift is caught here instead of in DM Note.
    //
    // Fields with no #[serde(default)] — absence fails the load. Present on every table (the
    // stat/graph/knob structs #[serde(flatten)] a KeyPosition, so they inherit these).
    private static readonly string[] RequiredEverywhere =
        ["dx", "dy", "width", "height", "count", "noteColor", "noteOpacity"];
    // Integer-typed in Rust (u32, or Option<i32> for zIndex). serde REJECTS a float here, even a
    // whole-valued one like 0.0 — the bug that broke every early export.
    private static readonly string[] IntegerFields =
        ["count", "noteOpacity", "noteGlowOpacity", "noteBorderOpacity", "zIndex"];
    // Enum-typed fields and the exact strings their variants serialize to (rename_all applied):
    // statType lowercase, the counter/note alignments kebab-case (single words, so lowercase).
    private static readonly Dictionary<string, string[]> EnumFields = new() {
        ["noteAlignment"] = ["left", "center", "right"],
    };
    private static readonly Dictionary<string, string[]> CounterEnumFields = new() {
        ["placement"] = ["inside", "outside"],
        ["align"] = ["top", "bottom", "left", "right"],
        ["alignMode"] = ["center", "between"],
    };
    /// <summary>
    /// Validate one serialized preset against the rules above, returning the first violation or
    /// null. Kept as data so a new DM Note field is one array entry, not new assertion code.
    /// </summary>
    private static string DmNoteImportViolation(string json) {
        JObject root = JObject.Parse(json);
        JObject keys = root["keys"] as JObject;
        foreach((string table, string[] disc) in new (string, string[])[] {
            ("keyPositions", []),
            ("statPositions", ["statType"]),
            ("graphPositions", ["statType", "graphType", "graphSpeed", "graphColor"]),
            ("knobPositions", []),
        }) {
            if(root[table] is not JObject byTab) continue;
            foreach(JProperty tab in byTab.Properties()) {
                if(tab.Value is not JArray arr) continue;
                for(int i = 0; i < arr.Count; i++) {
                    JObject outer = arr[i] as JObject;
                    JObject p = outer?["position"] as JObject ?? outer;
                    if(p == null) return $"{table}[{tab.Name}][{i}] is not an object";
                    string where = $"{table}[{tab.Name}][{i}]";
                    foreach(string req in RequiredEverywhere)
                        if(p[req] == null) return $"{where} missing required '{req}'";
                    // Discriminators live on the OUTER object for stat/graph/knob.
                    foreach(string d in disc)
                        if((outer ?? p)[d] == null) return $"{where} missing '{d}'";
                    foreach(string f in IntegerFields) {
                        JToken t = p[f];
                        if(t != null && t.Type == JTokenType.Float)
                            return $"{where}.{f} is a float ({t}); DM Note declares it an integer";
                    }
                    foreach((string f, string[] legal) in EnumFields) {
                        JToken t = p[f];
                        if(t != null && Array.IndexOf(legal, t.ToString()) < 0)
                            return $"{where}.{f}='{t}' is not a legal DM Note value";
                    }
                    if(p["counter"] is JObject counter)
                        foreach((string f, string[] legal) in CounterEnumFields) {
                            JToken t = counter[f];
                            if(t != null && Array.IndexOf(legal, t.ToString()) < 0)
                                return $"{where}.counter.{f}='{t}' is not a legal DM Note value";
                        }
                    JToken nc = p["noteColor"];
                    if(nc is { Type: not JTokenType.String } and JObject nco
                       && (nco["top"] == null || nco["bottom"] == null))
                        return $"{where}.noteColor object is not a valid gradient";
                }
                // keys[tab] and keyPositions[tab] must be equal-length and parallel.
                if(table == "keyPositions" && keys?[tab.Name] is JArray names && names.Count != arr.Count)
                    return $"keys[{tab.Name}]={names.Count} but keyPositions={arr.Count} (not parallel)";
            }
        }
        return null;
    }
    /// <summary>
    /// A generated Quartz layout, serialized, satisfies DM Note's import contract — every table,
    /// including a stat and a graph with their required discriminators. Built through the model
    /// (KvPresets is Unity-bound and cannot be reached from Unity-free tests), so it exercises the
    /// same KvElement/KvDocument write paths the editor and generators use.
    /// </summary>
    public static void TestGeneratedLayoutPassesDmNoteImport() {
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        // A key with a counter and a gradient note, both legal.
        KvElement key = KvElement.Wrap([], KvElementKind.Key, "Z");
        key.MoveTo(0f, 0f);
        key.Raw["noteColor"] = new JObject { ["type"] = "gradient", ["top"] = "#FFF", ["bottom"] = "#000" };
        key.Raw["counter"] = new JObject { ["enabled"] = true, ["align"] = "bottom", ["placement"] = "inside" };
        key.Z = 0;
        doc.Add(tab, key);
        // A stat: statType is mandatory and lowercase.
        KvElement stat = KvElement.Wrap([], KvElementKind.Stat, "");
        stat.StatType = "kps";
        stat.MoveTo(0f, 60f);
        stat.Z = 1;
        doc.Add(tab, stat);
        // A graph: statType (camelCase) + graphType (lowercase) + graphSpeed + graphColor mandatory.
        KvElement graph = KvElement.Wrap([], KvElementKind.Graph, "");
        graph.StatType = "kpsAvg";
        graph.Raw["graphType"] = "line";
        graph.Raw["graphSpeed"] = 1000;
        graph.Raw["graphColor"] = "#86EFAC";
        graph.MoveTo(0f, 120f);
        graph.Z = 2;
        doc.Add(tab, graph);
        string violation = DmNoteImportViolation(doc.ToJson());
        Assert(violation == null, "generated layout must pass DM Note import, but: " + violation);
    }
    /// <summary>The validator has teeth: a float zIndex — the real historical bug — is caught.</summary>
    public static void TestDmNoteImportValidatorCatchesFloatIntField() {
        // A hand-built preset with the exact defect that shipped: zIndex serialized as 0.0.
        string bad = """
            {"selectedKeyType":"t","keys":{"t":["Z"]},
             "keyPositions":{"t":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,
               "noteColor":"#FFF","noteOpacity":80,"zIndex":0.5}]}}
            """;
        Assert(DmNoteImportViolation(bad) != null, "a float in an integer field must be flagged");
        // And a genuinely valid preset passes the same validator, so it is not just always-failing.
        Assert(DmNoteImportViolation(Preset) == null, "the fixture preset satisfies the import rules");
    }
    /// <summary>
    /// The render anchor freezes the overlay's coordinate mapping per tab: set once, it survives a
    /// save/load round-trip (else the overlay would jump at every restart), is absent until seeded
    /// (the renderer's cue to capture it from the current bounds), and dies with its tab.
    /// </summary>
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
}
