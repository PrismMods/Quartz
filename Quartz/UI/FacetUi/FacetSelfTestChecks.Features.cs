#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.UI.Nav;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu's Features grid: the header counts, a card of tiles for each togglable category, a tile's switch and
/// label, the filter and its chips (Korean initials too), and the Hide/Show link in a card's header.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string FeatureTile = "menu-feature-";

    private static IEnumerator FeaturesPage(Action<string, bool, string> check, Action<string> info) {
        string query = ShellFeatures.Query, trLang = MainCore.Tr.Language, confLang = MainCore.Conf.Language;
        int chip = ShellFeatures.Chip;
        // Every id is read before any is flipped, and put back in reverse: "overlay" is the others' dependency.
        var saved = FeatureService.Features.Select(h => (id: h.Id, was: h.Enabled, had: FeatureService.State.Features.TryGetValue(h.Id, out bool v), value: v)).ToList();
        var cats = FeatureCategories.Togglable().Select(c => (key: c.Key, had: FeatureService.State.Categories.TryGetValue(c.Key, out bool v), value: v)).ToList();
        try {
            ShellFeatures.Query = "";
            ShellFeatures.Chip = 0;
            FacetBridge.OpenNewMenu(CorePages.FeaturesPageKey);
            yield return Frames(3);
            FeaturesHeader(check);
            FeaturesCards(check, info);
            yield return FeaturesEmptyCard(check);
            yield return FeaturesOverlayWrap(check);
            yield return FeaturesTile(check);
            yield return FeaturesFilter(check);
            yield return FeaturesKorean(check);
            yield return FeaturesHideShow(check);
        } finally {
            ShellFeatures.Query = query;
            ShellFeatures.Chip = chip;
            MainCore.Tr.Language = trLang;
            MainCore.Conf.Language = confLang;
            QuartzShell.Hide();
            for(int i = saved.Count - 1; i >= 0; i--) RestoreFeature(saved[i].id, saved[i].was, saved[i].had, saved[i].value);
            foreach(var (key, had, value) in cats) {
                if(FeatureCategories.IsEnabled(key) != (!had || value)) FeatureCategories.SetEnabled(key, !had || value);
                if(had) FeatureService.State.Categories[key] = value;
                else FeatureService.State.Categories.Remove(key);
            }
            FeatureService.State.Save();
            QuartzShell.Changed();
        }
        yield return Frames(3);
    }

    private static VisualElement FeatureCard(string key) => Body.Q(name: "menu-features-card-" + key);
    private static VisualElement FeatureTileOf(string id) => Body.Q(name: FeatureTile + id);
    private static string FeatureHaystack(NavCategory c, FeatureService.Handle h) => Quartz.Utility.StringUtils.ForQuery(
        MainCore.Tr.Get(c.LocaleKey, c.Title) + " " + MainCore.Tr.Get(h.Info.NameKey, h.Name) + " " + h.Id, MainCore.Conf.Language);
    private static List<NavCategory> WithFeatures() => [.. FeatureCategories.Togglable().Where(c => FeatureCategories.In(c.Key).Count > 0)];
    private static string Note(string key) => FeatureCard(key)?.Query<Label>().ToList().Select(l => l.text).FirstOrDefault(t => t.Contains(" / "));

    private static void FeaturesHeader(Action<string, bool, string> check) {
        List<Label> labels = Body.Q(name: "menu-header")?.Query<Label>().ToList() ?? [];
        string line = labels.Count > 1 ? labels[1].text : null;
        int on = FeatureService.Features.Count(h => h.Enabled), total = FeatureService.Features.Count;
        bool counts = QuartzHost.Host.Language == "en" ? line?.Contains($"{on} of {total}") == true
            : line?.Contains(on.ToString()) == true && line.Contains(total.ToString());
        check("the Features header holds the features on and the total", QuartzShell.CurrentPage == CorePages.FeaturesPageKey && counts,
            $"page={QuartzShell.CurrentPage} line={line} on={on}/{total}");
    }

    private static void FeaturesCards(Action<string, bool, string> check, Action<string> info) {
        var bad = new List<string>();
        foreach(NavCategory c in WithFeatures()) {
            List<FeatureService.Handle> list = FeatureCategories.In(c.Key);
            VisualElement card = FeatureCard(c.Key);
            int tiles = list.Count(h => FeatureTileOf(h.Id) != null);
            int siblings = list.Count > 0 ? FeatureTileOf(list[0].Id)?.parent?.childCount ?? 0 : 0;
            if(card == null || tiles != list.Count || siblings != list.Count) bad.Add($"{c.Key}: card={card != null} tiles={tiles}/{list.Count} grid={siblings}");
        }
        check("each togglable category with features has a card with one tile for each feature", WithFeatures().Count > 0 && bad.Count == 0, string.Join("; ", bad));
        var empty = FeatureCategories.Togglable().Where(c => FeatureCategories.In(c.Key).Count == 0).ToList();
        if(empty.Count == 0) { info("no togglable category is without features: the title-only card check is skipped"); return; }
        string hide = QuartzHost.Host.Translate("MENU_TAB_HIDE"), show = QuartzHost.Host.Translate("MENU_TAB_SHOW");
        var missing = empty.Where(c => {
            VisualElement card = FeatureCard(c.Key);
            return card == null || !Displayed(card) || !card.Query<Button>().ToList().Any(b => b.text == hide || b.text == show);
        }).Select(c => c.Key).ToList();
        check("a togglable category with no features has a title-only card with the Hide/Show link", missing.Count == 0, string.Join(",", missing));
    }

    // A togglable category with no features gets a card of its title and Hide/Show link only. The rig has no such category,
    // so the same card is built on a scratch window: its link must be drawn and answer a click.
    private static IEnumerator FeaturesEmptyCard(Action<string, bool, string> check) {
        Surface s = Window("QuartzSelfTestEmptyCard", out Window w);
        try {
            bool clicked = false;
            Controls.Card(w.Body, "FEATURES", o => o.Actions = [new("MENU_TAB_HIDE", () => clicked = true)]);
            yield return Frames(2);
            string hide = QuartzHost.Host.Translate("MENU_TAB_HIDE");
            Button link = w.Body.Query<Button>().Where(b => b.text == hide).First();
            bool drawn = link != null && Displayed(link) && link.layout.width > 0f && link.layout.height > 0f;
            if(link != null) Submit(link);
            check("a card with a title and a link but no rows draws the link, and it answers a click", drawn && clicked,
                $"link={link != null} drawn={drawn} size={link?.layout.size} clicked={clicked}");
        } finally {
            s.Dispose();
        }
    }

    // Five tiles to a row: the first five share a row and a width, the sixth wraps.
    private static IEnumerator FeaturesOverlayWrap(Action<string, bool, string> check) {
        yield return Frames(2);
        List<VisualElement> tiles = [.. FeatureCategories.In("overlay").Select(h => FeatureTileOf(h.Id))];
        bool laid = tiles.Count >= 6 && tiles.All(t => t != null);
        // Widths within two screen px: each edge snaps to the screen's pixel grid, which at the window's scale is not whole panel px.
        float snap = laid ? Mathf.Max(1f, 2f / PanelPx(tiles[0])) : 1f;
        bool row = laid && tiles.Take(5).All(t => Mathf.Abs(t.worldBound.y - tiles[0].worldBound.y) <= 1f && Mathf.Abs(t.worldBound.width - tiles[0].worldBound.width) <= snap);
        bool wraps = laid && tiles[5].worldBound.y > tiles[0].worldBound.y + 1f;
        check("Overlay's tiles run five to a row and the sixth wraps", row && wraps,
            string.Join(" ", tiles.Select(t => t == null ? "null" : $"{t.worldBound.y:0.#}/{t.worldBound.width:0.##}")));
    }

    private static IEnumerator FeaturesTile(Action<string, bool, string> check) {
        const string id = "ottoicon";
        FeatureService.Handle h = FeatureService.Find(id);
        string cat = h.Info.Group;
        bool was = h.Enabled;
        string want = null;
        VisualElement track = Body.Q(name: FeatureTile + id + "-input");
        if(track != null) Click(track);
        float t0 = Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup - t0 < 0.15f) {
            List<FeatureService.Handle> list = FeatureCategories.In(cat);
            want = list.Count(x => x.Enabled) + " / " + list.Count;
            if(h.Enabled != was && Note(cat) == want) break;
            yield return null;
        }
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        check("a tile's switch flips the feature and its card's note shows the new count within 150 ms",
            track != null && h.Enabled != was && Note(cat) == want, $"track={track != null} was={was} now={h.Enabled} note={Note(cat)} want={want} ms={ms:0}");
        FeatureService.SetEnabled(id, was);
        yield return Frames(3);
        VisualElement tile = FeatureTileOf(id);
        if(tile != null) Click(tile);
        yield return Frames(2);
        check("a tile's label opens the feature's page in the shell", tile != null && FeaturePages.FeatureOf(NavRegistry.ByKey(QuartzShell.CurrentPage)) == id,
            $"page={QuartzShell.CurrentPage}");
        QuartzShell.Go(CorePages.FeaturesPageKey);
        yield return Frames(2);
    }

    // Every feature's tile by display, against what the filter should show.
    private static string ShownFeatures() => string.Join(",", FeatureService.Features.Where(h => FeatureTileOf(h.Id) is { } t && Displayed(t)).Select(h => h.Id));

    private static IEnumerator FeaturesFilter(Action<string, bool, string> check) {
        string q = Quartz.Utility.StringUtils.ForQuery("key", MainCore.Conf.Language);
        ShellFeatures.Query = "key";
        float t0 = Time.realtimeSinceStartup;
        bool Settled() => FeatureTileOf("keyviewer") is { } t && Displayed(t) && FeatureService.Features.Where(h => FeatureTileOf(h.Id) is { } e && !Displayed(e)).Any();
        while(!Settled() && Time.realtimeSinceStartup - t0 < 0.15f) yield return null;
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        var wrong = new List<string>();
        foreach(NavCategory c in WithFeatures()) {
            bool any = false;
            foreach(FeatureService.Handle h in FeatureCategories.In(c.Key)) {
                bool match = FeatureHaystack(c, h).Contains(q), shown = FeatureTileOf(h.Id) is { } t && Displayed(t);
                any |= match;
                if(match != shown) wrong.Add(h.Id + (shown ? " shown" : " hidden"));
            }
            if(FeatureCard(c.Key) is { } card && Displayed(card) != any) wrong.Add("card " + c.Key + (any ? " hidden" : " shown"));
        }
        bool both = new[] { "keyviewer", "keylimiter" }.All(id => FeatureTileOf(id) is { } t && Displayed(t));
        check("\"key\" shows the tiles whose text holds it, Key Viewer and Key Limiter among them, and hides a card with none, within 150 ms",
            both && wrong.Count == 0 && ShownFeatures().Length > 0, $"ms={ms:0} both={both} wrong={string.Join(",", wrong)} shown={ShownFeatures()}");
        ShellFeatures.Query = "";
        ShellFeatures.Chip = 2;
        string off = string.Join(",", FeatureService.Features.Where(h => !h.Enabled).Select(h => h.Id));
        t0 = Time.realtimeSinceStartup;
        while(ShownFeatures() != off && Time.realtimeSinceStartup - t0 < 0.15f) yield return null;   // the tiles follow on Facet's refresh
        check("the Off chip shows only the disabled tiles", ShownFeatures() == off, $"shown={ShownFeatures()} off={off}");
        ShellFeatures.Chip = 0;
        yield return Frames(3);
    }

    // The filter turns on initials from the settings' language, and the labels from the translator's. The translator's lands
    // last, as when it loads after the setting changed: tiles already matched in English must match again in Korean.
    private static IEnumerator FeaturesKorean(Action<string, bool, string> check) {
        const string key = "SECTION_KEY_LIMITER";
        string english = MainCore.Tr.GetForLanguage(key, "en-US", key), q = null;
        bool shown = false;
        float ms = 0;
        try {
            MainCore.Conf.Language = "ko-KR";
            q = Hangul.Initials(MainCore.Tr.GetForLanguage(key, "ko-KR", english));
            ShellFeatures.Query = q;
            yield return Frames(3);   // a refresh matches the tiles against the translator's English
            MainCore.Tr.Language = "ko-KR";
            for(int i = 0; i < 30 && MainCore.Tr.Get(key, english) == english; i++) yield return null;
            float t0 = Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup - t0 < 0.15f && !(FeatureTileOf("keylimiter") is { } t1 && Displayed(t1))) yield return null;
            ms = (Time.realtimeSinceStartup - t0) * 1000f;
            shown = FeatureTileOf("keylimiter") is { } t && Displayed(t);
        } finally {
            ShellFeatures.Query = "";
        }
        check("Korean initials show the Key Limiter tile, with the translator's language landing after the setting's",
            q != Hangul.Initials(english) && shown, $"q={q} ms={ms:0} shown={shown}");
    }

    private static IEnumerator FeaturesHideShow(Action<string, bool, string> check) {
        const string key = "tweaks";
        NavCategory c = NavRegistry.AllCategories.First(x => x.Key == key);
        string railText = QuartzHost.Host.Translate(QuartzShell.Label(c.LocaleKey, c.Title));
        Button Link(string k) => FeatureCard(key)?.Query<Button>().ToList().FirstOrDefault(b => b.text == QuartzHost.Host.Translate(k));
        bool Rail() => RailLabels().Any(l => l?.text == railText);
        bool before = Rail();
        Button hide = Link("MENU_TAB_HIDE");
        if(hide != null) Submit(hide);
        int frames = 0;
        for(; frames < 3 && Rail(); frames++) yield return null;
        bool gone = !Rail();
        yield return Frames(2);
        Button show = Link("MENU_TAB_SHOW");   // the card was rebuilt, so the link reads the new state
        check("\"Hide from sidebar\" on Tweaks takes it off the rail within 3 frames, and the link turns to \"Show in sidebar\"",
            before && hide != null && gone && show != null && Link("MENU_TAB_HIDE") == null, $"before={before} hide={hide != null} gone={gone} frames={frames} show={show != null}");
        if(show != null) Submit(show);
        yield return Frames(3);
        check("\"Show in sidebar\" brings Tweaks back, and the link turns back", Rail() && Link("MENU_TAB_HIDE") != null, $"rail={Rail()} hide={Link("MENU_TAB_HIDE") != null}");
    }
}
#endif
