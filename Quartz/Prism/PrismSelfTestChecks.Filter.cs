#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using Quartz.UI;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.Prism;
/// <summary>
/// The shared layout editor's mod filter as Quartz drives it: each opener starts the editor on its mod's tab, whichever mod
/// hosts it, and an older PrismLib leaves the filter off with one log line. The filter is read from the bar's userData, so
/// no PrismLib.UI type is named for it; PrismLib.dll types stay in the NoInlining helpers at the end.
/// </summary>
internal static partial class PrismSelfTestChecks {
    private const string FilterProbeId = "FilterProbe";
    internal static IEnumerator Filter(Action<string, bool, string> check, Action<string> info) {
        info(FilterInfo());
        if(!PrismBridge.FilterReady && PrismBridge.FilterOffReason != null) {
            // Counted in the log itself, so a second path that writes the line fails this too (a run logs far fewer
            // lines than the log tab keeps).
            int lines = Quartz.Compat.QuartzLogger.Recent().Count(l => l.Contains("Quartz's layout filter off"));
            check("filter: an older PrismLib leaves the filter off with one log line", lines == 1, $"lines={lines}");
            info($"filter: off ({PrismBridge.FilterOffReason})");
            check("filter: with the filter off the menu hotkey goes to the hub",
                !LayoutHotkey.Door && LayoutHotkey.Route() == PrismRules.HotkeyRoute.Toggle, $"door={LayoutHotkey.Door} route={LayoutHotkey.Route()}");
            PrismBridge.OpenLayoutEditor();
            yield return Frames(5);
            check("filter: the editor still opens without the filter", LayoutEditorProbe.Shown(), null);
            Ui.CloseTop();
            yield return Frames(5);
            yield break;
        }
        string holder = LayoutHost();
        bool other = holder != null && holder != PrismBridge.Id;
        if(other) yield return FilterPass(check, false);
        object held = other ? HeldBy(holder) : null;
        try {
            if(held != null) {
                ReleaseOwner(holder);
                float until = Time.unscaledTime + 1.5f;
                while(LayoutHost() != PrismBridge.Id && Time.unscaledTime < until) yield return null;
            }
            yield return FilterPass(check, true);
        } finally {
            if(held != null) check("filter: the holder gets its claims back", GiveBack(holder, held) && LayoutHost() == holder, $"holder={holder} now={LayoutHost()}");
        }
        for(int i = 0; i < 10 && LayoutEditorProbe.Shown(); i++) yield return null;
        check("filter: the editor is closed after the section", !LayoutEditorProbe.Shown(), null);
    }
    // One host's pass: Quartz's opener, a request naming a mod, a request naming none.
    private static IEnumerator FilterPass(Action<string, bool, string> check, bool quartz) {
        string host = LayoutHost();
        PrismBridge.OpenLayoutEditor();
        yield return Frames(5);
        string f = ShownFilter();
        check($"filter ({host}): Quartz's opener starts the editor on Quartz", f == PrismBridge.Id, f ?? "null");
        if(quartz) {
            OneEditorShows(check, host);
            check("filter: the probe follows the host that shows the editor", LayoutEditorProbe.Shown() == QuartzLayout.Visible,
                $"shown={LayoutEditorProbe.Shown()} quartz={QuartzLayout.Visible} documents={EditorDocuments(false)}");
        }
        List<string> tabs = FilterTabs();
        string all = Words.T("All"); // the tab's label goes through the player's language, as the editor's Done does
        check($"filter ({host}): the bar lists All and Quartz", tabs.Count > 0 && tabs[0] == all && tabs.Contains(PrismBridge.Id), $"{string.Join("|", tabs)} all={all}");
        yield return CloseEditor();
        yield return Frames(5);
        DescribeFilterProbe();
        RequestOpenFor(FilterProbeId);
        yield return Frames(5);
        f = ShownFilter();
        check($"filter ({host}): a request naming a mod starts the editor on it", f == FilterProbeId, f ?? "null");
        if(quartz) OneEditorShows(check, host);
        yield return CloseEditor();
        yield return Frames(5);
        RequestOpenUnnamed();
        yield return Frames(5);
        f = ShownFilter();
        check($"filter ({host}): a request naming no mod starts it on All", LayoutEditorProbe.Shown() && f == null, f ?? "null");
        if(quartz) OneEditorShows(check, host);
        yield return CloseEditor();
        yield return Frames(5);
        UnregisterFilterProbe();
    }
    private static void OneEditorShows(Action<string, bool, string> check, string host) {
        int shown = EditorDocuments(true);
        check($"filter ({host}): one editor shows", shown == 1, $"shown={shown} documents={EditorDocuments(false)}");
    }
    private static IEnumerator CloseEditor() {
        for(int i = 0; i < 10 && LayoutEditorProbe.Shown(); i++) {
            Ui.CloseTop();
            yield return null;
        }
    }
    private static string ShownFilter() => LayoutEditorProbe.ShownRoot()?.Q("layout-filter")?.userData as string;
    private static List<string> FilterTabs() {
        var names = new List<string>();
        VisualElement bar = LayoutEditorProbe.ShownRoot()?.Q("layout-filter");
        if(bar != null) ButtonTexts(bar, names);
        return names;
    }
    private static void ButtonTexts(VisualElement e, List<string> into) {
        if(e is Button b) into.Add(b.text);
        for(int i = 0; i < e.childCount; i++) ButtonTexts(e[i], into);
    }
    // Every editor document in the scene, or only the displayed ones: each host builds its own.
    private static int EditorDocuments(bool shownOnly) =>
        UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .Count(d => d.name == "PrismLayoutEditor" && d.rootVisualElement != null
                && (!shownOnly || d.rootVisualElement.style.display.value != DisplayStyle.None));
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string FilterInfo() =>
        $"filter: lib={LibWhere(typeof(PrismLib.Prism).Assembly)} request={(PrismBridge.RequestFor ? "yes" : "no")} ui={(PrismBridge.FilterUi ? "yes" : "no")} host={LayoutHost()}";
    // A library loaded from bytes has no path: its name and version say which copy it is.
    private static string LibWhere(System.Reflection.Assembly a) => string.IsNullOrEmpty(a.Location) ? a.FullName : a.Location;
    // A mod with one target in the middle of the screen whose moves go nowhere.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DescribeFilterProbe() =>
        PrismLib.Prism.Register(FilterProbeId, new Version(0, 0, 1)).DescribeLayout([
            new PrismLib.LayoutTarget {
                Id = "box", Label = FilterProbeId,
                GetBox = () => [Screen.width * 0.5f, Screen.height * 0.5f, 40f, 40f],
                MoveTo = (_, _) => { },
            },
        ]);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RequestOpenFor(string owner) => PrismLib.Layout.RequestOpen(owner);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RequestOpenUnnamed() => PrismLib.Layout.RequestOpen();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnregisterFilterProbe() => PrismLib.Layout.Unregister(FilterProbeId);
}
#endif
