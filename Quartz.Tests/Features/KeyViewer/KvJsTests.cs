using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Js;
using static Asserts;

static class KvJsTests {
    public static void TestPluginIds() {
        KvJsPluginRecord declared = new() {
            Name = "Ignored Name.js",
            Content = "// a heading\n// @id Quartz-Counter_2\ndmn.plugin.defineElement({});",
        };
        Assert(declared.PluginId == "quartz-counter_2", "@id is read case-insensitively");

        string lateHeader = string.Join("\n", Enumerable.Repeat("// filler", 20)) + "\n// @id too-late";
        KvJsPluginRecord fallback = new() { Name = "My Fancy.Plugin.mjs", Content = lateHeader };
        Assert(fallback.PluginId == "my-fancy-plugin", "an @id after line 20 is ignored and the filename is normalized");
        Assert(KvJsPluginRecord.NormalizeId("!!.js") == "plugin", "an empty normalized id gets a stable fallback");
        TestKeyEventQueue();
    }

    private static void TestKeyEventQueue() {
        KvJsKeyEventQueue queue = new();
        for(int i = 0; i < KvJsKeyEventQueue.Capacity; i++)
            Assert(queue.TryEnqueue(i, (i & 1) == 0), "JS key queue accepts events up to its fixed capacity");
        Assert(!queue.TryEnqueue(-1, false) && queue.TakeOverflow(), "JS key queue reports bounded overflow");
        for(int i = 0; i < KvJsKeyEventQueue.Capacity; i++) {
            Assert(queue.TryDequeue(out KvJsKeyEventQueue.Event ev), "queued JS key event is available");
            Assert(ev.Key == i && ev.Down == ((i & 1) == 0), "JS key queue preserves key-event order and state");
        }
        Assert(!queue.TryDequeue(out _), "draining JS key queue consumes every event once");
        Assert(queue.TryEnqueue(99, true)
            && queue.TryDequeue(out KvJsKeyEventQueue.Event reused)
            && reused.Key == 99 && reused.Down, "JS key queue reuses drained ring slots");
    }

    public static void TestPluginRecordRoundTrip() {
        KvJsPluginRecord original = new() {
            Name = "counter.js",
            Path = "/plugins/counter.js",
            Content = "// @id counter\nconst value = 7;",
            Enabled = false,
        };
        JObject json = original.Serialize();
        KvJsPluginRecord restored = KvJsPluginRecord.Deserialize(json);
        Assert(restored.Name == original.Name, "plugin name survives serialization");
        Assert(restored.Path == original.Path, "plugin source path survives serialization");
        Assert(restored.Content == original.Content, "imported source survives serialization");
        Assert(!restored.Enabled, "per-plugin enable state survives serialization");
    }

    public static void TestHtmlTemplates() {
        KvJsVNode badge = KvJsVNode.NewElement("strong");
        badge.Children.Add(KvJsVNode.NewText("DOWN"));
        KvJsVNode root = KvJsTemplate.Get([
            "<div class=\"panel ", "\"><span>", "</span>", "</div>",
        ]).Instantiate([
            "active",
            "A",
            new object[] { badge, null, "!" },
        ]);

        Assert(root.Tag == "div", "single root element is returned directly");
        Assert(root.Attr("class") == "panel active", "attribute interpolation is preserved");
        Assert(root.Children.Count == 3, "text, nested vnode, and array text are flattened in order");
        Assert(root.Children[0].Tag == "span" && root.Children[0].Children[0].Text == "A", "nested element text is interpolated");
        Assert(root.Children[1] == badge, "nested template nodes stay as nodes");
        Assert(root.Children[2].Text == "!", "iterable template values are flattened");
    }
}
