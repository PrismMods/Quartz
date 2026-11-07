#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.EffectRemover;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu's search: the box, a moved page's row, a page and a row still in the classic menu (both jump there),
/// Korean initials, and an index that follows a feature switched off.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator ShellSearch(Action<string, bool, string> check) {
        EffectRemover.EnsureConf();
        string mode = C.Mode;
        bool wasOpen = UICore.IsOpen;
        bool fxWas = FeatureService.IsEnabled(Fx), fxHad = FeatureService.State.Features.TryGetValue(Fx, out bool fxStored);
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
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
            yield return SearchClassic(check, "profiles", h => h.PageRef != null && h.Key == "PROFILES", "profiles.main", "a classic page's hit");
            // A row of the classic Settings page, until that page moves.
            string middle = QuartzHost.Host.Translate("MIDDLE_CLICK_TO_SET_AS_DEFAULT");
            yield return SearchClassic(check, "middle-click", h => h.PageRef == null && h.Label == middle, CorePages.SettingsPageKey, "a classic row's hit");
            SearchNoCardRows(check);
            yield return SearchKorean(check);
            yield return SearchFresh(check);
        } finally {
            C.Mode = mode;
            QuartzShell.Changed();
            QuartzShell.Nav?.Filter("");
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            RestoreFeature(Fx, fxWas, fxHad, fxStored);
            if(wasOpen) UICore.Open(true);
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

    // Picks a mode in Effect Remover's own dropdown (the page is open), as a click would.
    private static IEnumerator PickMode(string itemKey) {
        QuartzShell.Nav.Filter("");   // a search's results hide the page
        yield return Frames(2);
        Button b = Body.Q<Button>("FXRM_MODE-input");
        if(b == null) yield break;
        ContentScroll.ScrollTo(b);
        yield return Frames(2);
        Submit(b);
        yield return Frames(2);
        // The menu lives on Facet's popup surface, a document of its own.
        string want = QuartzHost.Host.Translate(itemKey);
        VisualElement row = null;
        foreach(UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if(d.rootVisualElement != null) row ??= d.rootVisualElement.Query<Label>().Where(l => l.text == want && l.panel != null).First()?.parent;
        if(row != null) {
            using PointerUpEvent ev = Pooled<PointerUpEvent>();
            typeof(PointerEventBase<PointerUpEvent>).GetProperty("button").SetValue(ev, 0);   // the popup takes the primary button only
            ev.target = row;
            row.SendEvent(ev);
        }
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

    // A page or a row still in the classic menu: its result jumps there, on that page, and its close brings this window back.
    private static IEnumerator SearchClassic(Action<string, bool, string> check, string q, Func<SearchIndex.Hit, bool> match, string pageKey, string what) {
        List<SearchIndex.Hit> hits = Find(q);
        int at = hits.FindIndex(h => match(h));
        check($"search finds {what} for \"{q}\"", at >= 0, Hits(hits));
        if(at < 0) yield break;
        int state = NavRegistry.StateFor(pageKey);
        Click(Body[at]);
        bool jumped = false;
        for(int i = 0; i < 3 && !jumped; i++) {
            yield return Frames(1);
            jumped = UICore.IsOpen && UICore.CurrentMenuState == state && !FacetBridge.NewMenuVisible();
        }
        Step(check, $"{what}: within 3 frames the classic menu opens on its page, the new one hidden", jumped, $"state={UICore.CurrentMenuState}/{state}");
        UICore.Close();
        yield return Frames(2);
        Step(check, $"{what}: closing the classic menu brings the new one back", jumped && FacetBridge.NewMenuVisible() && !UICore.IsOpen);
    }

    // The classic cards' own row is not indexed: "classic" finds no card, one per page before.
    private static void SearchNoCardRows(Action<string, bool, string> check) {
        List<SearchIndex.Hit> hits = Find("classic");
        check("a classic card's row is not indexed once per page", !hits.Any(h => h.Key == "MENU_CLASSIC_PAGE" || h.Key == "MENU_CLASSIC_OPEN"), Hits(hits));
    }

    // Korean initials find a moved page's row.
    private static IEnumerator SearchKorean(Action<string, bool, string> check) {
        const string key = "FXRM_ENABLE_SAVE";
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
        check("Korean initials find a moved page's row", q != Hangul.Initials(english) && hits.Any(h => h.Key == key), $"q={q} {Hits(hits)}");
    }

    // The index follows a feature switched off. On its tab the classic page's rows give way to the off
    // page, whose header has the same title (the feature's Lang keys stay loaded). The switch lands on a later pump,
    // which a busy queue can push past a fixed frame count: wait for it.
    private static IEnumerator SearchFresh(Action<string, bool, string> check) {
        const string id = "ottoicon", q = "otto icon", stubKey = "feature-off." + id;
        bool was = FeatureService.IsEnabled(id), had = FeatureService.State.Features.TryGetValue(id, out bool stored);
        bool Has(string tab, string key) => Find(q).Any(h => h.PageRef != null && h.Tab == tab && h.Key == key);
        // The classic page's own rows, which the search index takes from the classic menu.
        bool Source(string page) => Find(q).Any(h => h.PageRef == null && h.Page == page);
        try {
            if(!was) {
                FeatureService.SetEnabled(id, true);
                for(int i = 0; i < 60 && NavRegistry.ByKey(stubKey) != null; i++) yield return null;
            }
            NavPage page = NavRegistry.ByState(FeaturePages.StateFor(id));
            string tab = page == null ? null : QuartzShell.Label(page.LocaleKey, page.Title);
            bool on = Source(tab);
            if(page != null) yield return SearchClassic(check, q, h => h.PageRef != null && h.Tab == tab && h.Key == tab, page.Key, "a classic tab's hit");
            FeatureService.SetEnabled(id, false);
            int frames = 0;
            for(; frames < 60 && NavRegistry.ByKey(stubKey) == null; frames++) yield return null;
            NavPage stub = NavRegistry.ByKey(stubKey);
            string offTab = stub == null ? null : QuartzShell.Label(stub.LocaleKey, stub.Title);
            bool header = Has(offTab, offTab), gone = !Source(tab) && !Source(offTab);
            check("search follows a feature switched off: its classic rows give way to its off page's header", on && header && gone,
                $"tab={tab} offTab={offTab} rows={on} header={header} rowsGone={gone} switched after {frames} frames {Hits(Find(q))}");
        } finally {
            RestoreFeature(id, was, had, stored);
        }
        yield return Frames(3);
    }
}
#endif
