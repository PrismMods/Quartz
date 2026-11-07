#if !IL2CPP
using System.Globalization;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>The new menu's Features page: a filter, then a card of switch tiles for each togglable category.</summary>
internal static class ShellFeatures {
    // One tile's search text, normalised once per query or language, not on every refresh.
    private sealed class Entry {
        internal FeatureService.Handle Handle;
        internal NavCategory Category;
        internal int Seen = -1;
        internal string Haystack;
    }

    private static string query = "", normQuery = "", lang, trLang;
    private static int generation;

    /// <summary>The filter text, kept for the session.</summary>
    internal static string Query {
        get => query;
        set {
            query = value ?? "";
            lang = null;   // the next read normalises it again
        }
    }

    /// <summary>0 all, 1 on, 2 off.</summary>
    internal static int Chip { get; set; }

    internal static void Build(VisualElement p) {
        Controls.PageHeader(p, "FEATURES", null, o => {
            o.Id = "menu-header";
            o.DescriptionText = Line;
        });
        Controls.Text(p, "FEATURES_FILTER", Bind.To(() => Query, v => Query = v), o => o.Id = "menu-features-query");
        Controls.Choice(p, "", Bind.To(() => Chip, v => Chip = v), o => {
            o.Id = "menu-features-chip";
            o.Items = ["MENU_CHIP_ALL", "MENU_CHIP_ON", "MENU_CHIP_OFF"];
        });
        foreach(NavCategory c in FeatureCategories.Togglable()) {
            string key = c.Key;
            // Read again on the click: the card is rebuilt by Changed, which is what turns the label over.
            var actions = new List<KeyValuePair<string, Action>> {
                new(FeatureCategories.IsEnabled(key) ? "MENU_TAB_HIDE" : "MENU_TAB_SHOW", () => {
                    FeatureCategories.SetEnabled(key, !FeatureCategories.IsEnabled(key));
                    QuartzShell.Changed();
                }),
            };
            string title = QuartzShell.Label(c.LocaleKey, c.Title);
            List<FeatureService.Handle> features = FeatureCategories.In(key);
            if(features.Count == 0) {   // an addon's category: still hideable, as on the classic page
                Controls.Card(p, title, o => {
                    o.Id = "menu-features-card-" + key;
                    o.Actions = actions;
                    o.VisibleWhen = Unfiltered;
                });
                continue;
            }
            List<Entry> entries = [.. features.Select(h => new Entry { Handle = h, Category = c })];
            VisualElement card = Controls.Card(p, title, o => {
                o.Id = "menu-features-card-" + key;
                o.Actions = actions;
                o.NoteText = () => features.Count(h => h.Enabled) + " / " + features.Count;
                o.VisibleWhen = () => entries.Any(Matches);
            });
            VisualElement grid = Controls.Tiles(card, o => o.Columns = 5);
            foreach(Entry e in entries) {
                string id = e.Handle.Id;
                Controls.Tile(grid, QuartzShell.Label(e.Handle.Info.NameKey, e.Handle.Name), QuartzShell.FeatureSwitch(id), o => {
                    o.Id = "menu-feature-" + id;
                    o.Open = () => QuartzShell.Go(NavRegistry.KeyFor(FeaturePages.StateFor(id)));
                    o.TooltipText = () => e.Handle.Error;
                    o.VisibleWhen = () => Matches(e);
                });
            }
        }
    }

    private static string Line() => string.Format(CultureInfo.InvariantCulture,
        QuartzHost.Text("MENU_FEATURES_LINE", "{0} of {1} on · a feature that's off is fully unloaded"),
        FeatureService.Features.Count(h => h.Enabled), FeatureService.Features.Count);

    // The query's initials form follows the settings' language and the haystacks the translator's, which can land later
    // (it loads): a change of either renormalises.
    private static void Sync() {
        string now = MainCore.Conf.Language, tr = MainCore.Tr.Language;
        if(now == lang && tr == trLang) return;
        lang = now;
        trLang = tr;
        normQuery = PageFeatures.Norm(query);
        generation++;
    }

    private static bool Unfiltered() {
        Sync();
        return string.IsNullOrWhiteSpace(normQuery) && Chip == 0;
    }

    // Asked for every tile on every refresh: the haystack is built only when the query or language changed.
    private static bool Matches(Entry e) {
        Sync();
        if(Chip != 0 && (Chip == 1) != e.Handle.Enabled) return false;
        if(string.IsNullOrWhiteSpace(normQuery)) return true;
        if(e.Seen != generation) {
            // Translated now, not when the page was built: the language can change under a built page.
            e.Haystack = PageFeatures.Norm(MainCore.Tr.Get(e.Category.LocaleKey, e.Category.Title) + " "
                + MainCore.Tr.Get(e.Handle.Info.NameKey, e.Handle.Name) + " " + e.Handle.Id);
            e.Seen = generation;
        }
        return e.Haystack.Contains(normQuery);
    }
}
#endif
