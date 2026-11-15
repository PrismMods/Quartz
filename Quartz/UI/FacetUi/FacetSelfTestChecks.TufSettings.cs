#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Quartz.Features.Tuf;
using Quartz.IO;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// TUF settings in the new menu against its classic page: rows, search hits, Korean text and a write reaching its file, the
/// library summary, the status rows hidden while there is nothing to say, and the header switch's hotkey. It never presses
/// Open, Change or Reset. The TUF category shows only while Others is on, so Others is on for the run.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator TufSettings(Action<string, bool, string> check, Action<string> info) {
        const string page = "tuf.settings";
        TufService service = TufService.Instance;
        if(service == null) {
            info($"{page}: no TUF service, so only the page's build is checked");
            yield return FormPage("tuf", page, () => { }, () => { }, () => Header(), "others");
            yield break;
        }
        SettingsFile<TufSettings> file = SettingsOf(service);
        if(file == null) {
            check($"{page}: the TUF service's settings file is found", false, "TufService.settings");
            yield break;
        }
        string[] rows = ["TUF_PREVIEWS", "TUF_GRID_VIEW", "TUF_OPEN_FOLDER", "TUF_PICK_FOLDER", "TUF_RESET_FOLDER"];
        if(TufHelperLiteLink.Installed) rows = ["TUF_LINK_HELPER", .. rows];
        // The header switch's check reloads the feature, whose new service loads the file again and saves it at will: the
        // player's values go back into that one too.
        Newtonsoft.Json.Linq.JToken kept = null;
        yield return FeatureFormPage(check, "tuf", page, file, () => kept = file.Data.Serialize(), () => {
            SettingsFile<TufSettings> now = SettingsOf(TufService.Instance);
            if(now == null || now == file || kept == null) return;
            now.Data.Deserialize(kept);
            now.Save();
        }, [.. rows, "TUF_LIBRARY", "TUF_LIBRARY_SUMMARY"], rows, () => More(), category: "others");

        IEnumerator More() {
            bool was = service.GridView;
            yield return Saved(check, page, "TUF_GRID_VIEW", () => ClickRow("TUF_GRID_VIEW"), () => service.GridView != was, file.Path);

            // The library is measured as the page shows, so the count and size may land a frame or more later.
            string want = null, got = null;
            for(float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1f;) {
                yield return null;
                want = string.Format(QuartzHost.Translate("TUF_LIBRARY_SUMMARY"), service.InstalledCount, TufDiskSpace.Describe(service.LibraryBytes));
                got = PageContent().Q(name: "TUF_LIBRARY_SUMMARY")?.Query<Label>().ToList().Select(l => l.text).FirstOrDefault(t => t == want);
                if(got != null) break;
            }
            check($"{page}: the summary reads as the classic page's", got != null, $"want='{want}'");

            if(service.MoveState == TufMoveState.Idle && !(service.LinkTufHelperLite && service.CustomLevelsRoot.Length > 0)) {
                string[] shown = [.. new[] { "TUF_STATUS", "TUF_LIBRARY_LINK_OVERRIDE" }.Where(id => !Hidden(id))];
                check($"{page}: the status and link-override rows hide while there is nothing to say", shown.Length == 0, $"shown={string.Join(",", shown)}");
            } else {
                info($"{page}: the status and link-override rows hiding: not run, move={service.MoveState} linked={service.LinkTufHelperLite}");
            }
            yield return HeaderHotkeyCheck(check, page, "TUF_SETTINGS", "tuf");
        }

        IEnumerator Header() {
            check($"{page}: with no TUF service the page is its header", PageContent().Q(name: "TUF_SETTINGS") != null, $"page={QuartzShell.CurrentPage}");
            yield break;
        }
    }

    private static SettingsFile<TufSettings> SettingsOf(TufService service) =>
        typeof(TufService).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(service) as SettingsFile<TufSettings>;

    // Absent, or under an element the layout does not display.
    private static bool Hidden(string id) {
        VisualElement page = PageContent();
        for(VisualElement e = page.Q(name: id); e != null && e != page; e = e.parent)
            if(e.resolvedStyle.display != DisplayStyle.Flex) return true;
        return page.Q(name: id) == null;
    }
}
#endif
