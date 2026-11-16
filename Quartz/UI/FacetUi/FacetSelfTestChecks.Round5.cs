#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Features.Panels;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The fifth round of the player's notes: the search index is built before the first search and finds what Facet's own
/// build finds, slider rows keep their whole label, the stat picker's titles take the theme's text colour, and the layout
/// editor's Layouts title has the menu titles' inner padding. Runs from Polish.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator Round5(Action<string, bool, string> check) {
        // The prebuilt index against a fresh one Facet builds itself: the same hits, in the same order.
        for(float t0 = Time.realtimeSinceStartup; !QuartzShell.SearchPrebuilt && Time.realtimeSinceStartup - t0 < 10f;) yield return null;
        Facet.Nav nav = QuartzShell.Nav;
        SearchIndex pre = nav?.Index;
        string[] queries = ["note", "scale", "key", "노트", "ㄴㅌ"];
        string Hits(SearchIndex ix) => ix == null ? "" : string.Join("|", queries.SelectMany(q => ix.Find(q)).Select(h => h.Page + ">" + h.Label));
        string before = Hits(pre);
        if(nav != null) {
            ((System.Collections.IDictionary)typeof(Facet.Nav).GetField("_built", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(nav)).Clear();
            typeof(Facet.Nav).GetMethod("EnsureIndex", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(nav, null);
        }
        string after = Hits(nav?.Index);
        check("round 5: the search index is built before the first search and finds what Facet's own build finds",
            pre != null && before.Length > 0 && before == after, $"prebuilt={pre != null} hits={before.Split('|').Length} same={before == after}");

        // Slider rows: every label in the key viewer's column shows its whole text, on one line or wrapped.
        QuartzShell.Go("overlay.keyviewer");
        yield return Frames(3);
        var cut = new List<string>();
        int rows = 0;
        foreach(UnityEngine.UIElements.Slider s in PageContent().Query<UnityEngine.UIElements.Slider>().ToList()) {
            VisualElement row = s.parent?.parent;
            Label l = row?.Children().OfType<Label>().FirstOrDefault();
            if(l == null || l.resolvedStyle.display == DisplayStyle.None || l.contentRect.width <= 0f) continue;
            rows++;
            Vector2 one = l.MeasureTextSize(l.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            bool whole = one.x <= l.contentRect.width + 1f || (l.resolvedStyle.whiteSpace == WhiteSpace.Normal && l.contentRect.height >= one.y * 1.5f);
            if(!whole) cut.Add($"{l.text} ({one.x:0}>{l.contentRect.width:0})");
        }
        check("round 5: the key viewer's slider rows show their whole labels", rows > 0 && cut.Count == 0, $"rows={rows} cut={string.Join(", ", cut)}");

        // The stat picker's category titles are drawn in the text colour its stat chips use.
        QuartzShell.Go("overlay.panels");
        yield return Frames(2);
        PanelConfig panel = PanelsOverlay.Conf.Panels.FirstOrDefault();
        Popup picker = panel == null ? null : OverlayPages.PanelsStats.Picker(PageContent(), panel, 1, null);
        yield return Frames(2);
        Button head = picker?.Content?.Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("PANEL_CATEGORY_"));
        Button chip = picker?.Content?.Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("PANEL_PICK_") && b.enabledSelf);
        Color hc = head?.resolvedStyle.color ?? Color.clear, cc = chip?.resolvedStyle.color ?? Color.black;
        check("round 5: the stat picker's category titles and arrows take the theme's text colour",
            head != null && chip != null && Mathf.Abs(hc.r - cc.r) + Mathf.Abs(hc.g - cc.g) + Mathf.Abs(hc.b - cc.b) < 0.05f && hc.a > 0.9f,
            $"head={hc} chip={cc}");
        picker?.Close();

#if !QUARTZ_KEYVIEWER
        // Layouts: its lit box has the other titles' inner padding (its text over the first tab is Spacing's check).
        QuartzShell.Go("overlay.keyviewer");
        yield return Frames(3);
        if(KeyViewerEditor.KvEditorDoor.Live && KeyViewerEditor.KvEditorPage.Editor is { } ed) {
            Button layouts = ed.Menu.Root.Q<Button>("kle-layouts"), file = ed.Menu.Root.childCount > 1 ? ed.Menu.Root[1] as Button : null;
            float lp = layouts?.resolvedStyle.paddingLeft ?? -1f, fp = file?.resolvedStyle.paddingLeft ?? -2f;
            check("round 5: Layouts' box has the menu titles' inner padding", lp > 0f && Mathf.Abs(lp - fp) < 0.5f, $"layouts={lp} file={fp}");
        }
#endif
    }
}
#endif
