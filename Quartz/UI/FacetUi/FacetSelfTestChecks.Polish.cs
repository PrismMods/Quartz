#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Core;
using Quartz.Features;
using Quartz.Features.Restriction;
using Quartz.UI.Nav;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu's first look: its size at the classic default scale, the rail's icons, one right edge for a card's
/// controls, every moved page's parts on the cards' edges, and switches that keep still when their page rebuilds.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static readonly string[] EdgePages = [CorePages.HomePageKey, CorePages.FeaturesPageKey, CorePages.SettingsPageKey, FxPage];

    private static IEnumerator Polish(Action<string, bool, string> check, Action<string> info) {
        float scale = MainCore.Conf.UIScale, speed = Anim.Speed;
        string query = ShellFeatures.Query;
        bool fxWas = FeatureService.IsEnabled(Fx), fxHad = FeatureService.State.Features.TryGetValue(Fx, out bool fxStored);
        try {
            MainCore.Conf.UIScale = 0.85f;   // the classic default; the open below applies it
            FacetBridge.OpenNewMenu(CorePages.HomePageKey);
            yield return Frames(2);
            check("at the classic default UI scale, 0.85, the new menu shows at 1.2x", Mathf.Abs(QuartzShell.Window.Scale - 1.2f) <= 0.001f,
                $"window={QuartzShell.Window.Scale}");
            VisualElement last = PageContent().Children().LastOrDefault(e => Ink(e) != null);
            info($"Home at the default scale: its last part ends at {last?.worldBound.yMax:0} on a viewport ending at {ContentScroll.contentViewport.worldBound.yMax:0}");
            RailIcons(check);
            yield return OsCursor(check, info, QuartzShell.Window.Search, "the search field");
            if(!fxWas) FeatureService.SetEnabled(Fx, true);   // its page, not the feature-off stub
            foreach(string key in EdgePages) {
                QuartzShell.Go(key);
                yield return Frames(2);
                string misfits = EdgeMisfits(PageContent());
                check($"{key}: every part of the page draws on the cards' left and right edges (±1)", QuartzShell.CurrentPage == key && misfits == "",
                    $"page={QuartzShell.CurrentPage} {misfits}");
                if(key == CorePages.SettingsPageKey) ControlEdges(check);
            }
            yield return FilterField(check);
            yield return SwitchesStill(check);
            yield return Insets(check);
            yield return Spacing(check);
            yield return Round3(check);
            yield return Round4(check);
        } finally {
            ShellFeatures.Query = query;
            MainCore.Conf.UIScale = scale;
            Anim.Speed = speed;
            QuartzShell.ApplyLook();
            RestoreFeature(Fx, fxWas, fxHad, fxStored);
            QuartzShell.Hide();
        }
        yield return Frames(3);
    }

    // What the open page was built into: its tab's content in a category with tabs, else the content pane.
    private static VisualElement PageContent() => QuartzShell.TabRow?.Content ?? Body;

    // The rail's entries, each drawn with its category's icon wherever that category has one.
    private static void RailIcons(Action<string, bool, string> check) {
        var bad = new List<string>();
        int drawn = 0;
        foreach(NavCategory c in NavRegistry.AllCategories) {
            if(ShellModel.RailGroup(c.Key) < 0 || !c.IsVisible || NavRegistry.PagesIn(c.Key).Count == 0 || QuartzShell.CategoryIcon(c) == null) continue;
            VisualElement icon = RailScroll.contentContainer.Q(name: QuartzShell.Label(c.LocaleKey, c.Title) + "-icon");
            if(icon != null && Displayed(icon) && icon.worldBound.width > 0f && icon.worldBound.height > 0f) drawn++;
            else bad.Add(c.Key + (icon == null ? " none" : $" {icon.worldBound.size}"));
        }
        check("every rail entry whose category has an icon draws it", drawn > 0 && bad.Count == 0, $"drawn={drawn} missing={string.Join(",", bad)}");
    }

    // Over a text field the new menu shows the OS's own text cursor, as the classic menu showed the OS's resize cursors, and
    // the OS arrow once the pointer leaves; Facet's drawn cursor stays unset throughout. On macOS the app's current NSCursor
    // is read back; elsewhere only the drawn cursor is. Where the OS has no cursors to show, Facet draws its own: set over
    // the field, cleared after.
    private static IEnumerator OsCursor(Action<string, bool, string> check, Action<string> info, VisualElement field, string what) {
        FieldInfo drawn = typeof(Cursors).GetField("_drawn", BindingFlags.NonPublic | BindingFlags.Static);
        bool mac = Application.platform == RuntimePlatform.OSXPlayer, native = NativeCursor.Native;
        Hover(field);
        yield return Frames(2);
        bool overDrawn = (bool)drawn.GetValue(null), overBeam = !mac || !native || NativeCursor.MacShowing("IBeamCursor");
        Leave(field);
        yield return Frames(2);
        bool leftDrawn = (bool)drawn.GetValue(null), leftArrow = !mac || !native || NativeCursor.MacShowing("arrowCursor");
        string detail = $"field={field != null} mac={mac} native={native} over: drawn={overDrawn} ibeam={overBeam}; left: drawn={leftDrawn} arrow={leftArrow}";
        if(native) {
            check($"over {what} the OS's text cursor shows, not a drawn one, and the OS arrow after it leaves",
                field != null && !overDrawn && overBeam && !leftDrawn && leftArrow, detail);
            yield break;
        }
        info($"no OS cursors on {Application.platform}: Facet draws its own over {what}");
        check($"over {what} Facet's drawn text cursor shows, and is cleared after it leaves", field != null && overDrawn && !leftDrawn, detail);
    }

    // The Settings page's menu card holds the hotkey, its modifier and two switches: every control ends on one right edge.
    private static void ControlEdges(Action<string, bool, string> check) {
        List<VisualElement> inputs = SettingsCard("PRISM_GROUP_MENU")?.Query<VisualElement>().ToList()
            .Where(e => e.name != null && e.name.EndsWith("-input") && Displayed(e)).ToList() ?? [];
        float lo = inputs.Count == 0 ? 0f : inputs.Min(e => e.worldBound.xMax), hi = inputs.Count == 0 ? 0f : inputs.Max(e => e.worldBound.xMax);
        check("the Settings page's menu card ends every control on one right edge (±1)", inputs.Count >= 4 && hi - lo <= 1f,
            string.Join(", ", inputs.Select(e => $"{e.name} {e.worldBound.xMax:0.0}")));
    }

    // A box with a fill or a border, or a text, is drawn whole.
    private static bool Paints(VisualElement e) {
        IResolvedStyle s = e.resolvedStyle;
        return s.backgroundColor.a > 0.01f || (s.borderLeftWidth > 0f && s.borderLeftColor.a > 0.01f)
            || (s.borderRightWidth > 0f && s.borderRightColor.a > 0.01f) || e is TextElement { text.Length: > 0 };
    }

    // What a part draws, left to right: itself when it paints, else what its shown children draw. Null when nothing.
    private static (float min, float max)? Ink(VisualElement e) {
        if(e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden) return null;
        if(Paints(e)) return (e.worldBound.xMin, e.worldBound.xMax);
        (float min, float max)? ink = null;
        for(int i = 0; i < e.hierarchy.childCount; i++)
            if(Ink(e.hierarchy[i]) is { } c) ink = ink is { } a ? (Mathf.Min(a.min, c.min), Mathf.Max(a.max, c.max)) : c;
        return ink;
    }

    // Where a page's parts leave its edges, "" when none. One column: every part the page holds draws from the first card's left
    // edge to its right edge. Two (left and right from Controls.Columns): the page's parts share the first part's edges, the
    // left column's cards share their own, and the right column ends on the first part's right edge. The page header's ink
    // ends on the cards' control edge instead, a card border and Pad in, so its switch stands over the rows' switches.
    private static string EdgeMisfits(VisualElement page, VisualElement left = null, VisualElement right = null) {
        var bad = new List<string>();
        (float min, float max)? Off(VisualElement holder, bool byCard, string where) {
            var parts = holder.Children().Select(e => (e, ink: Ink(e) ?? (float.NaN, float.NaN))).Where(p => !float.IsNaN(p.ink.Item1)).ToList();
            if(parts.Count == 0) return null;
            var want = parts[byCard ? Math.Max(0, parts.FindIndex(p => Paints(p.e))) : 0].ink;   // the first card, else the first part
            for(int i = 0; i < parts.Count; i++) {
                var got = parts[i].ink;
                float max = want.Item2 - (i > 0 || byCard ? HeaderInset(parts[i].e) : 0f);
                if(Mathf.Abs(got.Item1 - want.Item1) > 1f || Mathf.Abs(got.Item2 - max) > 1f)
                    bad.Add($"{where}{i} {parts[i].e.name} {got.Item1:0.0}..{got.Item2:0.0} vs {want.Item1:0.0}..{max:0.0}");
            }
            return want;
        }
        (float min, float max)? edges = Off(page, left == null, "");
        if(edges == null) return "nothing drawn";
        if(left != null) Off(left, true, "left ");
        if(right != null && Ink(right) is { } r && Mathf.Abs(r.max - edges.Value.max) > 1f) bad.Add($"right column ends {r.max:0.0} vs {edges.Value.max:0.0}");
        return string.Join("; ", bad);
    }

    // A page header (its first label is the bold title) ends Line + Pad in from the card edge, in world px; anything else 0.
    private static float HeaderInset(VisualElement e) {
        Sizes sz = QuartzHost.Host.Theme.Sizes;
        Label title = e.Q<Label>();
        bool header = title != null && title.resolvedStyle.unityFontStyleAndWeight == FontStyle.Bold && Mathf.Abs(title.resolvedStyle.fontSize - sz.Title) < 0.01f;
        return header && e.layout.width > 0f ? (sz.Line + sz.Pad) * e.worldBound.width / e.layout.width : 0f;
    }

    // Text typed in the Features toolbar's field filters the tiles: the page reads the field on its next refresh, even after the
    // search index built the page off screen.
    private static IEnumerator FilterField(Action<string, bool, string> check) {
        QuartzShell.Go(CorePages.FeaturesPageKey);
        yield return Frames(2);
        static bool Shown(string id) => FeatureTileOf(id) is { } t && Displayed(t);
        using(Host.Use(QuartzHost.Host)) ShellFeatures.Build(new VisualElement());   // a throwaway page, as the search index builds one
        TextField f = Body.Q<TextField>("menu-features-query");
        if(f != null) f.value = "key";
        float t0 = Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup - t0 < 0.15f && Shown("ottoicon")) yield return null;
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        bool filtered = ShellFeatures.Query == "key" && Shown("keyviewer") && !Shown("ottoicon");
        if(f != null) f.value = "";
        t0 = Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup - t0 < 0.15f && !Shown("ottoicon")) yield return null;
        check("\"key\" typed in the Features toolbar's field filters the tiles within 150 ms, after an off-screen build of the page too, and clearing it shows them again",
            f != null && filtered && ShellFeatures.Query == "" && Shown("ottoicon"), $"field={f != null} ms={ms:0} filtered={filtered} query='{ShellFeatures.Query}'");
    }

    // A tile's switch flips its feature, and the Features page rebuilds: every new switch is drawn at its value on the first
    // frame after, not sliding in from off.
    private static IEnumerator SwitchesStill(Action<string, bool, string> check) {
        const string id = "ottoicon";
        bool was = FeatureService.IsEnabled(id), had = FeatureService.State.Features.TryGetValue(id, out bool stored);
        try {
            Anim.Speed = 1f;   // with motion off nothing could slide; Polish puts the speed back
            QuartzShell.Go(CorePages.FeaturesPageKey);
            yield return Frames(2);
            VisualElement track = Body.Q(name: FeatureTile + id + "-input");
            if(track != null) Click(track);
            int frames = 0;
            for(; frames < 10 && ReferenceEquals(Body.Q(name: FeatureTile + id + "-input"), track); frames++) yield return null;
            bool rebuilt = track != null && FeatureService.IsEnabled(id) != was && !ReferenceEquals(Body.Q(name: FeatureTile + id + "-input"), track);
            yield return Frames(1);
            var off = new List<string>();
            int seen = 0;
            foreach(FeatureService.Handle h in FeatureService.Features) {
                VisualElement t = Body.Q(name: FeatureTile + h.Id + "-input");
                if(t == null || !Displayed(t) || t.hierarchy.childCount == 0) continue;
                seen++;
                VisualElement knob = t.hierarchy[0];
                // The knob rests 2 in from the track's outer edges. resolvedStyle.left is its laid-out x, which counts from the
                // outer edge, border included (style.left counts from inside it), on whole screen pixels.
                float want = h.Enabled ? t.resolvedStyle.width - knob.resolvedStyle.width - 2f : 2f;
                if(Mathf.Abs(knob.resolvedStyle.left - want) >= 1f) off.Add($"{h.Id} {knob.resolvedStyle.left:0.#} vs {want:0.#}");
            }
            check("a tile's switch rebuilds the Features page, and every switch on it is at its value on the first frame after",
                rebuilt && seen > 0 && off.Count == 0, $"rebuilt={rebuilt} frames={frames} switches={seen} moving={string.Join(",", off)}");
        } finally {
            RestoreFeature(id, was, had, stored);
        }
        yield return Frames(3);
    }

    // Where a label's text starts, in world px: its content box, after its own padding.
    private static float TextX(VisualElement l) => l.LocalToWorld(l.contentRect).xMin;

    // A card header and a section's rows take the card rows' text inset (Facet starts both elsewhere; ShellRows lines them up).
    private static IEnumerator Insets(Action<string, bool, string> check) {
        const string restriction = "restriction";
        bool was = FeatureService.IsEnabled(restriction), had = FeatureService.State.Features.TryGetValue(restriction, out bool stored);
        try {
            if(!was) FeatureService.SetEnabled(restriction, true);
            Restriction.EnsureConf();
            QuartzShell.Go("gameplay.judgement");
            yield return Frames(2);
            Label head = PageContent().Q<Label>("JR_SECTIONS"), row = PageContent().Q(name: "JR_SECTIONS_ON")?.Q<Label>();
            float hx = head != null ? TextX(head) : float.NaN, rx = row != null ? TextX(row) : float.NaN;
            check("polish: a card header starts where its rows' labels start", Mathf.Abs(hx - rx) <= 1f, $"header={hx:0.#} row={rx:0.#}");
        } finally {
            RestoreFeature(restriction, was, had, stored);
        }

        QuartzShell.Go(FxPage);   // Polish turned the feature on
        yield return Frames(2);
        VisualElement orbit = PageContent().Q(name: "FXRM_PLANET_ORBIT"), body = orbit;
        object fold = null;
        if(Folds(out string miss))
            for(; body != null && (fold = findFold.Invoke(null, [body])) == null; body = body.parent) { }
        Action<bool, bool> set = fold != null ? (Action<bool, bool>)foldSet.GetValue(fold) : null;
        bool open = fold != null && ((Func<bool>)foldIsOpen.GetValue(fold))();
        set?.Invoke(true, false);
        yield return Frames(1);
        // The section's header row is built just before its body; its labels are the arrow, then the title.
        int at = body?.parent?.IndexOf(body) ?? -1;
        Label title = at > 0 ? body!.parent[at - 1].Query<Label>().AtIndex(1) : null, label = orbit?.Q<Label>();
        float tx = title != null ? TextX(title) : float.NaN, lx = label != null ? TextX(label) : float.NaN;
        if(!open) set?.Invoke(false, false);
        check("polish: a section's rows start under its title", Mathf.Abs(tx - lx) <= 1f,
            $"title='{title?.text}' {tx:0.#} row={lx:0.#} fold={fold != null} {miss}");
    }
}
#endif
