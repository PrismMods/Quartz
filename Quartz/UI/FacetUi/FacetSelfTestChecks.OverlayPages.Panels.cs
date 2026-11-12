#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Panels in the new menu: its rows against the classic page's, its two columns' edges, a panel made here that the
/// count and the preview follow and that goes again the classic way, the editor card's way to the classic menu and back, and
/// the preview pane. OverlayPanels.json is left byte for byte as it was.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string PanelsPage = "overlay.panels";
    // The classic page's own rows (the font and Create Panel), the count, and the card that opens the panels' editor.
    private static readonly string[] PanelsRows = ["FONT_OVERLAY", "PANELS_CREATE", "PANEL_COUNT", "MENU_PANELS_EDITOR"];

    private static IEnumerator OverlayPanels(Action<string, bool, string> check, Action<string> info) {
        PanelsOverlay.EnsureConf();
        string file = PanelsOverlay.ConfMgr.Path;
        byte[] was = File.Exists(file) ? File.ReadAllBytes(file) : null;
        int had = PanelsOverlay.Conf.Panels.Count;
        yield return OnPage("panels", PanelsPage, () => { }, () => RemovePanels(had), () => PanelsChecks(check, info, file, was, had));
    }

    private static IEnumerator PanelsChecks(Action<string, bool, string> check, Action<string> info, string file, byte[] was, int had) {
        yield return Parity(check, PanelsPage, PanelsRows);
        yield return ColumnEdges(check, PanelsPage, PanelsOverlay.FontKey);
        yield return PanelsCreate(check, file, was);
        yield return EditorLink(check, PanelsPage, "MENU_PANELS_EDITOR");
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
