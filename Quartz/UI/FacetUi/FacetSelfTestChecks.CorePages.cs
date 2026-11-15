#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.IO;
using Quartz.Localization;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
using Quartz.Update;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The core pages in the new menu against their classic pages: rows, Korean text, every button that acts going through Do,
/// and what each page reads. A page the rail hides on this build is shown with ShowForTest; its classic page is built off
/// screen.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string DeveloperKey = "developer.main";

    private static IEnumerator Developer(Action<string, bool, string> check, Action<string> info) {
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        // The classic page as the menu builds it, under the menu's panel: its keys, and its status for the compare.
        var host = new GameObject("FacetSelfTestPage", typeof(RectTransform)).GetComponent<RectTransform>();
        host.SetParent(UICore.Panel, false);
        try {
            PageDeveloper.Create(host);
            string[] classic = [.. host.GetComponentsInChildren<TextLocalization>(true).Select(l => l.Key)
                .Where(k => !string.IsNullOrEmpty(k) && k != "DEVELOPER")];

            FacetBridge.OpenNewMenu(CorePages.HomePageKey);
            yield return Frames(3);
            QuartzShell.Go(DeveloperKey);
            yield return Frames(2);
            check($"{DeveloperKey} is on the rail only when Info.IsDev", (QuartzShell.CurrentPage == DeveloperKey) == Info.IsDev,
                $"dev={Info.IsDev} page={QuartzShell.CurrentPage}");
            QuartzShell.Go(CorePages.HomePageKey);
            yield return Frames(2);

            int listeners = UpdateListeners();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            QuartzShell.ShowForTest(DeveloperKey);
            info($"[perf] page {DeveloperKey} first build: {clock.Elapsed.TotalMilliseconds:0.0} ms");
            yield return Frames(2);
            int shown = UpdateListeners();

            Covers(check, DeveloperKey, classic: classic);
            Translated(check, DeveloperKey, ["SECTION_SEASONAL"]);
            yield return Recorded(check, $"{DeveloperKey} DEV_SIM_UPDATE: a click acts only through Do", () => ClickRow("DEV_SIM_UPDATE"),
                "developer.simulate:" + !UpdateService.DevSimulate);
            yield return Recorded(check, $"{DeveloperKey} DEV_CHECK: a click acts only through Do", () => PressRow("DEV_CHECK"), "developer.check");
            yield return Recorded(check, $"{DeveloperKey} DEV_CLEAR_SKIP: a click acts only through Do", () => PressRow("DEV_CLEAR_SKIP"),
                "developer.clearskip");
            yield return Recorded(check, $"{DeveloperKey} DEV_APRIL_FOOLS: a click acts only through Do", () => ClickRow("DEV_APRIL_FOOLS"),
                "developer.aprilfools:" + !AprilFools.DevForced);

            foreach(string id in new[] { "DEV_SIM_UPDATE", "DEV_APRIL_FOOLS" }) {
                VisualElement row = PageContent().Q(name: id);
                if(row != null) RightClick(row);
                yield return Frames(2);
                bool offered = MenuRow(QuartzHost.Host.Translate("MENU_SET_HOTKEY")) != null;
                Popup.CloseAll();
                check($"{DeveloperKey} {id}: right-click offers Set hotkey, shared with the classic toggle", offered, $"row={row != null}");
            }

            // A value the status shows changes after the build, in memory only: Refresh must read it again.
            string skipped = MainCore.Conf.SkippedVersion, marker = "selftest-" + Time.frameCount, want, got;
            bool pressed;
            MainCore.Conf.SkippedVersion = marker;
            try {
                pressed = PressRow("DEV_REFRESH");
                yield return Frames(2);
                PageDeveloper.RefreshStatus();
                want = (typeof(PageDeveloper).GetField("statusText", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as TMP_Text)?.text;
                got = PageContent().Q<Label>("developer-status-input")?.text;
            } finally {
                MainCore.Conf.SkippedVersion = skipped;
            }
            check($"{DeveloperKey} DEV_REFRESH: the status reads again, as the classic page's",
                pressed && want != null && got == want && got.Contains(marker),
                $"pressed={pressed} classic={want?.Length.ToString() ?? "none"} chars, new={got?.Length.ToString() ?? "none"} chars, "
                + $"marker={got?.Contains(marker)}");

            if(Info.IsDev) Searched(check, DeveloperKey, ["DEV_CHECK"]);
            else info($"{DeveloperKey}: the search index follows the rail, so Searched needs a dev build");

            QuartzShell.Go(CorePages.HomePageKey);
            yield return Frames(2);
            int left = UpdateListeners();
            check($"{DeveloperKey}: the status follows UpdateService.OnChanged only while attached", shown == listeners + 1 && left == listeners,
                $"listeners={listeners} shown={shown} left={left}");
        } finally {
            UnityEngine.Object.Destroy(host.gameObject);
            // The classic page's status line now points at the page built here: a dev build's classic menu builds its own again.
            if(Info.IsDev) UICore.ClassicStale = true;
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(2);
    }

    private static int UpdateListeners() =>
        (typeof(UpdateService).GetField(nameof(UpdateService.OnChanged), BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Delegate)
        ?.GetInvocationList().Length ?? 0;

    // A button row pressed as a player presses it.
    private static bool PressRow(string id) {
        Button b = PageContent().Q<Button>(id + "-input");
        if(b != null) Submit(b);
        return b != null;
    }

    private const string ProfilesKey = "profiles.main";

    // Profiles: nothing here creates, applies, imports, exports or deletes a profile, recalibrates or opens a dialog. Each of
    // those buttons is checked through Do, and the profiles directory must read the same at the end.
    private static IEnumerator Profiles(Action<string, bool, string> check, Action<string> info) {
        static string[] Dirs() => [.. Directory.GetDirectories(ProfileManager.ProfilesPath).OrderBy(d => d, StringComparer.Ordinal)];
        string[] before = Dirs();
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            FacetBridge.OpenNewMenu(CorePages.HomePageKey);
            yield return Frames(3);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            QuartzShell.Go(ProfilesKey);
            info($"[perf] page {ProfilesKey} first build: {clock.Elapsed.TotalMilliseconds:0.0} ms");
            yield return Frames(2);

            // Select and Delete are numbered by the row's place in the whole list, and the active row has neither, so the
            // classic page's count of them is no Id range; the list check below holds them row by row.
            Covers(check, ProfilesKey, omitted: ["PROFILE_SELECT", "PROFILE_DELETE"]);
            Searched(check, ProfilesKey, ["PROFILE_RECALIBRATE", "PROFILES"]);
            Translated(check, ProfilesKey, ["PROFILE_ADD"]);
            ProfilesList(check);
            string active = ProfileManager.Active;
            List<SearchIndex.Hit> named;
            try {
                named = Find(active);
            } finally {
                QuartzShell.Nav?.Filter("");
            }
            check($"{ProfilesKey}: a profile's name is no search row", !named.Any(h => h.Key == active), Hits(named));

            string pid = System.Diagnostics.Process.GetCurrentProcess().Id.ToString(), free = "selftest-" + pid;
            string typed = (free + "-").PadRight(40, 'x');
            TextField field = PageContent().Q<TextField>("PROFILE_NAME-input");
            TypeInto("PROFILE_NAME-input", typed);
            yield return Frames(1);
            check($"{ProfilesKey} PROFILE_NAME: holds 32 characters", field != null && field.maxLength == 32 && field.text == typed[..32],
                $"field={field != null} cap={field?.maxLength} shown={field?.text?.Length}");

            foreach(var (text, key, what) in new[] { ("", "PROFILE_STATUS_NAME_INVALID", "an empty name"),
                         (ProfileManager.Active, "PROFILE_STATUS_NAME_TAKEN", "a taken name") }) {
                var names = new List<string>();
                TypeInto("PROFILE_NAME-input", text);
                yield return Frames(1);
                yield return Recording(names, () => PressRow("PROFILE_ADD"));
                string said = ProfileStatus(), want = QuartzHost.Host.Translate(key);
                check($"{ProfilesKey} PROFILE_ADD: {what} says {key} and creates nothing", names.Count == 0 && said == want && Dirs().SequenceEqual(before),
                    $"recorded={string.Join(",", names)} status='{said}' want='{want}'");
            }

            {
                var names = new List<string>();
                TypeInto("PROFILE_NAME-input", free);
                yield return Frames(1);
                yield return Recording(names, () => PressRow("PROFILE_ADD"));
                string said = ProfileStatus();
                check($"{ProfilesKey} PROFILE_ADD: a free name reaches Create once", names.SequenceEqual(["profiles.create:" + free])
                    && field?.text == "" && said == "", $"recorded={string.Join(",", names)} field='{field?.text}' status='{said}'");
            }

            string[] list = ProfileManager.List();
            int other = Array.FindIndex(list, n => n != ProfileManager.Active) + 1;
            if(other > 0) {
                string name = list[other - 1], id = "PROFILE_DELETE#" + other;
                var first = new List<string>();
                var second = new List<string>();
                yield return Recording(first, () => PressRow(id));
                string armed = PageContent().Q<Button>(id + "-input")?.text;
                yield return Recording(second, () => PressRow(id));
                check($"{ProfilesKey} PROFILE_DELETE#n: the first click arms, only the second reaches Delete",
                    first.Count == 0 && armed == QuartzHost.Host.Translate("PROFILE_DELETE_CONFIRM") && second.SequenceEqual(["profiles.delete:" + name]),
                    $"first={string.Join(",", first)} armed='{armed}' second={string.Join(",", second)}");
                yield return Recorded(check, $"{ProfilesKey} PROFILE_SELECT#n: a click acts only through Do", () => PressRow("PROFILE_SELECT#" + other),
                    "profiles.select:" + name);
            } else info($"{ProfilesKey}: the player has one profile, so Delete and Select have no row to check");
            yield return Recorded(check, $"{ProfilesKey} PROFILE_EXPORT#n: a click acts only through Do", () => PressRow("PROFILE_EXPORT#1"),
                "profiles.export:" + list[0]);
            yield return Recorded(check, $"{ProfilesKey} PROFILE_IMPORT: a click acts only through Do", () => PressRow("PROFILE_IMPORT"), "profiles.import");
            yield return Recorded(check, $"{ProfilesKey} PROFILE_OPEN_FOLDER: a click acts only through Do", () => PressRow("PROFILE_OPEN_FOLDER"),
                "profiles.folder");
            yield return Recorded(check, $"{ProfilesKey} PROFILE_RECALIBRATE: a click acts only through Do", () => PressRow("PROFILE_RECALIBRATE"),
                "profiles.recalibrate");

            List<ProfileManager.PresetInfo> presets = ProfileManager.ListPresets();
            if(presets.Count > 0) {
                yield return Recorded(check, $"{ProfilesKey} PRESET_APPLY#n: a click acts only through Do", () => PressRow("PRESET_APPLY#1"),
                    "profiles.preset:" + presets[0].Name);
                var wrong = new List<string>();
                bool unkeyed = false;
                for(int i = 0; i < presets.Count; i++) {
                    string key = "PRESET_" + presets[i].Name.ToUpperInvariant();
                    bool keyed = MainCore.Tr.HasKeyForLanguage(key, "en-US");
                    unkeyed |= !keyed;
                    string shown = PageContent().Q(name: "PRESET_APPLY#" + (i + 1))?.Q<Label>()?.text;
                    if(shown != (keyed ? QuartzHost.Host.Translate(key) : presets[i].Name)) wrong.Add($"{presets[i].Name}='{shown}'");
                }
                if(unkeyed) check($"{ProfilesKey}: a preset with no Lang key shows its folder name", wrong.Count == 0, $"wrong={string.Join(",", wrong)}");
                else info($"{ProfilesKey}: every preset has a Lang key, so none shows its folder name");
            } else info($"{ProfilesKey}: no presets, so Apply has no row to check");

            var excluded = typeof(ProfileManager).GetField("excluded", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as HashSet<string>;
            string layout = Features.KeyViewer.Layout.KvStore.LayoutPath;
            check("profiles carry KeyViewerLayout.json", Path.GetDirectoryName(layout) == MainCore.Paths.RootPath && excluded != null
                && !excluded.Contains(Path.GetFileName(layout)), $"layout={layout} excluded={(excluded == null ? "none" : string.Join(",", excluded))}");
        } finally {
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(2);
        string[] after = Dirs();
        check($"{ProfilesKey}: the section leaves the profiles directory as it found it", after.SequenceEqual(before),
            $"before={before.Length} after={after.Length}");
    }

    // Every profile in List() order, the active one in the accent colour with neither Select nor Delete.
    private static void ProfilesList(Action<string, bool, string> check) {
        string[] names = ProfileManager.List();
        VisualElement list = PageContent().Q(name: "profiles-list");
        var wrong = new List<string>();
        for(int i = 0; i < names.Length; i++) {
            VisualElement box = list != null && i < list.childCount ? list[i] : null;
            Label label = box?.Q<Label>();
            bool active = names[i] == ProfileManager.Active, accent = label != null && Close(label.resolvedStyle.color, Theme.For(label)[Role.Accent]);
            string n = "#" + (i + 1);
            bool ok = label?.text == names[i] && Names(box, "PROFILE_EXPORT" + n) && accent == active
                && Names(box, "PROFILE_SELECT" + n) != active && Names(box, "PROFILE_DELETE" + n) != active;
            if(!ok) wrong.Add($"{names[i]}(label='{label?.text}' accent={accent})");
        }
        check($"{ProfilesKey}: the list shows every profile in order, the active one marked without Select or Delete",
            list != null && list.childCount == names.Length && wrong.Count == 0,
            $"rows={list?.childCount}/{names.Length} active={ProfileManager.Active} wrong={string.Join(",", wrong)}");
    }

    private static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    private static string ProfileStatus() => PageContent().Q(name: "profiles-status")?.Q<Label>()?.text;

    // A click with every Do recorded into names and skipped, through two main-thread pumps.
    private static IEnumerator Recording(List<string> names, Action click) {
        Func<string, bool> was = QuartzShell.Doing;
        QuartzShell.Doing = n => { names.Add(n); return true; };
        try {
            click();
            yield return Frames(3);
        } finally {
            QuartzShell.Doing = was;
        }
    }
}
#endif
