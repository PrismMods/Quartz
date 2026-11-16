#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.EffectRemover;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The menu's search: the box, a moved page's row, a page drawn for the old menu (opens its note), Korean initials, and an index that follows a feature switched off.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator ShellSearch(Action<string, bool, string> check) {
        EffectRemover.EnsureConf();
        string mode = C.Mode;
        bool fxWas = FeatureService.IsEnabled(Fx), fxHad = FeatureService.State.Features.TryGetValue(Fx, out bool fxStored);
        try {
            if(!fxWas) FeatureService.SetEnabled(Fx, true);
            // "Allow saving" is Enhanced's alone; a mode set outside the page is no revision, so the index is told.
            C.Mode = EffectRemoverSettings.ModeEnhanced;
            QuartzShell.Changed();
            yield return Frames(3);
            FacetBridge.OpenNewMenu("profiles.main");
            yield return Frames(2);
            yield return SearchTyping(check);
            yield return SearchMovedRow(check);
            yield return SearchModeChange(check);
            // A page drawn for the old menu: the fixture page, which stands in for a custom addon page.
            yield return FacetSelfTestFixture.Register();
            yield return SearchUndrawable(check);
            yield return FacetSelfTestFixture.Unregister();
            yield return SearchSettingsRow(check);
            SearchNoCardRows(check);
            yield return SearchKorean(check, "FXRM_ENABLE_SAVE", "SECTION_EFFECT_REMOVER", "Korean initials find a moved page's row");
            yield return SearchFresh(check);
        } finally {
            FacetSelfTestFixture.Forget();
            C.Mode = mode;
            QuartzShell.Changed();
            QuartzShell.Nav?.Filter("");
            QuartzShell.Hide();
            RestoreFeature(Fx, fxWas, fxHad, fxStored);
        }
        yield return Frames(3);
    }

    private static List<SearchIndex.Hit> Find(string q) {
        QuartzShell.Nav.Filter(q);   // Index alone never rebuilds a stale index
        return QuartzShell.Nav.Index?.Find(q) ?? [];
    }

    private static string Hits(List<SearchIndex.Hit> hits) =>
        $"hits={hits.Count}: " + string.Join("|", hits.Take(8).Select(h => $"{h.Key}@{h.Page}/{h.Tab}{(h.PageRef == null ? "(entry)" : "")}"));

    // Focused, the box keeps the menu hotkey from toggling; typed into, it shows results; emptied, the page again.
    private static IEnumerator SearchTyping(Action<string, bool, string> check) {
        TextField box = QuartzShell.Window.Search;
        box.Focus();
        yield return Frames(2);
        bool focused = FacetBridge.TextFocused();
        check("the menu hotkey does not toggle while typing in the new menu's search box", focused && !UICore.ToggleHotkeyAllowed(true, true),
            $"focused={focused}");
        box.value = "profiles";
        yield return Frames(1);
        bool results = Body.Q(name: "menu-header") == null && Body.childCount > 0;
        box.value = "";
        yield return Frames(1);
        bool page = Body.Q(name: "menu-header") != null;
        box.Blur();
        yield return Frames(2);
        check("typing in the box shows results, and emptying it shows the page again", results && page, $"results={results} page={page}");
    }

    // A moved page's row: its result opens Effect Remover's tab with the row in view. Clearing a search shows the page again.
    private static IEnumerator SearchMovedRow(Action<string, bool, string> check) {
        List<SearchIndex.Hit> hits = Find("allow saving");
        int at = hits.FindIndex(h => h.Key == "FXRM_ENABLE_SAVE" && h.Tab == "SECTION_EFFECT_REMOVER");
        check("search finds a moved page's row, on its tab", at >= 0, Hits(hits));
        if(at < 0) yield break;
        Click(Body[at]);
        bool inView = false;
        for(int i = 0; i < 30 && !inView; i++) {
            yield return null;
            VisualElement row = Body.Q(name: "FXRM_ENABLE_SAVE");
            Rect vp = ContentScroll.contentViewport.worldBound, r = row?.worldBound ?? default;
            inView = row != null && r.height > 0f && r.yMin >= vp.yMin - 1f && r.yMax <= vp.yMax + 1f;
        }
        string tab = QuartzHost.Host.Translate("SECTION_EFFECT_REMOVER");
        check("its result opens Visuals on Effect Remover with the row in view",
            QuartzShell.CurrentPage == FxPage && SelectedTab() == tab && inView, $"page={QuartzShell.CurrentPage} tab={SelectedTab()} inView={inView}");
        QuartzShell.Nav.Filter("allow saving");
        bool results = Body.Q(name: "FXRM_HEADER") == null;
        QuartzShell.Nav.Filter("");
        check("clearing the search shows the page again", results && Body.Q(name: "FXRM_HEADER") != null && QuartzShell.CurrentPage == FxPage,
            $"results={results} page={QuartzShell.CurrentPage}");
    }

    // The Updates card's row on the moved Settings page: its result opens Settings with the row in view.
    private static IEnumerator SearchSettingsRow(Action<string, bool, string> check) {
        List<SearchIndex.Hit> hits = Find("check for updates");
        int at = hits.FindIndex(h => h.PageRef != null && h.Key == "CHECK_FOR_UPDATES");
        check("search finds the Updates card's row for \"check for updates\" on Settings", at >= 0, Hits(hits));
        if(at < 0) yield break;
        Click(Body[at]);
        bool inView = false;
        for(int i = 0; i < 30 && !inView; i++) {
            yield return null;
            VisualElement row = Body.Q(name: "CHECK_FOR_UPDATES") ?? Body.Q(name: "CHECK_FOR_UPDATES-input");
            Rect vp = ContentScroll.contentViewport.worldBound, r = row?.worldBound ?? default;
            inView = row != null && r.height > 0f && r.yMin >= vp.yMin - 1f && r.yMax <= vp.yMax + 1f;
        }
        check("its result opens Settings with Check for updates in view", QuartzShell.CurrentPage == CorePages.SettingsPageKey && inView,
            $"page={QuartzShell.CurrentPage} inView={inView}");
    }

    // Picks a mode in Effect Remover's own dropdown (the page is open), as a click would.
    private static IEnumerator PickMode(string itemKey) {
        QuartzShell.Nav.Filter("");   // a search's results hide the page
        yield return Frames(2);
        yield return PickItem("FXRM_MODE", QuartzHost.Host.Translate(itemKey));
    }

    // Opens the open page's dropdown row `id` and picks the item that reads `shown`, as a click would.
    private static IEnumerator PickItem(string id, string shown) {
        Button b = Body.Q<Button>(id + "-input");
        if(b == null) yield break;
        ContentScroll.ScrollTo(b);
        yield return Frames(2);
        Submit(b);
        yield return Frames(2);
        if(MenuRow(shown) is { } row) Pick(row);
        yield return Frames(3);
    }

    // A mode changed in the page itself reaches the index: "allow saving" is Enhanced's alone.
    private static IEnumerator SearchModeChange(Action<string, bool, string> check) {
        string simple = null, enhanced = null;
        bool Saves() => Find("allow saving").Any(h => h.Key == "FXRM_ENABLE_SAVE" && h.Tab == "SECTION_EFFECT_REMOVER");
        yield return PickMode("FXRM_MODE_SIMPLE");
        bool inSimple = C.IsSimple, hitSimple = Saves();
        simple = $"mode={C.Mode} hit={hitSimple}";
        yield return PickMode("FXRM_MODE_ENHANCED");
        bool inEnhanced = !C.IsSimple, hitEnhanced = Saves();
        enhanced = $"mode={C.Mode} hit={hitEnhanced}";
        QuartzShell.Nav.Filter("");
        check("search follows a mode changed in Effect Remover's page: no \"allow saving\" in Simple, one in Enhanced",
            inSimple && !hitSimple && inEnhanced && hitEnhanced, $"{simple}; {enhanced}");
    }

    // The fixture page's hit opens it in the menu, on its note that it cannot be drawn.
    private static IEnumerator SearchUndrawable(Action<string, bool, string> check) {
        string title = QuartzShell.Label("ADDON_SELF_TEST", FacetSelfTestFixture.Title);
        List<SearchIndex.Hit> hits = Find("self-test");
        int at = hits.FindIndex(h => h.PageRef != null && h.Key == title);
        check("search finds a page drawn for the old menu (fixture page)", at >= 0, Hits(hits));
        if(at < 0) yield break;
        Click(Body[at]);
        yield return Frames(2);
        Step(check, "its hit opens the page in the menu, on its note (fixture page)",
            QuartzShell.CurrentPage == FacetSelfTestFixture.PageKey && Body.Q(name: "menu-undrawable") != null && FacetBridge.NewMenuVisible());
    }

    // A moved page's tab: its result opens the page in the new menu, nothing classic.
    private static IEnumerator SearchMoved(Action<string, bool, string> check, string q, string tab, string pageKey) {
        List<SearchIndex.Hit> hits = Find(q);
        int at = hits.FindIndex(h => h.PageRef != null && h.Tab == tab && h.Key == tab);
        check($"search finds a moved tab's hit for \"{q}\"", at >= 0, Hits(hits));
        if(at < 0) yield break;
        Click(Body[at]);
        bool opened = false;
        for(int i = 0; i < 3 && !opened; i++) {
            yield return Frames(1);
            opened = QuartzShell.CurrentPage == pageKey && FacetBridge.NewMenuVisible();
        }
        Step(check, "a moved tab's hit opens its page in the new menu within 3 frames", opened,
            $"page={QuartzShell.CurrentPage}/{pageKey}");
    }

    /// <summary>A row that is only a button, with no label, is found by its button's text on its moved page.</summary>
    private static void SearchButtonRow(Action<string, bool, string> check, string pageKey, string key) {
        try {
            List<SearchIndex.Hit> hits = Find(QuartzHost.Host.Translate(key));
            check($"{pageKey}: search finds the button-only row {key} by its button's text, on this page", hits.Any(h => h.Key == key && h.PageRef != null),
                Hits(hits));
        } finally {
            QuartzShell.Nav?.Filter("");
        }
    }

    // The classic cards' own row is not indexed: "classic" finds no card, one per page before.
    private static void SearchNoCardRows(Action<string, bool, string> check) {
        List<SearchIndex.Hit> hits = Find("classic");
        check("a classic card's row is not indexed once per page", !hits.Any(h => h.Key == "MENU_CLASSIC_PAGE" || h.Key == "MENU_CLASSIC_OPEN"), Hits(hits));
    }

    // Korean initials find a moved page's row, key on its tab.
    private static IEnumerator SearchKorean(Action<string, bool, string> check, string key, string tab, string name) {
        string was = MainCore.Tr.Language, english = MainCore.Tr.GetForLanguage(key, "en-US", key), q = null;
        List<SearchIndex.Hit> hits = [];
        try {
            MainCore.Tr.Language = "ko-KR";
            for(int i = 0; i < 30 && MainCore.Tr.Get(key, english) == english; i++) yield return null;
            q = Hangul.Initials(MainCore.Tr.Get(key, english));
            hits = Find(q);
        } finally {
            MainCore.Tr.Language = was;
        }
        yield return Frames(3);   // the restored language relabels
        check(name, q != Hangul.Initials(english) && hits.Any(h => h.Key == key && h.Tab == tab && h.PageRef != null), $"q={q} {Hits(hits)}");
    }

    // The index follows a feature switched off. On its tab the moved page's rows give way to the off
    // page, whose header has the same title (the feature's Lang keys stay loaded). The switch lands on a later pump,
    // which a busy queue can push past a fixed frame count: wait for it.
    private static IEnumerator SearchFresh(Action<string, bool, string> check) {
        const string id = "ottoicon", q = "otto icon", stubKey = "feature-off." + id;
        bool was = FeatureService.IsEnabled(id), had = FeatureService.State.Features.TryGetValue(id, out bool stored);
        bool Has(string tab, string key) => Find(q).Any(h => h.PageRef != null && h.Tab == tab && h.Key == key);
        // The moved page's own rows on its tab, other than its header.
        bool Source(string tab) => Find(q).Any(h => h.PageRef != null && h.Tab == tab && h.Key != tab);
        try {
            if(!was) {
                FeatureService.SetEnabled(id, true);
                for(int i = 0; i < 60 && NavRegistry.ByKey(stubKey) != null; i++) yield return null;
            }
            NavPage page = NavRegistry.ByState(FeaturePages.StateFor(id));
            string tab = page == null ? null : QuartzShell.Label(page.LocaleKey, page.Title);
            bool on = Source(tab);
            if(page != null) yield return SearchMoved(check, q, tab, page.Key);
            FeatureService.SetEnabled(id, false);
            int frames = 0;
            for(; frames < 60 && NavRegistry.ByKey(stubKey) == null; frames++) yield return null;
            NavPage stub = NavRegistry.ByKey(stubKey);
            string offTab = stub == null ? null : QuartzShell.Label(stub.LocaleKey, stub.Title);
            bool header = Has(offTab, offTab), gone = !Source(tab) && !Source(offTab);
            check("search follows a feature switched off: its moved rows give way to its off page's header", on && header && gone,
                $"tab={tab} offTab={offTab} rows={on} header={header} rowsGone={gone} switched after {frames} frames {Hits(Find(q))}");
        } finally {
            RestoreFeature(id, was, had, stored);
        }
        yield return Frames(3);
    }
}
#endif
