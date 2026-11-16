#if !IL2CPP
using Facet;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.Tuf;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TufPages;
/// <summary>TUF ▸ Settings in the new menu, row for row as TufSettingsUI: the browser's switches, then the level library's
/// summary, folder and buttons. With no TUF service the page is its header alone, as the classic page is its heading alone.
/// The library is measured, and the service's changes followed, only while the page is shown.</summary>
internal static class TufSettingsPage {
    // A folder the last Pick or Reset refused, and why; the next Pick or Reset clears it.
    private static string notice = "";

    internal static void Build(VisualElement p) =>
        ShellPage.Feature(p, "tuf", "TUF_SETTINGS", "TUF_SETTINGS_DESC", null, Cards);

    private static void Cards(VisualElement p) {
        TufService service = TufService.Instance;
        if(service == null) return;
        bool helper = TufHelperLiteLink.Installed;
        VisualElement card = Controls.Card(p, null);
        if(helper) Row(card, "TUF_LINK_HELPER", () => service.LinkTufHelperLite, service.SetLinkTufHelperLite, false);
        Row(card, "TUF_PREVIEWS", () => service.ShowPreviews, service.SetShowPreviews, true);
        Row(card, "TUF_GRID_VIEW", () => service.GridView, service.SetGridView, false);

        VisualElement library = Controls.Card(p, "TUF_LIBRARY", o => o.Id = "TUF_LIBRARY");
        ShellRows.Info(library, "TUF_LIBRARY_SUMMARY", () => (service.InstalledCount, service.LibraryBytes),
            t => string.Format(QuartzHost.Translate("TUF_LIBRARY_SUMMARY"), t.InstalledCount, TufDiskSpace.Describe(t.LibraryBytes)));
        ShellRows.Info(library, "TUF_LIBRARY_PATH", () => service.ActiveRootPath, s => s);
        Button(library, "TUF_OPEN_FOLDER", () => {
            try {
                string path = service.ActiveRootPath;
                System.IO.Directory.CreateDirectory(path);
                FileDialog.Reveal(path);
            } catch(Exception e) {
                MainCore.Log.Err($"[TufSettingsPage] Reveal failed: {e}");
            }
        });
        Button(library, "TUF_PICK_FOLDER", () => Pick(service));
        Button(library, "TUF_RESET_FOLDER", () => {
            notice = "";
            if(!service.ClearCustomLevelsRoot(out string reason)) notice = Rejection(reason);
            QuartzShell.Surface?.Refresh();
        });
        if(helper)
            ShellRows.Info(library, "TUF_LIBRARY_LINK_OVERRIDE", () => true, _ => QuartzHost.Translate("TUF_LIBRARY_LINK_OVERRIDE"),
                () => service.LinkTufHelperLite && !string.IsNullOrEmpty(service.CustomLevelsRoot), () => Role.Danger);
        ShellRows.Info(library, "TUF_STATUS", () => Status(service).Text, s => s, () => Status(service).Text.Length > 0,
            () => Status(service).Tone);

        // Only while shown: the search index builds this page detached, and must neither measure the library nor subscribe.
        Action changed = () => QuartzShell.Surface?.Refresh();
        Ui.WhileAttached(library, () => {
            service.EnsureLibraryMeasured();
            service.Changed += changed;
        }, () => service.Changed -= changed);
    }

    // The service's setters save. The classic id is the key in lower case; the tooltip is DESC_<key>.
    private static void Row(VisualElement card, string key, Func<bool> get, Action<bool> set, bool def) =>
        ShellRows.Toggle(card, key, key.ToLowerInvariant(), Bind.To(get, set), null, null, o => {
            o.Default = def;
            o.Tooltip = "DESC_" + key;
        });

    private static void Button(VisualElement card, string key, Action click) =>
        Controls.Action(card, "", key, click, o => {
            o.Id = key;
            o.Tooltip = "DESC_" + key;
        });

    private static void Pick(TufService service) {
        string picked = FileDialog.PickFolder(service.ActiveRootPath, null, null, QuartzHost.Translate("TUF_PICK_FOLDER_TITLE"));
        if(string.IsNullOrEmpty(picked)) return;
        notice = "";
        if(!service.SetCustomLevelsRoot(picked, out string reason)) {
            MainCore.Log.Wrn($"[TUF] rejected levels folder '{picked}': {reason}");
            notice = Rejection(reason);
        }
        QuartzShell.Surface?.Refresh();
    }

    // The status line's first case that holds, as the classic page's: Facet has no warning or success role, so its amber is
    // Danger and its green Accent.
    private static (string Text, Role Tone) Status(TufService s) {
        if(notice.Length > 0) return (notice, Role.Danger);
        return s.MoveState switch {
            TufMoveState.Moving => (string.Format(QuartzHost.Translate("TUF_MOVE_PROGRESS"), s.MoveDone, s.MoveTotal), Role.TextMuted),
            TufMoveState.Failed => (s.MoveError ?? "", Role.Danger),
            TufMoveState.Done when s.MoveTotal > 0 => (string.Format(QuartzHost.Translate("TUF_MOVE_DONE"), s.MoveTotal), Role.Accent),
            _ => ("", Role.TextMuted),
        };
    }

    private static string Rejection(string reason) => QuartzHost.Translate(reason switch {
        "linked" => "TUF_FOLDER_LINKED",
        "not-empty" => "TUF_FOLDER_NOT_EMPTY",
        "volume-root" => "TUF_FOLDER_VOLUME_ROOT",
        "nested" => "TUF_FOLDER_NESTED",
        "busy" => "TUF_FOLDER_BUSY",
        "symlink" => "TUF_FOLDER_SYMLINK",
        _ => "TUF_FOLDER_UNUSABLE",
    });
}
#endif
