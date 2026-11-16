#if !IL2CPP
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.Game.Stats;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Builds the menu's search index ahead of the first search, a few pages per frame (Budget), outside play: Facet builds it on
/// the first query, every page at once (~150-500 ms). The work is Facet's own (Nav.EnsureIndex): each visible page built
/// detached under SearchIndex.Building, then the extra sources, stored for the host's language, so the hits are the same.
/// A search before it ends builds the index as before, and this stops. Facet 0.2's internals are reached by reflection;
/// a missing one stops it for the session and the search builds the index as before.
/// </summary>
// ponytail: once per session; a language change or a page revision leaves the next search to Facet's synchronous build.
internal static partial class QuartzShell {
    /// <summary>The search index prebuild has ended (built, or left to the first search).</summary>
    internal static bool SearchPrebuilt => SearchWarm.Done;

    private static class SearchWarm {
        internal static bool Done;
        private const double Budget = 2.0;   // ms per frame; a page always gets built, so one heavy page can go over
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static Facet.Nav nav;
        private static IDictionary built;
        private static object entry;   // Nav.Built
        private static SearchIndex index;
        private static string lang;
        private static Host host;
        private static List<NavItem> leaves;
        private static int next, frames;
        private static double total, longest;
        private static FieldInfo building;

        internal static void Step() {
            if(GameStats.InGame) return;   // never in a played frame
            try {
                if(nav == null && !Begin()) return;
                if(built.Contains(lang) || host.Language != lang) {   // a search built it first, or the language moved
                    Stop($"stopped after {next}/{leaves.Count} pages: {(built.Contains(lang) ? "a search built the index" : "the language changed")}");
                    return;
                }
                var clock = Stopwatch.StartNew();
                do Build(leaves[next++]);
                while(next < leaves.Count && clock.Elapsed.TotalMilliseconds < Budget);
                if(next == leaves.Count) Finish();
                double ms = clock.Elapsed.TotalMilliseconds;
                total += ms;
                frames++;
                if(ms > longest) longest = ms;
                if(next == leaves.Count) Stop($"{leaves.Count} pages in {total:0.0} ms over {frames} frames, longest frame {longest:0.0} ms");
            } catch(Exception e) {
                Diag.Ignore(e);
                Stop("gave up (" + e.GetType().Name + "): the first search builds the index");
            }
        }

        // The visible leaves in Facet's order, their visibility and revisions noted as Facet notes them.
        private static bool Begin() {
            Facet.Nav n = Nav;
            if(n == null) return false;
            Type navType = typeof(Facet.Nav), builtType = navType.GetNestedType("Built", Any);
            var root = (VisualElement)navType.GetField("_host", Any).GetValue(n);
            host = Host.For(root);
            lang = host.Language;
            built = (IDictionary)navType.GetField("_built", Any).GetValue(n);
            if(built.Contains(lang)) { Stop("a search built the index first"); return false; }
            building = typeof(SearchIndex).GetField("Building", Any);
            MethodInfo visible = navType.GetMethod("Visible", Any, null, [typeof(NavItem), typeof(Host)], null);
            entry = Activator.CreateInstance(builtType, true);
            index = new SearchIndex(host.Translate);
            builtType.GetField("Index").SetValue(entry, index);
            var vis = (IDictionary)builtType.GetField("Vis").GetValue(entry);
            leaves = [];
            void Walk(List<NavItem> items) {
                foreach(NavItem item in items) {
                    bool v = (bool)visible.Invoke(n, [item, host]);
                    if(item.VisibleWhen != null) vis[item] = v;
                    if(!v) continue;
                    if(item.IsBranch) Walk(item.Children);
                    else if(item.Build != null) leaves.Add(item);
                }
            }
            Walk((List<NavItem>)navType.GetField("_roots", Any).GetValue(n));
            nav = n;
            return true;
        }

        private static void Build(NavItem item) {
            if(item.Revision != null) {
                int rev = 0;
                try { rev = item.Revision(); } catch(Exception e) { Diag.Ignore(e); }   // Facet notes 0 for a throwing revision
                ((IDictionary)entry.GetType().GetField("Revs").GetValue(entry))[item] = rev;
            }
            object was = building.GetValue(null);
            building.SetValue(null, index);
            Invoke(index, "Begin", item, item.Title);
            try {
                using(Host.Use(host))
                    try { item.Build(new VisualElement()); } catch(Exception e) { host.Log("Nav.build '" + item.Title + "': " + e); }
            } finally {
                Invoke(index, "End");
                building.SetValue(null, was);   // only for this page: other builds between frames must not index into it
            }
            FieldInfo pages = entry.GetType().GetField("Pages");
            pages.SetValue(entry, (int)pages.GetValue(entry) + 1);
        }

        private static void Finish() {
            var sources = (IEnumerable)typeof(Facet.Nav).GetField("_sources", Any).GetValue(nav);
            foreach(Func<IEnumerable<SearchIndex.Entry>> src in sources)
                try { foreach(SearchIndex.Entry e in src()) Invoke(index, "Add", e); } catch(Exception e) { host.Log("Nav search source: " + e); }
            built[lang] = entry;
        }

        private static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethods(Any).First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);

        private static void Stop(string why) {
            Done = true;
            nav = null;
            built = null;
            entry = null;
            index = null;
            leaves = null;
            MainCore.Log.Msg("[perf] search index prebuilt: " + why);
        }
    }
}
#endif
