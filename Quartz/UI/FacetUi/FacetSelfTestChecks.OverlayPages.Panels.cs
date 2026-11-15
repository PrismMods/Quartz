#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Overlay;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Panels in the new menu: its rows against the classic page's, its two columns' edges, a panel made here that the
/// count and the preview follow and that goes again the classic way, the panel list where the editor card was, and the
/// preview pane; on the classic page, a panel deleted while open at its end, and panels dragged into a new order, whose hotkeys
/// follow them. OverlayPanels.json and Settings.json are left byte for byte as they were.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string PanelsPage = "overlay.panels";
    // The classic page's own rows (the font and Create Panel) and the count; Covers pins the list's rows.
    private static readonly string[] PanelsRows = ["FONT_OVERLAY", "PANELS_CREATE", "PANEL_COUNT"];

    private static IEnumerator OverlayPanels(Action<string, bool, string> check, Action<string> info) {
        PanelsOverlay.EnsureConf();
        string file = PanelsOverlay.ConfMgr.Path;
        byte[] start = File.Exists(file) ? File.ReadAllBytes(file) : null;   // the player's, put back after whatever the checks save
        PanelsOverlay.ConfMgr.Save();   // pending saves, and fields newer than the file, land now: the checks compare against these bytes
        byte[] was = File.Exists(file) ? File.ReadAllBytes(file) : null;
        int had = PanelsOverlay.Conf.Panels.Count;
        try {
            yield return OnPage("panels", PanelsPage, () => { }, () => RemovePanels(had), () => PanelsChecks(check, info, file, was, had));
        } finally {
            PutBackPanelsFile(file, start);
        }
        // A debounced save already handed to a worker can land after the finally: settle, then put it back again.
        float until = Time.realtimeSinceStartup + 1f;
        while(Time.realtimeSinceStartup < until) yield return null;
        PutBackPanelsFile(file, start);
        check($"{PanelsPage}: OverlayPanels.json ends as the run found it, byte for byte, or absent where there was none",
            SameBytes(file, start), $"file={start?.Length.ToString() ?? "none"}/{(File.Exists(file) ? new FileInfo(file).Length.ToString() : "none")}");
    }

    private static void PutBackPanelsFile(string file, byte[] start) {
        PanelsOverlay.ConfMgr.CancelPendingSave();
        if(start == null) { if(File.Exists(file)) File.Delete(file); }
        else if(!SameBytes(file, start)) File.WriteAllBytes(file, start);
    }

    private static IEnumerator PanelsChecks(Action<string, bool, string> check, Action<string> info, string file, byte[] was, int had) {
        yield return Parity(check, PanelsPage, PanelsRows);
        yield return ColumnEdges(check, PanelsPage, PanelsOverlay.FontKey);
        yield return PanelsCreate(check, file, was);
        PanelsListStands(check);
        yield return PanelsDeleteKeepsPage(check);
        yield return PanelsDragKeepsBinds(check, info);
        // With no panel there is nothing to preview: one is made for the preview checks, and goes after them.
        if(had == 0) PanelsOverlay.CreatePanel();
        yield return PanelsNote(check);
        yield return PanelsFailedLease(check);
        PanelConfig first = PanelsOverlay.Conf.Panels[0];
        Color colour = first.GetTextColor(), want = SameRgb(colour, Color.red) ? Color.green : Color.red;
        try {
            // No row here sets a panel's colour: the overlay's own path, Conf then Apply (never saved), read off the first panel's text.
            yield return PreviewChecks(check, info, PanelsPage, PanelsOverlay.FontKey, () => {
                first.SetTextColor(want);
                PanelsOverlay.Apply();
                return true;
            }, () => OverlayPreview.ShownOf(PanelsOverlay.FontKey)?.transform.Find("Text")?.GetComponent<TextMeshProUGUI>() is { } text
                && SameRgb(text.color, want));
        } finally {
            first.SetTextColor(colour);
            PanelsOverlay.Apply();
            RemovePanels(had);
        }
        PanelsOverlay.ConfMgr.Save();   // now, not half a second on
        (int left, bool same) = (PanelsOverlay.Conf.Panels.Count, SameFile(file, was, out string detail));
        check($"{PanelsPage}: after the page's checks, the preview's power-off among them, the list and OverlayPanels.json are as before",
            same && left == had, $"panels={left}/{had} {detail}");
        SearchButtonRow(check, PanelsPage, "PANELS_CREATE");
    }

    // Create Panel adds a panel: within 3 frames the count reads one more and the preview frames the new panel alone, at its
    // true size, which, empty, shows its name; the classic page is due a rebuild. Taken out again as the classic page's Delete
    // does, the list and OverlayPanels.json are as before.
    private static IEnumerator PanelsCreate(Action<string, bool, string> check, string file, byte[] was) {
        List<PanelConfig> list = PanelsOverlay.Conf.Panels;
        int n = list.Count, frames = 0;
        for(; n > 0 && frames < 10 && OverlayPreview.Current is not { Released: false }; frames++) yield return null;   // the pane's lease
        Label count = Body.Q<Label>("PANEL_COUNT-input");
        Button create = Body.Q<Button>("PANELS_CREATE-input");
        string before = count?.text, want = OverlayPages.Panels.CountText(n + 1);
        string Named() => OverlayPreview.FrameOf(PanelsOverlay.FontKey)?.LastOrDefault()?.Find("Text")?.GetComponent<TextMeshProUGUI>()?.text;
        bool Grew() => list.Count == n + 1 && count?.text == want && Framed() == 1 && Named() == list[^1].Name;
        UICore.ClassicStale = false;
        if(create != null) Submit(create);
        bool stale = UICore.ClassicStale;
        for(frames = 0; frames < 3 && !Grew(); frames++) yield return null;
        (bool grew, int framed, string named) = (Grew(), Framed(), Named());
        RemovePanels(n);
        int left = list.Count;
        PanelsOverlay.ConfMgr.Save();   // now, not half a second on
        bool same = SameFile(file, was, out string detail);
        check($"{PanelsPage}: Create Panel adds one, which the count follows and the preview frames alone within 3 frames and the classic page "
            + "is due to show, and taken out again the classic way it leaves the list and OverlayPanels.json as before",
            create != null && grew && stale && left == n && same,
            $"button={create != null} panels={n}->{n + 1}->{left} count='{before}'->'{count?.text}' want='{want}' framed={framed} named='{named}' frames={frames} "
            + $"stale={stale} {detail}");
    }

    // The list of panels stands in the card the editor card followed, and nothing on the page opens the classic menu.
    private static void PanelsListStands(Action<string, bool, string> check) {
        VisualElement page = PageContent();
        bool list = page.Q(name: "PANELS_LIST") != null, link = page.Q(name: "MENU_PANELS_EDITOR") != null || page.Q(name: "menu-classic") != null;
        check($"{PanelsPage}: the panel list stands where the editor card was, with no link to the classic menu on the page", list && !link,
            $"list={list} link={link} {Showing()}");
    }

    // Deleting a panel open at the classic page's end leaves the page showing its rows: the offset follows the shorter content
    // within 2 frames, and the content after its glide. Before, the offset stayed past the new end and the page was blank,
    // its "Use this feature" switch too, until the next wheel notch. The panel deleted is a throwaway one, named so that no
    // panel of the player's can be taken for it, and only it goes: Delete Panel removes its own config. It also rekeys the
    // player's toggle hotkeys and saves them: those and Settings.json are put back.
    private static IEnumerator PanelsDeleteKeepsPage(Action<string, bool, string> check) {
        List<PanelConfig> list = PanelsOverlay.Conf.Panels, keep = [.. list];
        PanelConfig made = new() { Name = "Self-test " + Guid.NewGuid().ToString("N")[..8] };
        (Dictionary<string, (int Modifier, int Key)> binds, byte[] settings) = HoldBinds();
        list.Add(made);
        PanelsOverlay.Rebuild();
        int state = NavRegistry.StateFor(PanelsPage);
        bool opened = false, found = false, clicked = false, shows = false;
        float before = float.NaN, offset = float.NaN, max = float.NaN, at = float.NaN, end = float.NaN;
        try {
            UICore.ClassicStale = true;   // the classic page lists the new panel
            QuartzShell.OpenClassic(state);
            yield return Frames(2);
            opened = UICore.IsOpen && UICore.CurrentMenuState == state;
            RectTransform page = UICore.Pages.TryGetValue(state, out RectTransform p) ? p : null;
            UIScrollController sc = page != null ? page.GetComponentInChildren<UIScrollController>(true) : null;
            GenerateUI.CollapsibleSection sec = page == null ? null
                : GenerateUI.Sections.LastOrDefault(s => s.Body != null && s.Section != null && s.Section.IsChildOf(page) && s.Title == made.Name);
            if(opened && sc != null && sec != null) {
                found = true;
                sec.SetExpanded(true, false, false);   // not saved: Quartz's settings keep their bytes
                yield return Frames(2);
                sc.JumpTo(float.MaxValue);   // the page's end, Delete Panel in view
                yield return Frames(1);
                before = sc.Offset;
                clicked = ClickLabel(sec.Section, MainCore.Tr.Get(GenerateUI.LocaleKeyFromId("panel0_delete"), "Delete Panel"));
                yield return Frames(2);
                offset = sc.Offset;
                yield return WaitGlide();
                max = Mathf.Max(0f, sc.content.rect.height - sc.viewport.rect.height);
                at = sc.content.anchoredPosition.y;
                end = sc.content.rect.height;
                shows = sc.content.Find("PanelsList") is RectTransform rows && VisibleHeight(rows, sc.viewport) > 1f;
            }
        } finally {
            if(list.Remove(made)) {   // the click missed: the throwaway goes, by reference
                PanelsOverlay.Save();
                PanelsOverlay.Rebuild();
            }
            PutBackBinds(binds);
            if(UICore.IsOpen) UICore.Close();
        }
        bool[] kept = [false];
        yield return BindsFileBack(settings, kept);
        bool same = list.Count == keep.Count && keep.All(list.Contains);
        check($"{PanelsPage}: deleting a panel open at the classic page's end leaves the page showing its rows, the offset within the "
            + "shorter content in 2 frames and the content there after its glide, and only that panel goes; Settings.json ends as before",
            found && clicked && same && before > max + 0.5f && offset <= max + 0.5f && at <= max + 0.5f && shows && kept[0],
            $"opened={opened} found={found} clicked={clicked} offset {before:0.#}->{offset:0.#} at={at:0.#} max={max:0.#} content={end:0.#} "
            + $"blank before the fix={before >= end} rows shown={shows} panels={list.Count}/{keep.Count} same={same} settings same={kept[0]} {Showing()}");
    }

    // Two throwaway panels at the list's end: X, open, with a hotkey on its Text Shadow, and Y, shut, with one on its Background.
    // X dragged by its handle below Y takes its hotkey to its new place and Y's moves up with Y; each section opens or stays
    // shut as before, and the page stays where it was scrolled. X deleted then from its section, built again for its new
    // place, takes only its own hotkey. The hotkeys have no key, so nothing fires; every other hotkey stays as it was, and
    // the player's hotkeys and Settings.json are put back.
    private static IEnumerator PanelsDragKeepsBinds(Action<string, bool, string> check, Action<string> info) {
        List<PanelConfig> list = PanelsOverlay.Conf.Panels, keep = [.. list];
        int n = list.Count;
        if(n + 2 > PanelsOverlay.PanelSoftCap) {   // the "that's a lot" note heads the list, and a drag's target is off by one under it
            info($"{PanelsPage}: {n} panels, over {PanelsOverlay.PanelSoftCap} with two more: the drag hotkey check is skipped");
            yield break;
        }
        PanelConfig x = new() { Name = "Self-test X " + Guid.NewGuid().ToString("N")[..8] }, y = new() { Name = "Self-test Y " + Guid.NewGuid().ToString("N")[..8] };
        Dictionary<string, (int Modifier, int Key)> c = MainCore.Conf.ToggleKeybinds;
        (Dictionary<string, (int Modifier, int Key)> binds, byte[] settings) = HoldBinds();
        (int, int) xBind = (1, 0), yBind = (2, 0);
        static string xAt(int i) => $"panel{i}_textshadow";
        static string yAt(int i) => $"panel{i}_background";
        foreach(string id in c.Keys.Where(k => k.StartsWith($"panel{n}_") || k.StartsWith($"panel{n + 1}_")).ToList()) c.Remove(id);   // past the list: no panel's
        Dictionary<string, (int Modifier, int Key)> others = new(c);
        bool Binds(params (string id, (int, int) bind)[] want) =>
            c.Count == others.Count + want.Length && others.All(kv => c.TryGetValue(kv.Key, out var b) && b == kv.Value)
            && want.All(w => c.TryGetValue(w.id, out var b) && b == w.bind);
        c[xAt(n)] = xBind;
        c[yAt(n + 1)] = yBind;
        list.Add(x);
        list.Add(y);
        PanelsOverlay.Rebuild();
        int state = NavRegistry.StateFor(PanelsPage);
        bool opened = false, found = false, moved = false, dragBinds = false, shut = false, scrolled = false, clicked = false, deleteBinds = false;
        float before = float.NaN, after = float.NaN, at = float.NaN;
        try {
            UICore.ClassicStale = true;   // the classic page lists the new panels
            QuartzShell.OpenClassic(state);
            yield return Frames(2);
            opened = UICore.IsOpen && UICore.CurrentMenuState == state;
            RectTransform page = UICore.Pages.TryGetValue(state, out RectTransform p) ? p : null;
            UIScrollController sc = page != null ? page.GetComponentInChildren<UIScrollController>(true) : null;
            GenerateUI.CollapsibleSection Sec(PanelConfig panel) => page == null ? null
                : GenerateUI.Sections.LastOrDefault(s => s.Body != null && s.Section != null && s.Section.IsChildOf(page) && s.Title == panel.Name);
            GenerateUI.CollapsibleSection sx = Sec(x), sy = Sec(y);
            Transform handle = sx?.Section.Find("Header/LayerHandle");
            EventSystem es = EventSystem.current;
            if(opened && sc != null && sx != null && sy != null && handle != null && es != null) {
                found = true;
                sx.SetExpanded(true, false, false);   // not saved: Quartz's settings keep their bytes
                yield return Frames(2);
                sc.JumpTo(float.MaxValue);
                yield return Frames(1);
                before = sc.Offset;
                var drag = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = sx.Section.position };
                ExecuteEvents.Execute(handle.gameObject, drag, ExecuteEvents.beginDragHandler);
                yield return null;
                drag.position = new Vector2(drag.position.x, sy.Section.position.y - 1f);   // just below Y's middle
                ExecuteEvents.Execute(handle.gameObject, drag, ExecuteEvents.dragHandler);
                yield return null;
                ExecuteEvents.Execute(handle.gameObject, drag, ExecuteEvents.endDragHandler);
                float until = Time.realtimeSinceStartup + 1f;   // the drop glides 0.12 s, then commits
                while(Time.realtimeSinceStartup < until && list.IndexOf(x) != n + 1) yield return null;
                yield return Frames(2);
                moved = list.Count == n + 2 && list[n] == y && list[n + 1] == x;
                dragBinds = Binds((xAt(n + 1), xBind), (yAt(n), yBind));
                (sx, sy) = (Sec(x), Sec(y));
                shut = sx is { Expanded: true } && sy is { Expanded: false } && sy.Section.GetSiblingIndex() < sx.Section.GetSiblingIndex();
                after = sc.Offset;
                yield return WaitGlide();
                at = sc.content.anchoredPosition.y;
                scrolled = Mathf.Abs(after - before) < 0.5f && Mathf.Abs(at - before) < 0.5f;
                clicked = sx != null && ClickLabel(sx.Section, MainCore.Tr.Get(GenerateUI.LocaleKeyFromId("panel0_delete"), "Delete Panel"));
                yield return Frames(2);
                deleteBinds = list.Count == n + 1 && list[n] == y && Binds((yAt(n), yBind));
            }
        } finally {
            if(!list.SequenceEqual(keep)) {   // the throwaways go, and the player's panels are in their order
                list.Clear();
                list.AddRange(keep);
                PanelsOverlay.Save();
                PanelsOverlay.Rebuild();
            }
            UICore.ClassicStale = true;
            PutBackBinds(binds);
            if(UICore.IsOpen) UICore.Close();
        }
        bool[] kept = [false];
        yield return BindsFileBack(settings, kept);
        check($"{PanelsPage}: a panel dragged below another takes its hotkey along and the other's moves with it, each section opens "
            + "or stays shut as before and the page keeps its scroll; deleted then, it takes only its own hotkey; Settings.json ends as before",
            found && moved && dragBinds && shut && scrolled && clicked && deleteBinds && kept[0],
            $"opened={opened} found={found} moved={moved} drag binds={dragBinds} open/shut kept={shut} offset {before:0.#}->{after:0.#} at={at:0.#} "
            + $"clicked={clicked} delete binds={deleteBinds} panels={list.Count}/{keep.Count} settings same={kept[0]} {Showing()}");
    }

    // The player's toggle hotkeys, which Delete Panel and a drop rekey and save, with Settings.json's bytes, before the checks.
    private static (Dictionary<string, (int Modifier, int Key)> binds, byte[] file) HoldBinds() {
        MainCore.ConfMgr.Save();   // pending saves land now: the bytes compared at the end are these
        return (new(MainCore.Conf.ToggleKeybinds), File.ReadAllBytes(MainCore.ConfMgr.Path));
    }

    private static void PutBackBinds(Dictionary<string, (int Modifier, int Key)> binds) {
        MainCore.ConfMgr.CancelPendingSave();
        MainCore.Conf.ToggleKeybinds.Clear();
        foreach(var kv in binds) MainCore.Conf.ToggleKeybinds[kv.Key] = kv.Value;
    }

    // A save already handed to a worker can land after the put-back: settle, then save what was put back. same[0]: the file is as held.
    private static IEnumerator BindsFileBack(byte[] file, bool[] same) {
        float until = Time.realtimeSinceStartup + 1f;
        while(Time.realtimeSinceStartup < until) yield return null;
        MainCore.ConfMgr.Save();
        same[0] = SameBytes(MainCore.ConfMgr.Path, file);
    }

    // How much of r's height the viewport shows, in the viewport's units.
    private static float VisibleHeight(RectTransform r, RectTransform viewport) {
        var corners = new Vector3[4];
        r.GetWorldCorners(corners);
        float bottom = viewport.InverseTransformPoint(corners[0]).y, top = viewport.InverseTransformPoint(corners[1]).y;
        return Mathf.Min(top, viewport.rect.yMax) - Mathf.Max(bottom, viewport.rect.yMin);
    }

    // The note under an empty stage says why it is empty: no panels (none in memory, never saved), or, with panels there but
    // Quartz off, the general reason. Within 3 frames each, translated.
    private static IEnumerator PanelsNote(Action<string, bool, string> check) {
        string none = QuartzHost.Translate("PANEL_NO_PANELS"), general = QuartzHost.Translate("MENU_PREVIEW_IN_LEVEL");
        Label Note() => Body.Q<Label>(PanelsOverlay.FontKey + "-preview-empty");
        bool Says(string text) => Note() is { } note && Displayed(note) && note.text == text;
        List<PanelConfig> list = PanelsOverlay.Conf.Panels, keep = [.. list];
        bool empty, off;
        int frames;
        PanelsOverlay.ConfMgr.CancelPendingSave();   // nothing saves the emptied list
        try {
            list.Clear();
            PanelsOverlay.Rebuild();
            for(frames = 0; frames < 3 && !Says(none); frames++) yield return null;
            empty = Says(none);
        } finally {
            if(list.Count == 0) list.AddRange(keep);
            PanelsOverlay.Rebuild();
        }
        string emptyText = Note()?.text;
        MainCore.Runtime.SetModEnabled(false, false, save: false);
        try {
            for(frames = 0; frames < 3 && !Says(general); frames++) yield return null;
            off = Says(general);
        } finally {
            MainCore.Runtime.SetModEnabled(true, false, save: false);
        }
        string offText = Note()?.text;
        for(frames = 0; frames < 10 && OverlayPreview.Current is not { Released: false }; frames++) yield return null;   // the pane's lease again
        check($"{PanelsPage}: with no panels the empty stage's note says there are none, and with panels but Quartz off it gives the general reason",
            empty && off && list.Count == keep.Count, $"empty='{emptyText}' want='{none}' off='{offText}' want='{general}' panels={list.Count}/{keep.Count}");
    }

    // A lease that fails once the preview has begun hides the panels again at once, as they were before it outside a level.
    private static IEnumerator PanelsFailedLease(Action<string, bool, string> check) {
        QuartzShell.Hide();   // the pane gives its lease back
        yield return Frames(3);
        GameObject root = OverlayPreview.ShownOf(PanelsOverlay.FontKey);
        bool was = root != null && root.activeSelf;
        string threw = null;
        OverlayPreview.FailNextLease = true;
        try {
            OverlayPreview.Take(PanelsOverlay.FontKey)?.Release();
        } catch(InvalidOperationException e) {
            threw = e.Message;
        } finally {
            OverlayPreview.FailNextLease = false;
        }
        bool now = root != null && root.activeSelf;
        yield return Frames(3);
        check($"{PanelsPage}: a lease that fails after the preview began leaves the panels shown or hidden as before it, at once and 3 frames on",
            threw != null && root != null && now == was && root.activeSelf == was && OverlayPreview.Target == null,
            $"threw={threw != null} root={root != null} before={was} at once={now} after={root != null && root.activeSelf} target={OverlayPreview.Target}");
    }

    // The rects the live lease frames that are drawn at a size: none without a lease.
    private static int Framed() => OverlayPreview.Current is { Released: false }
        ? OverlayPreview.FrameOf(PanelsOverlay.FontKey)?.Count(r => r != null && r.rect.width > 0f && r.rect.height > 0f) ?? 0 : 0;

    // The panels past the first keep, taken out as the classic page's Delete takes one.
    private static void RemovePanels(int keep) {
        List<PanelConfig> list = PanelsOverlay.Conf.Panels;
        if(list.Count <= keep) return;
        list.RemoveRange(keep, list.Count - keep);
        PanelsOverlay.Save();
        PanelsOverlay.Rebuild();
    }

    // The file as before the run. Where there was none, the checks' saves made one: it goes again.
    private static bool SameFile(string file, byte[] was, out string detail) {
        bool made = was == null && File.Exists(file);
        if(made) File.Delete(file);
        byte[] now = File.Exists(file) ? File.ReadAllBytes(file) : null;
        bool same = was == null ? now == null : now != null && now.SequenceEqual(was);
        detail = $"file={was?.Length.ToString() ?? "none"}/{now?.Length.ToString() ?? "none"}{(made ? " (made, deleted)" : "")} bytes same={same}";
        return same;
    }
}
#endif
