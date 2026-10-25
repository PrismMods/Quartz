using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static partial class KvDocumentTests {
    public static void TestDuplicateTabCopiesElementsIndependently() {
        KvDocument doc = KvDocument.Parse("""
            {"selectedKeyType":"custom-a",
             "customTabs":[{"id":"custom-a","name":"Main"},{"id":"custom-b","name":"Alt"}],
             "keys":{"custom-a":["Z"],"custom-b":["X"]},
             "keyPositions":{
               "custom-a":[{"dx":4,"dy":0,"width":60,"height":60,"count":5,"noteColor":"#F00","noteOpacity":80}],
               "custom-b":[{"dx":0,"dy":0,"width":60,"height":60,"count":0,"noteColor":"#FFF","noteOpacity":80}]}}
            """);
        string copy = doc.DuplicateTab("custom-a");
        Assert(copy != null && doc.HasTab(copy) && copy != "custom-a", "a fresh tab id comes back");
        Assert(doc.TabName(copy) != "Main" && doc.TabName(copy).StartsWith("Main"), "name de-duped: " + doc.TabName(copy));
        List<string> order = [.. doc.Tabs];
        Assert(order.IndexOf(copy) == order.IndexOf("custom-a") + 1, "the copy sits right after its source");
        KvElement src = doc.Elements("custom-a", KvElementKind.Key)[0];
        KvElement dup = doc.Elements(copy, KvElementKind.Key)[0];
        Assert(dup.GlobalKey == "Z" && dup.X == 4f, "the binding and position came across");
        dup.X = 99f;
        Assert(src.X == 4f, "editing the copy leaves the source alone");
        JObject json = JObject.Parse(doc.ToJson());
        Assert(((JObject)json["keys"]!).Count == 3, "three tabs serialize");
    }
    public static void TestPasteStyleKeepsPlaceAndBinding() {
        KvDocument doc = KvDocument.Parse("""
            {"selectedKeyType":"t","keys":{"t":["Z","X"]},
             "keyPositions":{"t":[
               {"dx":0,"dy":0,"width":60,"height":60,"count":3,"noteColor":"#F00","noteOpacity":80,"borderRadius":2,"fontSize":30},
               {"dx":100,"dy":5,"width":40,"height":40,"count":7,"noteColor":"#FFF","noteOpacity":50,"displayText":"X!"}]}}
            """);
        List<KvElement> keys = doc.Elements("t", KvElementKind.Key);
        keys[1].PasteStyle(keys[0].CopyStyle());
        KvElement b = keys[1];
        Assert(b.X == 100f && b.W == 40f && b.Count == 7 && b.DisplayText == "X!" && b.GlobalKey == "X",
            "place, size, count, label and binding stay");
        Assert(b.Raw["noteColor"]!.ToString() == "#F00" && (int)b.Raw["fontSize"]! == 30 && (int)b.Raw["borderRadius"]! == 2,
            "the look came across");
        JObject plain = JObject.Parse("""{"noteColor":"#FFF","noteOpacity":80}""");
        b.PasteStyle(plain);
        Assert(b.Raw["fontSize"] == null, "a field the source left unset is cleared back to default");
    }
}
