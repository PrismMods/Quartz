#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Addons;
using Quartz.Core;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using Quartz.UI.Search;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
using Fixture = Quartz.UI.FacetUi.FacetSelfTestFixture;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Addons page against PageAddons, and an addon settings tab drawn by AddonForm against AddonSettingsUI, on the fixture tab.
/// Nothing here adds, reloads, enables, disables or removes an addon: each of those buttons is checked through Do.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator Addons(Action<string, bool, string> check, Action<string> info) {
        const string key = CorePages.AddonsPageKey;
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            FacetBridge.OpenNewMenu(CorePages.HomePageKey);
            yield return Frames(3);
            var attached = new List<string>();
            Func<string, bool> was = QuartzShell.Doing;
            QuartzShell.Doing = n => { attached.Add(n); return true; };
            try {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                QuartzShell.Go(key);
                info($"[perf] page {key} first build: {clock.Elapsed.TotalMilliseconds:0.0} ms");
                yield return Frames(3);
            } finally {
                QuartzShell.Doing = was;
            }

            Covers(check, key);
            Searched(check, key, ["ADDONS_RELOAD"]);
            Translated(check, key, ["ADDONS_HINT"]);

            IReadOnlyList<AddonService.Handle> handles = AddonService.Addons;
            VisualElement root = PageContent();
            string[] toggles = [.. handles.Select(h => GenerateUI.LocaleKeyFromId("addon_" + h.UnitId))];
            List<VisualElement> order = root.Query<VisualElement>().ToList();
            int[] at = [.. toggles.Select(t => order.FindIndex(e => e.name == t))];
            VisualElement empty = root.Q(name: "ADDONS_EMPTY");
            bool emptyShown = empty != null && empty.resolvedStyle.display != DisplayStyle.None;
            bool rows = handles.Count == 0 ? emptyShown : !emptyShown && at.All(i => i >= 0) && at.SequenceEqual(at.OrderBy(i => i))
                && Enumerable.Range(1, handles.Count).All(n => Names(root, "addons-status#" + n));
            check($"{key}: one row per loaded addon in AddonService order, with its status", rows,
                $"addons={handles.Count} empty={emptyShown} at={string.Join(",", at)}");

            yield return Recorded(check, $"{key} ADDONS_ADD: a click acts only through Do", () => PressRow("ADDONS_ADD"), "addons.add");
            yield return Recorded(check, $"{key} ADDONS_RELOAD: a click acts only through Do", () => PressRow("ADDONS_RELOAD"), "addons.reload");
            yield return Recorded(check, $"{key} ADDONS_OPEN_FOLDER: a click acts only through Do", () => PressRow("ADDONS_OPEN_FOLDER"), "addons.folder");
            if(handles.Count > 0) {
                for(int i = 0; i < handles.Count; i++) {
                    string toggle = toggles[i];
                    yield return Recorded(check, $"{key} ADDON_<UNIT>#{i + 1}: the toggle acts only through Do", () => ClickRow(toggle),
                        "addons.enable:" + handles[i].UnitId + ":" + !handles[i].Enabled);
                }
                AddonService.Handle h = handles[0];
                var first = new List<string>();
                var second = new List<string>();
                yield return Recording(first, () => PressRow("ADDONS_REMOVE#1"));
                string armed = PageContent().Q<Button>("ADDONS_REMOVE#1-input")?.text;
                yield return Recording(second, () => PressRow("ADDONS_REMOVE#1"));
                check($"{key} ADDONS_REMOVE#n: the first click arms, only the second reaches Remove",
                    first.Count == 0 && armed == QuartzHost.Host.Translate("ADDONS_REMOVE_CONFIRM") && second.SequenceEqual(["addons.remove:" + h.UnitId]),
                    $"first={string.Join(",", first)} armed='{armed}' second={string.Join(",", second)}");
            } else info($"{key}: no addon is installed, so the toggle and Remove have no row to check");

            string[] want = [.. handles.Where(h => h.Loaded).Select(h => "addons.updatecheck:" + h.UnitId)];
            if(want.Length > 0)
                check($"{key}: the update check runs only while the page is attached",
                    attached.Where(n => n.StartsWith("addons.updatecheck:", StringComparison.Ordinal)).SequenceEqual(want),
                    $"attached={string.Join(",", attached)} want={string.Join(",", want)}");
            else info($"{key}: no addon is loaded, so the update check has nothing to ask (the detached builds ask nothing)");

            yield return AddonFixture(check);
        } finally {
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(2);
    }

    // The fixture tab's rows, each by its classic key.
    private static string FixtureKey(string member) => GenerateUI.LocaleKeyFromId("addon_" + Fixture.Owner + "_" + member);

    private static IEnumerator AddonFixture(Action<string, bool, string> check) {
        const string tag = "addon fixture";
        global::Facet.History.Clear();
        Fixture.RegisterSettingsTab();
        try {
            yield return Fixture.Register();   // a custom addon page beside it, for the pages check
            QuartzShell.Go(Fixture.TabKey);
            yield return Frames(3);
            VisualElement root = PageContent();
            Fixture.TabSettings data = Fixture.Data;

            {
                const BindingFlags any = BindingFlags.Static | BindingFlags.NonPublic;
                MethodInfo split = typeof(AddonSettingsUI).GetMethod("SplitPascal", any);
                var members = (IEnumerable<MemberInfo>)typeof(AddonSettingsUI).GetMethod("Members", any).Invoke(null, [typeof(Fixture.TabSettings)]);
                var wrong = new List<string>();
                int seen = 0;
                foreach(MemberInfo m in members) {
                    seen++;
                    string want = m.GetCustomAttribute<NameAttribute>()?.Label ?? (string)split.Invoke(null, [m.Name]);
                    VisualElement row = root.Q(name: FixtureKey(m.Name));
                    // A colour row's first text is its fold arrow: the label is any of the row's texts.
                    List<Label> texts = row as Label is { } l ? [l] : row?.Query<Label>().ToList() ?? [];
                    if(!texts.Any(t => t.text == want)) wrong.Add($"{m.Name}='{string.Join("|", texts.Select(t => t.text))}'/'{want}'");
                }
                foreach(Fixture.TabMode v in Enum.GetValues(typeof(Fixture.TabMode)))
                    if(AddonFieldRules.Label(v.ToString(), null) != (string)split.Invoke(null, [v.ToString()])) wrong.Add("option " + v);
                string mode = string.Concat(root.Q(name: FixtureKey("Mode") + "-input")?.Query<TextElement>().ToList().Select(t => t.text) ?? []);
                if(!mode.Contains("Fast Mode")) wrong.Add($"mode shows '{mode}'");
                check($"{tag}: every row's Id is classic's key and its label classic's", seen == 12 && wrong.Count == 0,
                    $"members={seen} wrong={string.Join(",", wrong)}");
            }

            {
                global::Facet.History.Clear();
                Control<float> volume = RowOf<float>(FixtureKey("Volume"));
                int s0 = Fixture.Saves;
                float was = data.Volume;
                Drag(volume, 0.2f, 0.3f);
                yield return Frames(2);
                int dragged = Fixture.Saves;
                float now = data.Volume;
                bool undid = global::Facet.History.Undo();
                yield return Frames(2);
                check($"{tag}: a slider drag saves once and an undo saves once more",
                    volume != null && dragged == s0 + 1 && Mathf.Abs(now - 0.3f) < 0.001f && undid && Fixture.Saves == s0 + 2 && data.Volume == was,
                    $"row={volume != null} saves={s0}->{dragged}->{Fixture.Saves} value={now} undo={undid} back={data.Volume}");
            }

            {
                Control<Color> tint = RowOf<Color>(FixtureKey("Tint"));
                int s0 = Fixture.Saves;
                Invoke(tint, "Edit", Color.blue);
                Invoke(tint, "Edit", Color.green);
                yield return Frames(2);
                int during = Fixture.Saves;
                Invoke(tint, "Commit");
                yield return Frames(2);
                check($"{tag}: a colour drag saves once, on release", tint != null && during == s0 && Fixture.Saves == s0 + 1 && data.Tint == Color.green,
                    $"row={tint != null} saves={s0}->{during}->{Fixture.Saves} tint={data.Tint}");
            }

            {
                var names = new List<string>();
                int s0 = Fixture.Saves;
                bool clicked = false;
                yield return Recording(names, () => clicked = ClickRow(FixtureKey("Heavy")));
                check($"{tag}: the ReloadRequired toggle saves once and asks for one reload",
                    clicked && data.Heavy && Fixture.Saves == s0 + 1 && names.SequenceEqual(["addons.reload"]),
                    $"clicked={clicked} on={data.Heavy} saves={s0}->{Fixture.Saves} recorded={string.Join(",", names)}");
            }

            {
                string id = FixtureKey("Limit") + "-input";
                TextField field = root.Q<TextField>(id);
                int s0 = Fixture.Saves;
                TypeInto(id, "7");
                yield return Frames(2);
                int? seven = data.Limit;
                TypeInto(id, "abc");
                yield return Frames(2);
                string reverted = field?.value;
                int afterBad = Fixture.Saves;
                int? kept = data.Limit;
                TypeInto(id, "");
                yield return Frames(2);
                check($"{tag}: an empty nullable number writes null, bad text reverts",
                    field != null && seven == 7 && kept == 7 && reverted == "7" && afterBad == s0 + 1 && data.Limit == null && Fixture.Saves == s0 + 2,
                    $"field={field != null} seven={seven} kept={kept} shown='{reverted}' saves={s0}->{afterBad}->{Fixture.Saves} now={data.Limit}");
            }

            {
                VisualElement head = root.Q(name: FixtureKey("Title") + "_SECTION");
                VisualElement body = head?.parent is { } box && box.IndexOf(head) + 1 < box.childCount ? box[box.IndexOf(head) + 1] : null;
                VisualElement title = root.Q(name: FixtureKey("Title")), enabled = root.Q(name: FixtureKey("Enabled"));
                bool look = head?.Query<Label>().ToList().Any(l => l.text == "Look") == true;
                check($"{tag}: Ignore hides a member and Section groups the next ones",
                    !Names(root, FixtureKey("Hidden")) && look && body != null && title != null && body.Contains(title)
                    && body.Contains(root.Q(name: FixtureKey("Map"))) && enabled != null && !body.Contains(enabled),
                    $"hidden={Names(root, FixtureKey("Hidden"))} head={head != null} look={look} body={body != null}");
            }

            {
                string map = FixtureKey("Map");
                int s0 = Fixture.Saves;
                bool added = PressRow(map + "_ADD");
                yield return Frames(2);
                bool two = data.Map.Count == 2 && data.Map.ContainsKey("");
                int afterAdd = Fixture.Saves;
                bool typed = TypeInto(map + "_KEY#2-input", "b");
                yield return Frames(2);
                bool edited = data.Map.TryGetValue("b", out string bv) && bv == "" && data.Map["a"] == "1";
                int afterEdit = Fixture.Saves;
                bool removed = PressRow(map + "_REMOVE#2");
                yield return Frames(2);
                check($"{tag}: a string map row adds, edits and removes, saving each",
                    added && two && afterAdd == s0 + 1 && typed && edited && afterEdit == s0 + 2 && removed
                    && data.Map.Count == 1 && data.Map["a"] == "1" && Fixture.Saves == s0 + 3,
                    $"add={added}/{two} edit={typed}/{edited} remove={removed} map={string.Join(",", data.Map.Select(kv => kv.Key + "=" + kv.Value))} saves={s0}->{Fixture.Saves}");
            }

            {
                // The addon replaces its map in code while the tab stays built: the next edit starts from the new map.
                string map = FixtureKey("Map");
                data.Map = new() { ["x"] = "9" };
                yield return Frames(2);
                bool typed = TypeInto(map + "_KEY#1-input", "y");
                yield return Frames(2);
                check($"{tag}: a map replaced in code is what the next edit changes",
                    typed && data.Map.Count == 1 && data.Map.TryGetValue("y", out string yv) && yv == "9",
                    $"typed={typed} map={string.Join(",", data.Map.Select(kv => kv.Key + "=" + kv.Value))}");
            }

            {
                var names = new List<string>();
                int s0 = Fixture.Saves;
                Action<VisualElement> build = QuartzShell.BuilderFor(NavRegistry.ByKey(Fixture.TabKey));
                Func<string, bool> was = QuartzShell.Doing;
                QuartzShell.Doing = n => { names.Add(n); return true; };
                try {
                    for(int i = 0; i < 2; i++) build?.Invoke(new VisualElement());
                } finally {
                    QuartzShell.Doing = was;
                }
                check($"{tag}: the tab builds detached without a save or a side effect", build != null && names.Count == 0 && Fixture.Saves == s0,
                    $"builder={build != null} recorded={string.Join(",", names)} saves={s0}->{Fixture.Saves}");
            }

            {
                List<NavPage> pages = [.. NavRegistry.AllVisible().Where(pg => pg.Key.StartsWith("addon.", StringComparison.Ordinal))];
                // Each record names a page its addon still owns: a record left by an unload or a throwing RegisterTab is stale.
                string[] stale = [.. AddonSettingsTabs.Keys.Where(k => !AddonSettingsTabs.TryGet(k, out AddonSettingsTabs.Tab t)
                    || NavRegistry.ByKey(k)?.OwnerId != t.AddonId)];
                // A page with a record opens natively from search; one without opens the classic card.
                string[] wrong = [.. pages.Where(pg => AddonSettingsTabs.TryGet(pg.Key, out _) == (QuartzShell.ClassicHit(pg) != null)).Select(pg => pg.Key)];
                bool both = pages.Any(pg => pg.Key == Fixture.TabKey) && pages.Any(pg => pg.Key == Fixture.PageKey);
                QuartzShell.Go(Fixture.PageKey);
                yield return Frames(3);
                bool custom = Body.Q(name: "menu-classic") != null;
                QuartzShell.Go(Fixture.TabKey);
                yield return Frames(3);
                bool native = Body.Q(name: "menu-classic") == null && Names(PageContent(), FixtureKey("Enabled"));
                check("addon pages: every loaded addon's settings tab draws natively and every custom tab keeps the classic card",
                    both && stale.Length == 0 && wrong.Length == 0 && custom && native,
                    $"pages={string.Join(",", pages.Select(pg => pg.Key))} stale={string.Join(",", stale)} wrong={string.Join(",", wrong)} custom={custom} native={native}");
            }

            {
                global::Facet.History.Clear();
                bool on = data.Enabled;
                bool clicked = ClickRow(FixtureKey("Enabled"));
                yield return Frames(2);
                int s0 = Fixture.Saves;
                AddonSettingsTabs.RemoveAddon(Fixture.Owner);
                bool forgotten = !AddonSettingsTabs.TryGet(Fixture.TabKey, out _);
                global::Facet.History.Undo();
                yield return Frames(2);
                check($"{tag}: an undo after the tab is forgotten saves nothing", clicked && data.Enabled == !on && Fixture.Saves == s0,
                    $"clicked={clicked} value={on}->{data.Enabled} saves={s0}->{Fixture.Saves}");
                check($"{tag}: unregistering the addon forgets its tab", forgotten, null);
                AddonSettingsTabs.Register(Fixture.Tab);
            }

            {
                bool threw = false;
                try {
                    Fixture.Context.RegisterTab("Settings", _ => { });
                } catch(InvalidOperationException e) {
                    threw = true;   // the settings tab's page already holds the key; its record is dropped first
                    Diag.Ignore(e);
                }
                bool dropped = !AddonSettingsTabs.TryGet(Fixture.TabKey, out _);
                QuartzShell.Go(CorePages.HomePageKey);
                yield return Frames(2);
                QuartzShell.Go(Fixture.TabKey);
                yield return Frames(3);
                bool card = Body.Q(name: "menu-classic") != null;
                check($"{tag}: a custom RegisterTab with the settings tab's title drops its record", dropped && card, $"threw={threw} dropped={dropped} card={card}");
            }
        } finally {
            global::Facet.History.Clear();
            Fixture.UnregisterSettingsTab();
            Fixture.Forget();
            // The registry changed: re-read the classic rows now, so the next index build is not charged for it.
            SettingsSearch.Rows();
        }
        yield return Frames(2);
    }

    // The value row named id on the open page, from its surface's refresher, which holds every row's Refresh.
    private static Control<T> RowOf<T>(string id) {
        VisualElement any = PageContent().Q(name: id);
        if(any == null) return null;
        Type refresher = typeof(Controls).Assembly.GetType("Facet.Refresher");
        object of = refresher?.GetMethod("Of", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, [any]);
        var rows = refresher?.GetField("_controls", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(of) as IEnumerable<KeyValuePair<string, Action>>;
        return rows?.Select(r => r.Value?.Target).OfType<Control<T>>().FirstOrDefault(c => c.Root == any);
    }

    // A drag as a player makes it: one Edit per frame, then the release's Commit.
    private static void Drag<T>(Control<T> row, params T[] frames) {
        foreach(T v in frames) Invoke(row, "Edit", v);
        Invoke(row, "Commit");
    }

    private static void Invoke<T>(Control<T> row, string method, params object[] args) {
        if(row != null) typeof(Control<T>).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(row, args);
    }
}
#endif
