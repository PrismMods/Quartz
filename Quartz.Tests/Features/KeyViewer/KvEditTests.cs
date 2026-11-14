using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static class KvEditTests {
    private const string Tab = "t";
    private const string Layout = """
        {"selectedKeyType":"t",
         "customTabs":[{"id":"t","name":"Main"},{"id":"u","name":"Other"}],
         "keys":{"t":["Z","X","Z"],"u":["Z"]},
         "keyPositions":{
           "t":[{"dx":0,"dy":0,"width":60,"height":60,"count":1,"zIndex":0,"noteColor":"#FFF","noteOpacity":80},
                {"dx":70,"dy":0,"width":60,"height":60,"count":2,"zIndex":1,"noteColor":"#FFF","noteOpacity":80},
                {"dx":140,"dy":0,"width":60,"height":60,"count":3,"zIndex":2,"noteColor":"#FFF","noteOpacity":80}],
           "u":[{"dx":0,"dy":0,"width":60,"height":60,"count":9,"noteColor":"#FFF","noteOpacity":80}]},
         "statPositions":{
           "t":[{"statType":"kps","dx":0,"dy":100,"width":100,"height":30,"count":0,"zIndex":3,"noteColor":"#FFF","noteOpacity":80}]}}
        """;
    // The editor's epoch memoises per frame; each test moves the frame itself.
    private static int frame;
    private static KvEditEpoch NewEpoch() => new(() => frame);
    private static List<KvElement> Keys(KvDocument doc, string tab = Tab) => doc.Elements(tab, KvElementKind.Key);

    public static void TestEpochKeepsOwnChanges() {
        KvDocument doc = KvDocument.Parse(Layout);
        KvEditEpoch epoch = NewEpoch();
        object t0 = epoch.Token(doc);
        string before = doc.ToJson();
        frame++;
        KvEditOps.Duplicate(doc, Tab, [Keys(doc)[0]]);
        epoch.Own(doc);
        frame++;
        Assert(epoch.Token(doc) == t0, "a change the editor owned keeps its steps live");
        KvDocument next = KvDocument.Parse(before);
        epoch.Adopt(next);
        frame++;
        Assert(epoch.Token(next) == t0, "the editor's own restore keeps the token");
    }
    public static void TestEpochStaleBeforeOwn() {
        KvDocument doc = KvDocument.Parse(Layout);
        KvEditEpoch epoch = NewEpoch();
        object t0 = epoch.Token(doc);
        frame++;
        KvEditOps.Duplicate(doc, Tab, [Keys(doc)[0]]);
        Assert(epoch.Token(doc) != t0, "a structure change read before Own renews the token");
    }
    public static void TestEpochRenewsOnForeignChange() {
        KvDocument doc = KvDocument.Parse(Layout);
        KvEditEpoch epoch = NewEpoch();
        object t0 = epoch.Token(doc);
        doc.Add(Tab, Keys(doc)[0].Clone());
        Assert(epoch.Token(doc) == t0, "within one frame the hash is memoised");
        frame++;
        Assert(epoch.Token(doc) != t0, "a foreign add renews the token on the next frame");
        object t1 = epoch.Token(doc);
        KvDocument reparsed = KvDocument.Parse(doc.ToJson());
        Assert(epoch.Token(reparsed) != t1, "a fresh parse is a new document and a new token");
        object t2 = epoch.Token(reparsed);
        KvDocument other = KvDocument.Parse(Layout);
        epoch.Own(other);
        frame++;
        Assert(epoch.Token(other) != t2, "Own never adopts another document");
        object t3 = epoch.Token(other);
        Keys(other)[0].X = 5;
        frame++;
        Assert(epoch.Token(other) == t3, "a foreign field edit keeps the token");
    }
    public static void TestKeepCountsByOccurrence() {
        KvDocument live = KvDocument.Parse(Layout);
        KvDocument restored = KvDocument.Parse(Layout);
        List<KvElement> keys = Keys(restored);
        keys[0].GlobalKey = "X";
        keys[1].GlobalKey = "Z";
        // A third Z on this tab has no live partner here; it must keep its own count, not take
        // the other tab's live Z count.
        KvElement extra = keys[0].Clone();
        extra.GlobalKey = "Z";
        restored.Add(Tab, extra);
        foreach(KvElement k in keys) k.Count = 0;
        Keys(restored, "u")[0].Count = 4;
        restored.KeepCountsFrom(live);
        Assert(string.Join(",", keys.Select(k => k.Count)) == "2,1,3,0", "counts by occurrence: " + string.Join(",", keys.Select(k => k.Count)));
        Assert(Keys(restored, "u")[0].Count == 9, "the other tab takes its own tab's live count");
        Assert(extra.Count == 0, "an unpartnered key keeps its own count, not another tab's");
        // Two keys on one binding: undoing the delete of the first gives the restored key
        // the survivor's count, and the survivor its snapshot count.
        KvDocument afterDelete = KvDocument.Parse(Layout);
        KvEditOps.Delete(afterDelete, Tab, [Keys(afterDelete)[0]]);
        Keys(afterDelete)[1].Count = 30;
        KvDocument undone = KvDocument.Parse(Layout);
        undone.KeepCountsFrom(afterDelete);
        Assert(string.Join(",", Keys(undone).Select(k => k.Count)) == "30,2,3", "same-binding keys pair by order: " + string.Join(",", Keys(undone).Select(k => k.Count)));
    }
    public static void TestStructureHashTracksIdentity() {
        KvDocument doc = KvDocument.Parse(Layout);
        int h0 = doc.StructureHash();
        KvElement k = Keys(doc)[1];
        k.X = 300;
        k.Raw["noteColor"] = "#123456";
        Assert(doc.StructureHash() == h0, "field edits keep the hash");
        doc.Add(Tab, doc.Elements(Tab, KvElementKind.Stat)[0].Clone());
        int h1 = doc.StructureHash();
        Assert(h1 != h0, "adding a stat changes the hash");
        doc.Remove(Tab, k);
        doc.Add(Tab, k.Clone());
        Assert(doc.StructureHash() != h1, "a remove and re-add with equal counts changes the hash");
    }
    // The editor reads the hash every frame while it shows, so it must not allocate (foot flags included).
    public static void TestStructureHashAllocatesNothing() {
        KvDocument doc = KvDocument.Parse(Layout);
        doc.SetFootTab("u", true);
        Assert(doc.IsFootTab("u") && !doc.IsFootTab(Tab), "the fixture has a foot tab");
        long bytes = Alloc.BytesPerOp(() => Alloc.Consume(doc.StructureHash()));
        Assert(bytes == 0, "StructureHash allocates " + bytes + " B/op");
    }
    public static void TestKeysFindElementsAfterReplace() {
        KvDocument doc = KvDocument.Parse(Layout);
        KvElement e = Keys(doc)[1];
        string key = KvEditOps.Key(doc, Tab, e);
        KvElement found = KvEditOps.Find(KvDocument.Parse(doc.ToJson()), Tab, key);
        Assert(found != null && found.X == e.X && found.Y == e.Y && found.GlobalKey == e.GlobalKey, "the key finds the element in a reparsed document");
        Assert(KvEditOps.Find(doc, "gone", key) == null, "a missing tab finds nothing");
        Assert(!doc.HasTab("gone"), "looking up a missing tab does not create it");
    }
    public static void TestDuplicateAndPastePlacement() {
        KvDocument doc = KvDocument.Parse(Layout);
        List<KvElement> src = [Keys(doc)[0], Keys(doc)[1]];
        float top = doc.AllElements(Tab).Max(e => e.Z);
        List<KvElement> dups = KvEditOps.Duplicate(doc, Tab, src);
        Assert(dups.Count == 2, "two copies");
        for(int i = 0; i < 2; i++)
            Assert(dups[i].X == src[i].X + KvSnap.PasteOffset && dups[i].Y == src[i].Y + KvSnap.PasteOffset && dups[i].GlobalKey == src[i].GlobalKey, "copy " + i + " offset");
        float otherTop = doc.AllElements(Tab).Where(e => !dups.Contains(e)).Max(e => e.Z);
        Assert(dups.All(d => d.Z > otherTop), "copies sit above every other element");
        string clip = KvEditOps.Copy(src);
        List<KvElement> pasted = KvEditOps.Paste(doc, Tab, clip)!;
        Assert(pasted != null && pasted.Count == 2, "paste brings both back");
        Assert(pasted[1].X == src[1].X + KvSnap.PasteOffset && pasted[1].GlobalKey == src[1].GlobalKey && pasted[1].Kind == KvElementKind.Key, "pasted at the offset with its binding");
        float before = doc.AllElements(Tab).Where(e => !pasted.Contains(e)).Max(e => e.Z);
        Assert(pasted.All(p => p.Z > before) && top < before, "pasted above every other element");
        Assert(KvEditOps.Paste(doc, Tab, "not json") == null, "text that is not a copy pastes nothing");
        // Selected top first: the pasted pair still stacks as the originals do.
        List<KvElement> topFirst = [src[1], src[0]];
        List<KvElement> again = KvEditOps.Paste(doc, Tab, KvEditOps.Copy(topFirst))!;
        KvElement low = again.First(p => p.X == src[0].X + KvSnap.PasteOffset);
        KvElement high = again.First(p => p.X == src[1].X + KvSnap.PasteOffset);
        Assert(low.Z < high.Z, "copy keeps draw order, not selection order");
    }
    public static void TestSetOrderAndHidden() {
        KvDocument doc = KvDocument.Parse(Layout);
        List<KvElement> order = doc.AllElements(Tab);
        List<KvElement> reversed = [.. order];
        reversed.Reverse();
        KvEditOps.SetOrder(doc, Tab, reversed);
        Assert(doc.AllElements(Tab).SequenceEqual(reversed), "draw order reversed");
        KvEditOps.SetHidden([order[0], order[2]], true);
        Assert(order[0].Hidden && !order[1].Hidden && order[2].Hidden && !order[3].Hidden, "only the given items hide");
        KvEditOps.SetHidden([order[0]], false);
        Assert(!order[0].Hidden && order[2].Hidden, "and show again");
    }
}
