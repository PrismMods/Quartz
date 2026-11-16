#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Overlay;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using TMPro;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Panels in the new menu: its rows, its two columns' edges, a panel made here that the count and the preview
/// follow, the panel list where the editor card was, and the preview pane. OverlayPanels.json and Settings.json are left
/// byte for byte as they were.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string PanelsPage = "overlay.panels";
    // The page's own rows (the font and Create Panel) and the count.
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

    // The font row lists the game's own UI face; picked there, it is stored as the sentinel and the row shows it after the rebuild.
    private static IEnumerator PanelsGameFont(Action<string, bool, string> check) {
        string key = PanelsOverlay.FontKey, was = FontManager.OverlayFontName(key) ?? FontManager.FollowGlobal;
        string item = OverlayFontPicker.Display(FontManager.GameDefault);
        bool listed = OverlayFontPicker.Values().Contains(FontManager.GameDefault);
        try {
            yield return PickItem("FONT_OVERLAY", item);
            yield return Frames(2);
            string stored = FontManager.OverlayFontName(key), shown = Body.Q<Button>("FONT_OVERLAY-input")?.text;
            check($"{PanelsPage}: the font menu offers the game's default font and it sticks",
                listed && stored == FontManager.GameDefault && shown != null && shown.Contains(item) && FontManager.For(key) != null,
                $"listed={listed} stored={stored} shown='{shown}' item='{item}' face={FontManager.GameDefaultFace}");
        } finally {
            FontManager.SetOverlayFont(key, was);
        }
        yield return Frames(2);
    }

    private static void PutBackPanelsFile(string file, byte[] start) {
        PanelsOverlay.ConfMgr.CancelPendingSave();
        if(start == null) { if(File.Exists(file)) File.Delete(file); }
        else if(!SameBytes(file, start)) File.WriteAllBytes(file, start);
    }

    private static IEnumerator PanelsChecks(Action<string, bool, string> check, Action<string> info, string file, byte[] was, int had) {
        yield return Parity(check, PanelsPage, PanelsRows);
        yield return ColumnEdges(check, PanelsPage, PanelsOverlay.FontKey);
        yield return PanelsGameFont(check);
        yield return PanelsCreate(check, file, was);
        PanelsListStands(check);
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
    // true size, which, empty, shows its name. Taken out again as a panel's Delete does, the list and OverlayPanels.json are
    // as before.
    private static IEnumerator PanelsCreate(Action<string, bool, string> check, string file, byte[] was) {
        List<PanelConfig> list = PanelsOverlay.Conf.Panels;
        int n = list.Count, frames = 0;
        for(; n > 0 && frames < 10 && OverlayPreview.Current is not { Released: false }; frames++) yield return null;   // the pane's lease
        Label count = Body.Q<Label>("PANEL_COUNT-input");
        Button create = Body.Q<Button>("PANELS_CREATE-input");
        string before = count?.text, want = OverlayPages.Panels.CountText(n + 1);
        string Named() => OverlayPreview.FrameOf(PanelsOverlay.FontKey)?.LastOrDefault()?.Find("Text")?.GetComponent<TextMeshProUGUI>()?.text;
        bool Grew() => list.Count == n + 1 && count?.text == want && Framed() == 1 && Named() == list[^1].Name;
        if(create != null) Submit(create);
        for(frames = 0; frames < 3 && !Grew(); frames++) yield return null;
        (bool grew, int framed, string named) = (Grew(), Framed(), Named());
        RemovePanels(n);
        int left = list.Count;
        PanelsOverlay.ConfMgr.Save();   // now, not half a second on
        bool same = SameFile(file, was, out string detail);
        check($"{PanelsPage}: Create Panel adds one, which the count follows and the preview frames alone within 3 frames, and taken "
            + "out again it leaves the list and OverlayPanels.json as before",
            create != null && grew && left == n && same,
            $"button={create != null} panels={n}->{n + 1}->{left} count='{before}'->'{count?.text}' want='{want}' framed={framed} named='{named}' frames={frames} "
            + detail);
    }

    // The list of panels stands in the card the editor card followed, and nothing on the page links to an old editor.
    private static void PanelsListStands(Action<string, bool, string> check) {
        VisualElement page = PageContent();
        bool list = page.Q(name: "PANELS_LIST") != null, link = page.Q(name: "MENU_PANELS_EDITOR") != null || page.Q(name: "menu-classic") != null;
        check($"{PanelsPage}: the panel list stands where the editor card was, with no link to an old editor on the page", list && !link,
            $"list={list} link={link} {Showing()}");
    }

    // The player's toggle hotkeys, which a panel's move or delete rekeys and saves, with Settings.json's bytes, before the checks.
    private static (Dictionary<string, (int Modifier, int Key)> binds, byte[] file) HoldBinds() {
        MainCore.ConfMgr.Save();   // pending saves land now: the bytes compared at the end are these
        return (new(MainCore.Conf.ToggleKeybinds), File.ReadAllBytes(MainCore.ConfMgr.Path));
    }

    private static void PutBackBinds(Dictionary<string, (int Modifier, int Key)> binds) {
        MainCore.ConfMgr.CancelPendingSave();
        MainCore.Conf.ToggleKeybinds.Clear();
        foreach(var kv in binds) MainCore.Conf.ToggleKeybinds[kv.Key] = kv.Value;
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

    // The panels past the first keep, taken out as a panel's Delete takes one.
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
