using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static partial class KvDocumentTests {
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
    public static void TestRoundTripPreservesUnmodelledData() {
        KvDocument doc = KvDocument.Parse(Preset);
        KvElement first = doc.Elements("4key", KvElementKind.Key)[0];
        first.MoveTo(11f, 22f);
        JObject after = JObject.Parse(doc.ToJson());
        JObject before = JObject.Parse(Preset);
        JObject k0 = (JObject)after["keyPositions"]!["4key"]![0]!;
        Assert(k0["dx"]!.ToObject<float>() == 11f, "dx written");
        Assert(k0["dy"]!.ToObject<float>() == 22f, "dy written");
        foreach(string table in new[] {
            "backgroundColor", "noteSettings", "noteEffect", "useCustomCSS", "customCSS",
            "useCustomJS", "customJS", "fontSettings", "tabNoteOverrides",
            "embeddedLocalSounds", "laboratoryEnabled", "someFutureDmNoteField",
        }) {
            Assert(JToken.DeepEquals(before[table], after[table]), table + " survives round trip");
        }
        JObject b0 = (JObject)before["keyPositions"]!["4key"]![0]!;
        foreach(string field in new[] {
            "soundPath", "soundVolume", "soundEnabled", "noteBorderRadius", "noteBorderWidth",
            "noteBorderColor", "noteBorderOpacity", "noteBorderSide", "fontWeight", "fontItalic",
            "fontFamily", "useInlineStyles", "layerName", "groupId", "counter",
        }) {
            Assert(JToken.DeepEquals(b0[field], k0[field]), "keyPosition." + field + " survives");
        }
        Assert(k0["count"]!.ToObject<int>() == 1234, "count preserved");
        JToken grad = after["keyPositions"]!["4key"]![1]!["noteColor"]!;
        Assert(grad["type"]!.ToString() == "gradient", "gradient noteColor stays a gradient object");
        JObject knob = (JObject)after["knobPositions"]!["4key"]![0]!;
        Assert(knob["axisId"]!.ToString() == "HIDA:1:2:3:4", "knob axisId survives");
        Assert(knob["sensitivity"]!.ToObject<float>() == 1.5f, "knob sensitivity survives");
        Assert(after["statPositions"]!["4key"]![0]!["statType"]!.ToString() == "kps", "statType survives");
    }
    public static void TestRoundTripKeepsArraysParallel() {
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements("4key", KvElementKind.Key);
        Assert(keys.Count == 3, "parsed key count");
        Assert(keys[0].GlobalKey == "LEFT SHIFT", "globalKey mirrored from keys[]");
        Assert(keys[1].GlobalKey == "25", "numeric globalKey stays opaque");
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
    public static void TestMergeCarriesDmNoteEmbeddedImages() {
        KvDocument imported = KvDocument.Parse("""
            {
              "selectedKeyType": "4key",
              "keys": { "4key": ["Z"] },
              "keyPositions": { "4key": [
                { "dx": 0, "dy": 0, "width": 60, "height": 60,
                  "inactiveImage": "dmnote-local-image://image-1" }
              ] },
              "embeddedLocalImages": [
                { "imageId": "image-1", "extension": "webp", "dataBase64": "AQIDBA==" },
                { "imageId": "unused", "extension": "png", "dataBase64": "BQYHCA==" }
              ]
            }
            """);
        KvDocument destination = KvDocument.Empty();
        destination.Root["embeddedLocalImages"] = new JArray {
            new JObject {
                ["imageId"] = "image-1", ["extension"] = "png", ["dataBase64"] = "OLD=",
            },
        };
        string tab = destination.MergeFrom(imported);
        Assert(tab != null, "image-bearing tab merged");
        KvElement key = destination.Elements(tab, KvElementKind.Key)[0];
        string imageRef = key.Raw["inactiveImage"]!.ToString();
        Assert(imageRef != "dmnote-local-image://image-1", "colliding image id is remapped");
        Assert(destination.TryEmbeddedImage(imageRef, out string data, out string extension),
            "merged image reference resolves against carried payload");
        Assert(data == "AQIDBA==" && extension == "webp", "embedded image metadata survives merge");
        Assert(destination.TryEmbeddedImage(KvDocument.DmLocalImagePrefix + "image-1", out data, out extension)
            && data == "OLD=" && extension == "png", "an existing colliding image is never overwritten");
        JObject roundTrip = JObject.Parse(destination.ToJson());
        Assert(roundTrip["embeddedLocalImages"] is JArray { Count: 2 },
            "used image payload survives while an unreferenced source image is not copied");

        JObject firstImage = new() {
            ["imageId"] = "one", ["extension"] = "png", ["dataBase64"] = "AQIDBA==",
        };
        long oneImageBudget = System.Text.Encoding.UTF8.GetByteCount(firstImage.ToString(Newtonsoft.Json.Formatting.None));
        KvDocument limitedImport = KvDocument.Parse("""
            {
              "selectedKeyType": "4key",
              "keys": { "4key": ["Z"] },
              "keyPositions": { "4key": [
                { "dx": 0, "dy": 0, "width": 60, "height": 60,
                  "inactiveImage": "dmnote-local-image://one",
                  "activeImage": "dmnote-local-image://two" }
              ] },
              "embeddedLocalImages": [
                { "imageId": "one", "extension": "png", "dataBase64": "AQIDBA==" },
                { "imageId": "two", "extension": "png", "dataBase64": "BQYHCA==" },
                { "imageId": "orphan", "extension": "png", "dataBase64": "CQoLDA==" }
              ]
            }
            """);
        KvDocument limited = KvDocument.Empty();
        string limitedTab = limited.MergeFrom(
            limitedImport, oneImageBudget, out IReadOnlyList<KvEmbeddedImageWarning> budgetWarnings);
        KvElement limitedKey = limited.Elements(limitedTab, KvElementKind.Key)[0];
        Assert(limitedKey.Raw["inactiveImage"]?.ToString() == KvDocument.DmLocalImagePrefix + "one",
            "an image inside the aggregate budget remains referenced");
        Assert(limitedKey.Raw["activeImage"] == null,
            "a reference whose payload exceeds the aggregate budget is pruned instead of left dangling");
        JArray limitedImages = (JArray)limited.Root["embeddedLocalImages"]!;
        Assert(limitedImages.Count == 1 && limitedImages[0]!["imageId"]!.ToString() == "one",
            "the aggregate budget and reference scan admit only the one usable image");
        Assert(budgetWarnings.Count == 1
            && budgetWarnings[0].SourceId == "two"
            && budgetWarnings[0].Reason == KvEmbeddedImageRejectionReason.OverBudget,
            "a referenced payload rejected by the aggregate budget is reported once");

        string unsafeMissingId = "missing\n" + new string('x', 120);
        JObject rejectedRoot = new() {
            ["selectedKeyType"] = "4key",
            ["keys"] = new JObject { ["4key"] = new JArray("Z") },
            ["keyPositions"] = new JObject {
                ["4key"] = new JArray(new JObject {
                    ["dx"] = 0, ["dy"] = 0, ["width"] = 60, ["height"] = 60,
                    ["inactiveImage"] = KvDocument.DmLocalImagePrefix + unsafeMissingId,
                    ["activeImage"] = KvDocument.DmLocalImagePrefix + "invalid",
                }),
            },
            ["embeddedLocalImages"] = new JArray(new JObject {
                ["imageId"] = "invalid", ["extension"] = "png", ["dataBase64"] = "not base64",
            }),
        };
        KvDocument rejectedImport = KvDocument.Parse(rejectedRoot.ToString());
        KvDocument rejectedDestination = KvDocument.Empty();
        string rejectedTab = rejectedDestination.MergeFrom(
            rejectedImport, out IReadOnlyList<KvEmbeddedImageWarning> rejectedWarnings);
        Assert(rejectedWarnings.Count == 2, "every rejected referenced image produces one warning");
        KvEmbeddedImageWarning missingWarning = rejectedWarnings.Single(w =>
            w.Reason == KvEmbeddedImageRejectionReason.Missing);
        Assert(missingWarning.SourceId == unsafeMissingId,
            "the structured warning records the exact source id without a mutable side channel");
        Assert(!missingWarning.Message.Contains('\n') && missingWarning.Message.Length < 220
            && missingWarning.Message.Contains("missing"),
            "the log-ready warning safely bounds and escapes the image id while retaining the reason");
        Assert(rejectedWarnings.Any(w => w.SourceId == "invalid"
            && w.Reason == KvEmbeddedImageRejectionReason.Invalid),
            "an invalid referenced payload identifies its source id and reason");
        KvElement rejectedKey = rejectedDestination.Elements(rejectedTab, KvElementKind.Key)[0];
        Assert(rejectedKey.Raw["inactiveImage"] == null && rejectedKey.Raw["activeImage"] == null,
            "rejected image references remain pruned from imported elements");

        KvDocument malformedDestination = KvDocument.Empty();
        malformedDestination.Root["embeddedLocalImages"] = new JObject { ["unexpected"] = true };
        string malformedTab = malformedDestination.MergeFrom(
            imported, out IReadOnlyList<KvEmbeddedImageWarning> malformedWarnings);
        Assert(malformedTab != null && malformedWarnings.Count == 1
            && malformedWarnings[0].SourceId == "image-1"
            && malformedWarnings[0].Reason == KvEmbeddedImageRejectionReason.MalformedDestination,
            "a malformed destination reports every referenced image it prevents from merging");
        Assert(malformedDestination.Root["embeddedLocalImages"] is JObject,
            "reporting a malformed destination never overwrites it");
    }
    public static void TestAuthoredElementsCarryRequiredFields() {
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
    public static void TestIntegerFieldsNeverSerializeAsFloats() {
        string[] intFields = ["count", "noteOpacity", "noteGlowOpacity", "noteBorderOpacity", "zIndex"];
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        KvElement el = KvElement.Wrap([], KvElementKind.Key, "Z");
        doc.Add(tab, el);
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
        KvDocument legacy = KvDocument.Parse("""
            {"keys":{"4key":["Z"]},
             "positions":{"4key":[{"dx":5,"dy":0,"width":60,"height":60,
                                   "count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """);
        Assert(legacy.Elements("4key", KvElementKind.Key).Count == 1, "legacy positions key parsed");
        JObject after = JObject.Parse(legacy.ToJson());
        Assert(after["keyPositions"] != null, "legacy positions normalized to keyPositions");
    }
    public static void TestFootMarkerIsExplicitAndRoundTrips() {
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements("4key", KvElementKind.Key);
        Assert(!keys[0].Foot, "an element is not a foot key unless it says so");
        keys[0].CountInTotal = false;
        Assert(!keys[0].Foot, "excluding a key from the total does not make it a foot key");
        keys[1].Foot = true;
        Assert(keys[1].Raw["quartzFoot"]!.ToObject<bool>(), "the marker is written where DM Note ignores it");
        KvDocument reparsed = KvDocument.Parse(doc.ToJson());
        List<KvElement> back = reparsed.Elements("4key", KvElementKind.Key);
        foreach(KvElement el in back) Assert(!el.Foot, "the hand tab keeps no foot keys after a load");
        Assert(!back[0].CountInTotal, "quartzCountInTotal and quartzFoot are independent");
        string footTab = reparsed.SelectedFootTab;
        Assert(footTab != null && reparsed.IsFootTab(footTab), "foot keys land on their own foot tab");
        List<KvElement> feet = reparsed.Elements(footTab, KvElementKind.Key);
        Assert(feet.Count == 1 && feet[0].Foot, "the marker survives a DM Note round trip");
        feet[0].Foot = false;
        Assert(feet[0].Raw["quartzFoot"] == null, "clearing the marker removes the key");
    }
    public static void TestFootTabsSplitActivateAndExportMerged() {
        KvDocument doc = KvDocument.Parse(Preset);
        doc.Elements("4key", KvElementKind.Key)[2].Foot = true;
        KvDocument loaded = KvDocument.Parse(doc.ToJson());
        string foot = loaded.SelectedFootTab;
        Assert(foot != null && loaded.IsFootTab(foot), "a preset's foot row becomes its own tab");
        Assert(loaded.SelectedTab == "4key", "the hand tab stays selected");
        Assert(loaded.Elements("4key", KvElementKind.Key).Count == 2, "the hand tab loses the foot key");
        Assert(loaded.Elements(foot, KvElementKind.Key).Count == 1, "the foot tab gains it");
        JObject exported = JObject.Parse(loaded.ToJson());
        KvExportShaping.KeepOnlyTab(exported, loaded.SelectedTab, foot);
        Assert(((JArray)exported["keyPositions"]!["4key"]!).Count == 3, "export merges the foot tab back in");
        Assert(((JArray)exported["keys"]!["4key"]!).Count == 3, "names stay parallel through the merge");
        Assert(exported["quartzSelectedFootTab"] == null, "an export carries one tab and no foot pointer");
        Assert(KvDocument.Parse(exported.ToString()).SelectedFootTab != null, "a re-imported export splits again");
        Assert(!loaded.RemoveTab("4key"), "the last hand tab cannot be deleted");
        Assert(loaded.RemoveTab(foot), "a foot tab can be");
        Assert(loaded.SelectedFootTab == null, "removing the active foot tab deactivates it");
    }
    public static void TestPerKeyKpsIsOptOutAndRoundTrips() {
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements("4key", KvElementKind.Key);
        Assert(!keys[0].PerKeyKps, "an element shows its total unless it says otherwise");
        Assert(keys[0].Raw["quartzPerKeyKps"] == null, "the default writes nothing");
        keys[0].PerKeyKps = true;
        Assert(keys[0].Raw["quartzPerKeyKps"]!.ToObject<bool>(), "the flag is written where DM Note ignores it");
        KvDocument reparsed = KvDocument.Parse(doc.ToJson());
        List<KvElement> back = reparsed.Elements("4key", KvElementKind.Key);
        Assert(back[0].PerKeyKps && !back[1].PerKeyKps, "the flag survives a DM Note round trip, per element");
        back[0].PerKeyKps = false;
        Assert(back[0].Raw["quartzPerKeyKps"] == null, "clearing the flag removes the key");
        keys[1].PerKeyKps = true;
        Assert(keys[1].CountInTotal && !keys[1].Foot, "per-key KPS is independent of the total and the foot marker");
    }
    public static void TestBoundKeyElementsMatchWhatTheViewerDraws() {
        const string tab = "4key";
        KvDocument doc = KvDocument.Parse(Preset);
        List<KvElement> keys = doc.Elements(tab, KvElementKind.Key);
        Assert(doc.BoundKeyElements(tab).Count == 3, "every bound, visible key is one the viewer shows");
        keys[0].Hidden = true;
        List<KvElement> visible = doc.BoundKeyElements(tab);
        Assert(visible.Count == 2 && !visible.Contains(keys[0]), "a hidden key is not a key the viewer shows");
        keys[1].GlobalKey = "";
        Assert(doc.BoundKeyElements(tab).Count == 1, "an unbound element carries no key to sync");
        keys[2].Foot = true;
        keys[0].Hidden = false;
        List<KvElement> back = doc.BoundKeyElements(tab);
        Assert(back.Count == 2 && back[0] == keys[0] && back[1] == keys[2],
            "unhiding puts it back, foot keys included, in document order");
        Assert(doc.Elements(tab, KvElementKind.Stat).Count == 1 && doc.Elements(tab, KvElementKind.Knob).Count == 1,
            "the stat and knob this preset carries are bound to no key and stay out");
    }
    public static void TestExportFormatsSplitQuartzOnlyData() {
        KvDocument doc = KvDocument.Empty();
        string tab = doc.SelectedTab;
        KvElement key = KvElement.Wrap([], KvElementKind.Key, "Z");
        key.MoveTo(0f, 0f);
        key.Count = 4321;
        key.PressedText = "!";
        key.LabelEnabled = false;
        key.CounterShowWhilePressed = false;
        doc.Add(tab, key);
        KvElement stat = KvElement.Wrap([], KvElementKind.Stat, "");
        stat.StatType = "kpsAvg";
        stat.MoveTo(0f, 60f);
        doc.Add(tab, stat);
        string authored = doc.ToJson();
        Assert(DmNoteImportViolation(authored) != null,
            "the layout as authored must be one DM Note refuses, or this test proves nothing");
        JObject dm = JObject.Parse(authored);
        KvExportShaping.Shape(dm, KvExportFormat.DmNote);
        string violation = DmNoteImportViolation(dm.ToString());
        Assert(violation == null, "the DM Note export must pass DM Note's contract, but: " + violation);
        Assert(dm["statPositions"]?[tab]?[0]?["statType"]?.ToString() == "kps",
            "the Avg stat is written as KPS, the nearest readout DM Note has");
        Assert(!HasQuartzKey(dm), "no Quartz extension survives the DM Note export");
        Assert(dm["keyPositions"]?[tab]?[0]?["count"]?.Value<int>() == 4321,
            "a counts-included export keeps the press counts");
        JObject qkv = JObject.Parse(authored);
        KvExportShaping.Shape(qkv, KvExportFormat.Quartz);
        Assert(qkv["statPositions"]?[tab]?[0]?["statType"]?.ToString() == "kpsAvg",
            "the Quartz export keeps the readout the user chose");
        JObject qkvKey = (JObject)qkv["keyPositions"]![tab]![0]!;
        Assert(qkvKey["quartzPressedText"]?.ToString() == "!"
            && qkvKey["quartzLabelEnabled"]?.Value<bool>() == false
            && qkvKey["quartzCounterShowWhilePressed"]?.Value<bool>() == false,
            "the Quartz export keeps every per-element extension");
    }
    private const string NestedPreset = """
        {
          "selectedKeyType": "custom-a",
          "customTabs": [ { "id": "custom-a", "name": "Nested" } ],
          "keys": { "custom-a": ["Z"] },
          "keyPositions": {
            "custom-a": [
              { "className": "outer blue", "useInlineStyles": true,
                "position": { "dx": 1, "dy": 2, "width": 60, "height": 60,
                              "count": 0, "noteColor": "#FFFFFF", "noteOpacity": 80 } }
            ]
          }
        }
        """;
    public static void TestClassAndInlinePriorityRoundTrip() {
        KvDocument flat = KvDocument.Parse(Preset);
        KvElement key = flat.Elements("4key", KvElementKind.Key)[0];
        Assert(key.UseInlineStyles, "useInlineStyles is read off a flat element");
        Assert(key.ClassName.Length == 0, "an element without className reads empty");
        key.ClassName = "  blue special  ";
        Assert(key.ClassName == "blue special", "className is trimmed on write");
        JObject afterFlat = JObject.Parse(flat.ToJson());
        JObject k0 = (JObject)afterFlat["keyPositions"]!["4key"]![0]!;
        Assert(k0["className"]!.ToString() == "blue special", "className is written to the element");
        Assert(k0["useInlineStyles"]!.ToObject<bool>(), "useInlineStyles survives the round trip");
        key.ClassName = "";
        key.UseInlineStyles = false;
        JObject cleared = (JObject)JObject.Parse(flat.ToJson())["keyPositions"]!["4key"]![0]!;
        Assert(cleared["className"] == null, "an empty className removes the field");
        Assert(cleared["useInlineStyles"] == null, "turning inline priority off removes the field");
        KvDocument nested = KvDocument.Parse(NestedPreset);
        KvElement outer = nested.Elements("custom-a", KvElementKind.Key)[0];
        Assert(outer.ClassName == "outer blue", "className is read off the outer object of a nested element");
        Assert(outer.UseInlineStyles, "useInlineStyles is read off the outer object of a nested element");
        outer.ClassName = "rewritten";
        JObject entry = (JObject)JObject.Parse(nested.ToJson())["keyPositions"]!["custom-a"]![0]!;
        Assert(entry["className"]!.ToString() == "rewritten", "the outer object is rewritten");
        Assert(entry["position"]!["className"]!.ToString() == "rewritten", "the inner object is rewritten too");
        outer.ClassName = "";
        JObject gone = (JObject)JObject.Parse(nested.ToJson())["keyPositions"]!["custom-a"]![0]!;
        Assert(gone["className"] == null && gone["position"]!["className"] == null,
            "clearing className removes it from both objects");
    }
    public static void TestExportEmbedsLocalImages() {
        string dir = Path.Combine(Path.GetTempPath(), "quartz-kv-embed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try {
            string png = Path.Combine(dir, "key.png");
            byte[] pngBytes = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
            File.WriteAllBytes(png, pngBytes);
            string missing = Path.Combine(dir, "missing.png");
            JObject root = new() {
                ["selectedKeyType"] = "4key",
                ["keys"] = new JObject { ["4key"] = new JArray("Z", "X", "C", "V") },
                ["keyPositions"] = new JObject {
                    ["4key"] = new JArray(
                        new JObject {
                            ["dx"] = 0, ["dy"] = 0, ["width"] = 60, ["height"] = 60,
                            ["inactiveImage"] = png, ["activeImage"] = png,
                        },
                        new JObject {
                            ["dx"] = 70, ["dy"] = 0, ["width"] = 60, ["height"] = 60,
                            ["position"] = new JObject { ["inactiveImage"] = "https://example.com/a.png" },
                        },
                        new JObject {
                            ["dx"] = 140, ["dy"] = 0, ["width"] = 60, ["height"] = 60,
                            ["inactiveImage"] = missing,
                        },
                        new JObject {
                            ["dx"] = 210, ["dy"] = 0, ["width"] = 60, ["height"] = 60,
                            ["inactiveImage"] = KvDocument.DmLocalImagePrefix + "keep",
                        }),
                },
                ["embeddedLocalImages"] = new JArray(
                    new JObject { ["imageId"] = "keep", ["extension"] = "png", ["dataBase64"] = "AQIDBA==" },
                    new JObject { ["imageId"] = "orphan", ["extension"] = "png", ["dataBase64"] = "BQYHCA==" }),
            };
            List<string> notes = KvExportEmbedding.EmbedLocalImages(root);
            JArray keys = (JArray)root["keyPositions"]!["4key"]!;
            string idleRef = keys[0]!["inactiveImage"]!.ToString();
            Assert(idleRef.StartsWith(KvDocument.DmLocalImagePrefix, StringComparison.Ordinal),
                "a local file path is rewritten to an embedded reference");
            Assert(idleRef == keys[0]!["activeImage"]!.ToString(),
                "both fields reading the same file share one embedded image");
            Assert(keys[1]!["position"]!["inactiveImage"]!.ToString() == "https://example.com/a.png",
                "web references are left to travel as URLs");
            Assert(keys[2]!["inactiveImage"]!.ToString() == missing,
                "an unreadable path is left in place instead of dropped");
            Assert(KvDocument.Parse(root.ToString()).TryEmbeddedImage(idleRef, out string data, out string extension)
                && data == Convert.ToBase64String(pngBytes) && extension == "png",
                "the embedded payload round-trips through a parsed export");
            JArray images = (JArray)root["embeddedLocalImages"]!;
            Assert(images.Count == 2, "the export carries the new image plus the still-referenced one");
            bool keepSurvives = false, orphanSurvives = false;
            foreach(JToken image in images) {
                string id = image["imageId"]!.ToString();
                if(id == "keep") keepSurvives = true;
                if(id == "orphan") orphanSurvives = true;
            }
            Assert(keepSurvives && !orphanSurvives,
                "referenced embedded images survive while unreferenced ones are pruned");
            Assert(notes.Exists(n => n.Contains("not found")) && notes.Exists(n => n.Contains("Embedded 2")),
                "the export reports what it embedded and what it could not");
        } finally {
            Directory.Delete(dir, true);
        }
    }
    private static bool HasQuartzKey(JToken node) {
        switch(node) {
            case JObject obj:
                foreach(JProperty prop in obj.Properties()) {
                    if(prop.Name.StartsWith(KvExportShaping.QuartzPrefix, StringComparison.OrdinalIgnoreCase)) return true;
                    if(HasQuartzKey(prop.Value)) return true;
                }
                return false;
            case JArray arr:
                foreach(JToken item in arr)
                    if(HasQuartzKey(item)) return true;
                return false;
            default:
                return false;
        }
    }
}
