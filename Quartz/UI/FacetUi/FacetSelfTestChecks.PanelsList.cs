#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using TMPro;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.UI.Nav;
using Facet;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Panels' list in the new menu against the classic page: its rows, the panels in order, and a panel this section
/// makes, edits by reference and deletes: its sliders, name, background, stats picked, reordered and removed, its own move to
/// the top and back, and an undo after its removal. Before each step the panel must still be where it was made; the player's
/// panels are never touched. OverlayPanels.json and the toggle hotkeys end as the run found them.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator OverlayPanelsList(Action<string, bool, string> check, Action<string> info) {
        const string page = "overlay.panels";
        PanelsOverlay.EnsureConf();
        List<PanelConfig> list = PanelsOverlay.Conf.Panels;
        string file = PanelsOverlay.ConfMgr.Path;
        byte[] start = Bytes(file);   // the player's, put back after whatever the steps save
        (Dictionary<string, (int Modifier, int Key)> binds, _) = HoldBinds();   // a move rekeys the panels' hotkeys
        var made = new PanelConfig[1];
        try {
            yield return FormPage("panels", page, () => { }, () => Drop(made[0]), () => PanelsListChecks(check, info, page, file, made));
        } finally {
            Drop(made[0]);
            PutBackBinds(binds);
            PutBackPanelsFile(file, start);
        }
        // A debounced save already handed to a worker can land after the finally: settle, then put it back again. Settings.json
        // also holds the windows' geometry, which the page visits write: the hotkeys are compared, not its bytes.
        float until = Time.realtimeSinceStartup + 1f;
        while(Time.realtimeSinceStartup < until) yield return null;
        PutBackPanelsFile(file, start);
        Dictionary<string, (int Modifier, int Key)> now = MainCore.Conf.ToggleKeybinds;
        bool same = now.Count == binds.Count && binds.All(kv => now.TryGetValue(kv.Key, out var b) && b == kv.Value);
        check($"{page}: the list's steps leave OverlayPanels.json and the toggle hotkeys as the run found them",
            SameBytes(Bytes(file), start) && same, $"panels file same={SameBytes(Bytes(file), start)} hotkeys same={same}");
    }

    // The panel the section made goes, by reference, if it is still there.
    private static void Drop(PanelConfig made) {
        if(made == null || !PanelsOverlay.Conf.Panels.Remove(made)) return;
        PanelsOverlay.Save();
        PanelsOverlay.Rebuild();
        UICore.ClassicStale = true;
    }

    private static IEnumerator PanelsListChecks(Action<string, bool, string> check, Action<string> info, string page, string file, PanelConfig[] made) {
        List<PanelConfig> list = PanelsOverlay.Conf.Panels;
        Covers(check, page);
        Searched(check, page, ["PANELS_CREATE"]);
        PanelRowsSearched(check, page);
        Translated(check, page, ["HEADING_APPEARANCE", "PANEL_SHOWSTAT", "PANEL_SHOWLABEL"]);
        List<string> shown = [.. PageContent().Query<Label>(name: "PANELS_LIST-text").ToList().Select(l => l.text)];
        check($"{page}: the list shows every panel in order", shown.SequenceEqual(list.Select(p => p.Name)),
            $"shown={string.Join("|", shown)} panels={string.Join("|", list.Select(p => p.Name))}");
        check($"{page}: no page is partly moved",
            typeof(QuartzShell).GetField("PartlyMoved", BindingFlags.Static | BindingFlags.NonPublic) == null
            && !QuartzShell.ClassicRows().Any(e => e.Where == QuartzShell.Label(NavRegistry.ByKey(page).LocaleKey, NavRegistry.ByKey(page).Title)),
            $"field={typeof(QuartzShell).GetField("PartlyMoved", BindingFlags.Static | BindingFlags.NonPublic) != null}");

        List<PanelConfig> players = [.. list];
        if(PageContent().Q<Button>("PANELS_CREATE-input") is { } create) Submit(create);
        yield return Frames(3);
        int n = list.Count;
        PanelConfig created = made[0] = n == players.Count + 1 ? list[n - 1] : null;
        // Each step acts on #n only while the panel made here is still there: else the section stops.
        bool Here(string step) {
            if(created != null && n - 1 < list.Count && ReferenceEquals(list[n - 1], created)) return true;
            check($"{page}: the panel made here is at its place before each step", false, $"step={step} created={created != null} panels={list.Count}/{n}");
            return false;
        }
        if(!Here("create")) yield break;
        created!.Name = "Self-test " + Guid.NewGuid().ToString("N")[..8];   // no panel of the player's can be taken for it
        PanelsOverlay.ConfMgr.Save();
        QuartzShell.Surface?.Refresh();
        yield return Frames(2);
        VisualElement Q(string id) => PageContent().Q(name: id + "#" + n + "-input") ?? PageContent().Q(name: id + "#" + n);

        // Font size: written and applied every frame of the drag, saved once on release. Decimals: written, never applied.
        yield return SliderDrag(check, info, $"{page} PANEL_FONTSIZE#n: a drag applies per frame and saves on release", file, Q("PANEL_FONTSIZE") as Slider,
            12f, 48f, created.FontSize == 30f ? 36f : 30f, v => created.FontSize == v, created, true);
        if(!Here("decimals")) yield break;
        yield return SliderDrag(check, info, $"{page} PANEL_DECIMALS#n: a drag writes the value per frame, applies nothing and saves on release", file,
            Q("PANEL_DECIMALS") as Slider, 0f, 6f, created.Decimals == 4 ? 5f : 4f, v => created.Decimals == (int)v, created, false);
        if(!Here("background")) yield break;
        bool background = created.BackgroundEnabled;
        yield return Saved(check, page, "PANEL_BACKGROUND#n", () => ClickRow("PANEL_BACKGROUND#" + n), () => created.BackgroundEnabled != background, file);

        // A drag that starts on a slider inside the item is the slider's: the list does not reorder.
        if(!Here("slider drag")) yield break;
        List<PanelConfig> order = [.. list];
        bool dragged = Q("PANEL_FONTSIZE") is Slider font && SlideOnList(font, 12f, 48f, 20f);
        yield return Frames(2);
        VisualElement drop = PageContent().Q(name: "PANELS_LIST-drop");
        check($"{page}: dragging PANEL_FONTSIZE#n moves the slider, not the panel",
            dragged && list.SequenceEqual(order) && created.FontSize == 20f && (drop == null || drop.resolvedStyle.display == DisplayStyle.None),
            $"dragged={dragged} order same={list.SequenceEqual(order)} size={created.FontSize} drop={drop?.resolvedStyle.display}");

        // Two stats picked: any first, then an image, so the image rows are there.
        if(!Here("add stat")) yield break;
        byte[] before = Bytes(file);
        string first = null;
        for(int i = 0; i < 2; i++) {
            if(Q("PANEL_ADDSTAT") is Button add) Submit(add);
            yield return Frames(2);
            Button pick = i == 1 ? PageContent().Q<Button>("PANEL_PICK_IMAGE#" + n + "-input")
                : PageContent().Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("PANEL_PICK_") && b.name.EndsWith("#" + n + "-input"));
            if(i == 0) first = pick?.name;
            if(pick != null) Submit(pick);
            yield return Frames(2);
        }
        bool picked = created.Stats.Count == 2 && created.Stats[1].Id == "image";
        yield return SavedSince(file, before);
        check($"{page} PANEL_ADDSTAT#n: picking a stat adds it and saves, twice", picked && !SameBytes(Bytes(file), before),
            $"stats={string.Join(",", created.Stats.Select(s => s.Id))} first={first} saved={!SameBytes(Bytes(file), before)}");
        if(!picked) yield break;

        // The image stat's pick and folder only ask Do.
        int g = list.Take(n - 1).Sum(p => p.Stats.Count) + 2;   // the image stat's number on the page
        yield return Recorded(check, $"{page}: an image stat's Choose Image asks only Do", () => Submit(PageContent().Q<Button>($"PANEL_IMAGE_CHOOSE#{g}-input")),
            "panels.imagepick:" + n);
        yield return Recorded(check, $"{page}: an image stat's Open Images Folder asks only Do", () => Submit(PageContent().Q<Button>($"PANEL_IMAGE_FOLDER#{g}-input")),
            "panels.imagefolder");

        if(!Here("stat move")) yield break;
        StatEntry s1 = created.Stats[0], s2 = created.Stats[1];
        order = [.. list];
        before = Bytes(file);
        OverlayPages.PanelsStats.Move(created, 1, 0);
        yield return SavedSince(file, before);
        check($"{page}: dragging stat 2 above stat 1 reorders panel.Stats only, Conf.Panels unchanged",
            created.Stats.Count == 2 && created.Stats[0] == s2 && created.Stats[1] == s1 && list.SequenceEqual(order) && !SameBytes(Bytes(file), before),
            $"stats={string.Join(",", created.Stats.Select(s => s.Id))} panels same={list.SequenceEqual(order)} saved={!SameBytes(Bytes(file), before)}");

        if(!Here("stat X")) yield break;
        yield return Frames(2);   // the moved stats' rows are built again, renumbered
        g = list.Take(n - 1).Sum(p => p.Stats.Count) + 1;
        Button x = PageContent().Q<Button>($"DELETE_SHORT#{g}-input");
        bool hit = x != null;
        if(hit) Submit(x);
        yield return Frames(2);
        check($"{page}: a stat's X removes it", hit && created.Stats.Count == 1 && created.Stats[0] == s1,
            $"button={hit} stats={string.Join(",", created.Stats.Select(s => s.Id))}");

        // The made panel's section opened, so its stored fold state can be followed through the moves and the delete.
        if(!Here("panel move")) yield break;
        if(PageContent().Q(name: "PANEL#" + n) is { } head && Fold(n) != "1") Click(head);
        yield return Frames(2);
        string Fold(int at) => MainCore.Conf.FacetWindows.FirstOrDefault(kv => kv.Key.StartsWith("fold.", StringComparison.Ordinal)
            && kv.Key.EndsWith("/PANEL#" + at, StringComparison.Ordinal)).Value;
        // The player's first panel shut for the move, so the made panel's open state at place 1 can only have followed it.
        string firstKey = n > 1 ? FoldKey(1) : null, firstWas = firstKey == null ? null : Fold(1);
        if(firstWas == "1") QuartzHost.Write(firstKey, "0");
        string FoldKey(int at) => MainCore.Conf.FacetWindows.Keys.FirstOrDefault(k => k.StartsWith("fold.", StringComparison.Ordinal)
            && k.EndsWith("/PANEL#" + at, StringComparison.Ordinal));
        string opened = Fold(n), firstFold = Fold(1) ?? "0";

        // The made panel to the top and back: the list, the overlay's drawing order and the file follow.
        before = Bytes(file);
        OverlayPages.PanelsList.Move(n - 1, 0);
        string foldTop = Fold(1), foldNext = n > 1 ? Fold(2) ?? "0" : firstFold;
        bool built = PanelsOverlay.Built;
        bool top = list[0] == created && list.Skip(1).SequenceEqual(players), overlay = !built || OverlayFirst() == created;
        if(!built) info($"{page}: the panels overlay is not built, so the drawing order after a move is not checked");
        yield return SavedSince(file, before);
        bool saved = !SameBytes(Bytes(file), before);
        OverlayPages.PanelsList.Move(0, n - 1);
        yield return Frames(2);
        check($"{page}: dragging the new panel to the top moves it first in Conf.Panels and the overlay, saved, and back",
            top && overlay && saved && list.Take(n - 1).SequenceEqual(players),
            $"top={top} overlay={overlay} built={built} saved={saved} back={list.Take(n - 1).SequenceEqual(players)}");
        check($"{page}: a panel's fold state follows the panel through a move and back",
            opened == "1" && foldTop == "1" && foldNext == firstFold && Fold(n) == "1" && (Fold(1) ?? "0") == firstFold,
            $"opened={opened} top={foldTop} next={foldNext} (was {firstFold}) back={Fold(n)} first={Fold(1)}");
        if(firstWas == "1") QuartzHost.Write(firstKey, "1");

        // A name typed and committed: the section's title follows. That edit's step is the one undone after the removal: bound
        // to the panel, it goes stale with it, so neither the removed panel's name nor any other panel changes.
        if(!Here("name")) yield break;
        string name = "Self-test " + Guid.NewGuid().ToString("N")[..8];
        global::Facet.History.Clear();
        yield return Saved(check, page, "PANEL_NAME#n", () => TypeInto("PANEL_NAME#" + n + "-input", name),
            () => created.Name == name && PageContent().Q(name: "PANEL#" + n) is { } head && head.Children().OfType<Label>().Any(l => l.text == name), file);

        if(!Here("delete")) yield break;
        if(Q("PANEL_DELETE") is Button delete) Submit(delete);
        yield return Frames(2);
        bool gone = !list.Contains(created) && list.SequenceEqual(players);
        check($"{page} PANEL_DELETE#n: deletes the panel the test created, no confirm, as classic", gone,
            $"panels={list.Count}/{players.Count} created left={list.Contains(created)}");
        if(!gone) yield break;
        check($"{page}: a deleted panel's fold state stays behind with it, not on the next panel at its place", (Fold(n) ?? "0") == "0",
            $"fold={Fold(n)}");

        PanelsOverlay.ConfMgr.Save();
        string[] others = [.. list.Select(p => p.Serialize().ToString())];
        before = Bytes(file);
        global::Facet.History.Undo();
        yield return Frames(2);
        float until = Time.realtimeSinceStartup + 0.8f;   // past the save debounce
        while(Time.realtimeSinceStartup < until) yield return null;
        bool same = list.Select(p => p.Serialize().ToString()).SequenceEqual(others) && !list.Contains(created);
        check($"{page}: undo after the removal never writes into another panel", same && SameBytes(Bytes(file), before) && created.Name == name,
            $"panels same={same} file same={SameBytes(Bytes(file), before)} name={created.Name == name}");
    }

    // The detached build lists no panel: each panel row's English text still finds this page, and no hit names a panel.
    private static void PanelRowsSearched(Action<string, bool, string> check, string page) {
        NavPage nav = NavRegistry.ByKey(page);
        string title = QuartzShell.Label(nav.LocaleKey, nav.Title);
        var missed = new List<string>();
        var named = new List<string>();
        List<SearchIndex.Hit> All(string q) {   // past the 40 the box shows: a common label is on many pages
            QuartzShell.Nav.Filter(q);
            return QuartzShell.Nav.Index?.Find(q, 1000) ?? [];
        }
        try {
            foreach(string key in OverlayPages.PanelsList.RowKeys)
                if(!All(MainCore.Tr.GetForLanguage(key, "en-US", key)).Any(h => h.Key == key && h.Page == title)) missed.Add(key);
            foreach(PanelConfig p in PanelsOverlay.Conf.Panels)
                if(p.Name.Length >= 2 && All(p.Name).Any(h => h.Page == title && (h.Label == p.Name || h.Key == p.Name))) named.Add(p.Name);
        } finally {
            QuartzShell.Nav?.Filter("");
        }
        check($"{page}: search finds each panel row on this page, by its Lang key only", missed.Count == 0 && named.Count == 0,
            $"missed={string.Join(",", missed)} panel names found={named.Count}");
    }

    // The first panel the overlay draws, or null when the overlay is not built.
    private static PanelConfig OverlayFirst() {
        if(typeof(PanelsOverlay).GetField("panels", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is not System.Collections.IList live || live.Count == 0)
            return null;
        object first = live[0];
        return first?.GetType().GetField("Config", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(first) as PanelConfig;
    }

    // Up to 2 s of real time for a debounced save to land.
    private static IEnumerator SavedSince(string file, byte[] before) {
        for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 2f && SameBytes(Bytes(file), before);) yield return null;
    }

    // The text the overlay draws panel with, or null when the overlay is not built.
    private static TMP_Text LiveText(PanelConfig panel) {
        if(typeof(PanelsOverlay).GetField("panels", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is not System.Collections.IList live) return null;
        foreach(object p in live)
            if(p?.GetType().GetField("Config")?.GetValue(p) == panel) return p.GetType().GetField("Text")?.GetValue(p) as TMP_Text;
        return null;
    }

    // A slider dragged to v: pressed, moved over 3 frames, held 0.8 s (past the save debounce) and released. During the drag
    // the value is written (wrote) and the file keeps its bytes; within 2 s of the release the file changes. The overlay's text
    // starts at a size no panel has: an apply during the drag puts panel's size back (applies), none leaves it (!applies).
    private static IEnumerator SliderDrag(Action<string, bool, string> check, Action<string> info, string name, string file, Slider s, float min, float max, float v,
        Func<float, bool> wrote, PanelConfig panel, bool applies) {
        const float unset = 7f;
        PanelsOverlay.ConfMgr.Save();
        byte[] before = Bytes(file);
        bool during = false, still = false;
        TMP_Text live = LiveText(panel);
        if(live != null) live.fontSize = unset;
        float? drawn = null;
        if(s != null) {
            KvPointerTo<PointerDownEvent>(s);
            for(int i = 1; i <= 3; i++) {
                s.value = (Mathf.Lerp(s.value * (max - min) + min, v, i / 3f) - min) / (max - min);
                yield return Frames(1);
            }
            during = wrote(v);
            drawn = live?.fontSize;
            float until = Time.realtimeSinceStartup + 0.8f;
            while(Time.realtimeSinceStartup < until) yield return null;
            still = SameBytes(Bytes(file), before);
            KvPointerTo<PointerUpEvent>(s);
        }
        yield return SavedSince(file, before);
        bool saved = !SameBytes(Bytes(file), before);
        PanelsOverlay.Apply();   // the overlay's text back to the panel's size
        bool applied = drawn is not { } d || (applies ? d == panel.FontSize : d == unset);
        if(live == null) info($"{name}: the panels overlay is not built, so whether the drag applies is not checked");
        check(name, s != null && during && still && saved && applied,
            $"slider={s != null} written={during} unsaved while held={still} saved after={saved} overlay size={drawn?.ToString() ?? "not built"}");
    }

    // A press on the slider, a 60 px move up the list and a release, as a drag starting on the slider would send them, with the
    // slider set to v in between.
    private static bool SlideOnList(Slider s, float min, float max, float v) {
        Vector2 at = s.worldBound.center;
        Pointer<PointerDownEvent>(s, at);
        Pointer<PointerMoveEvent>(s, at + new Vector2(0f, -30f));
        s.value = (v - min) / (max - min);
        Pointer<PointerMoveEvent>(s, at + new Vector2(0f, -60f));
        Pointer<PointerUpEvent>(s, at + new Vector2(0f, -60f));
        return true;
    }
}
#endif
