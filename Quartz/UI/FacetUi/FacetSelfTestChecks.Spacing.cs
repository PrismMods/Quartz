#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Features.Interop;
using Quartz.Features;
using Quartz.Features.Restriction;
using Quartz.UI.FacetUi.KeyViewerEditor;
using Quartz.UI.FacetUi.VisualsPages;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Spacing and look the player reported: a section head is a row like any other, a tab's first row starts under the tab rule,
/// a page's note is its wrapped description, Import's Rescan sits in its header, the judgement bar's changed dot keeps label
/// and switch still, a preset restriction shows no switches, and the key viewer's preview fills its column. Runs from Polish.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator Spacing(Action<string, bool, string> check) {
        static bool Near(float a, float b, float by = 1f) => Mathf.Abs(a - b) <= by;
        var on = new List<(string id, bool was, bool had, bool stored)>();
        foreach(string id in new[] { "uihider", "combo", "practice", "hidejudgements", "restriction" }) {
            on.Add((id, FeatureService.IsEnabled(id), FeatureService.State.Features.TryGetValue(id, out bool s), s));
            if(!FeatureService.IsEnabled(id)) FeatureService.SetEnabled(id, true);
        }
        int mode = -1;
        try {
            float rowH = Theme.For(Body).Sizes.RowHeight;

            QuartzShell.Go("visuals.uihiding");
            yield return Frames(2);
            VisualElement shortcut = PageContent().Q(name: "UIH_SHORTCUT"), play = PageContent().Q(name: "SECTION_PLAYING_PROFILE"),
                rec = PageContent().Q(name: "SECTION_RECORDING_PROFILE");
            float gap = play != null && shortcut != null ? play.worldBound.yMin - shortcut.worldBound.yMax : float.NaN;
            float hp = play?.worldBound.height ?? 0f, hr = rec?.worldBound.height ?? 0f;
            check("spacing: a section head follows the row above with no gap and is a row tall", Near(gap, 0f) && Near(hp, hr) && hp >= rowH - 1f,
                $"gap={gap:0.#} playing={hp:0.#} recording={hr:0.#} row={rowH:0.#}");

            QuartzShell.Go("overlay.combo");
            yield return Frames(2);
            // The tab row is the indicator cells' parent; the shown tab's content follows it (whichever tab was last picked).
            VisualElement bar = PageContent().Q(name: "indicator")?.parent?.parent, holder = bar?.parent;
            int at = holder?.IndexOf(bar) ?? -1;
            VisualElement content = at >= 0 && at + 1 < holder!.childCount ? holder[at + 1] : null;
            VisualElement first = content is { childCount: > 0 } ? content[0] : null;
            float under = first != null && bar != null ? first.worldBound.yMin - bar.worldBound.yMax : float.NaN;
            check("spacing: a tab's first row starts right under the tab rule, with no rule of its own",
                Near(under, 0f) && first!.resolvedStyle.borderTopWidth == 0f, $"gap={under:0.#} rule={first?.resolvedStyle.borderTopWidth}");

            QuartzShell.Go("gameplay.practice");
            yield return Frames(2);
            VisualElement header = PageContent().Q(name: "SECTION_PRACTICE_DIFFICULTY");
            Label note = header?.Q<Label>("PRACTICE_HINT");
            int notes = PageContent().Query(name: "PRACTICE_HINT").ToList().Count;
            float lines = note != null ? note.worldBound.height / Mathf.Max(1f, note.resolvedStyle.fontSize) : 0f;
            check("spacing: Practice Difficulty's note is its header's description, wrapped, and no row repeats it",
                note != null && notes == 1 && note.resolvedStyle.whiteSpace == WhiteSpace.Normal && lines > 1.5f,
                $"inHeader={note != null} named={notes} lines={lines:0.0}");

            QuartzShell.Go("import.main");
            yield return Frames(2);
            VisualElement import = PageContent().Q(name: "menu-header");
            check("spacing: Import's note is its description and Rescan sits in its header",
                import?.Q<Label>("IMPORT_HINT") != null && import.Q<Button>("IMPORT_RESCAN-input") != null,
                $"header={import != null} note={import?.Q("IMPORT_HINT") != null} rescan={import?.Q("IMPORT_RESCAN-input") != null}");

            QuartzShell.Go("visuals.hidejudgements");
            yield return Frames(2);
            JudgementTiles.Row seg = HideJudgementsPage.Bar(XPerfectBridge.Shown, XPerfectBridge.Native)[0];
            VisualElement tile = PageContent().Q(name: seg.Key + "-input")?.parent, dot = tile?.Q(name: "changed");
            Label label = tile?.Q<Label>();
            bool still = false, left = false;
            float y0 = float.NaN, y1 = float.NaN, s0 = float.NaN, s1 = float.NaN;
            if(dot != null && label != null) {
                StyleEnum<DisplayStyle> had = dot.style.display;
                VisualElement input = tile!.Q(name: seg.Key + "-input");
                dot.style.display = DisplayStyle.None;
                yield return Frames(2);
                (y0, s0) = (label.worldBound.yMin, input.worldBound.yMin);
                dot.style.display = DisplayStyle.Flex;
                yield return Frames(2);
                (y1, s1) = (label.worldBound.yMin, input.worldBound.yMin);
                left = dot.worldBound.xMax <= label.worldBound.xMin + 1f && dot.worldBound.yMin >= label.worldBound.yMin - 1f;
                still = Near(y0, y1) && Near(s0, s1);
                dot.style.display = had;
            }
            bool edgeless = tile != null && tile.Children().All(c => c.style.position.value != Position.Absolute || c.name == JudgementBar.FillName
                || c.resolvedStyle.display == DisplayStyle.None);
            check("spacing: the judgement bar's changed dot sits left of its label and moves neither label nor switch; no selection edge",
                still && left && edgeless, $"dot={dot != null} label {y0:0.#}->{y1:0.#} switch {s0:0.#}->{s1:0.#} left={left} edgeless={edgeless}");

            Restriction.EnsureConf();
            mode = Restriction.Conf.JRestrictMode;
            QuartzShell.Go("gameplay.judgement");
            yield return Frames(2);
            static DisplayStyle Shows() => PageContent().Q(name: "JR_ALLOW_TOOEARLY-input")?.resolvedStyle.display ?? DisplayStyle.None;
            Restriction.Conf.JRestrictMode = 1;
            for(float t0 = Time.realtimeSinceStartup; Shows() != DisplayStyle.None && Time.realtimeSinceStartup - t0 < 1f;) yield return null;
            DisplayStyle preset = Shows();
            Restriction.Conf.JRestrictMode = 3;
            for(float t0 = Time.realtimeSinceStartup; Shows() != DisplayStyle.Flex && Time.realtimeSinceStartup - t0 < 1f;) yield return null;
            DisplayStyle custom = Shows();
            check("restriction: a preset mode's bar shows no switches, Custom shows them",
                preset == DisplayStyle.None && custom == DisplayStyle.Flex, $"preset={preset} custom={custom}");

            QuartzShell.Go("overlay.keyviewer");
            yield return Frames(3);
            if(KvEditorDoor.Live && KvEditorPage.Editor is { } ed) {
                VisualElement column = ed.Root.parent, status = ed.Root.Q(name: "kle-status"), side = PageContent().Q(name: "kv-inspector-column");
                float slack = column.worldBound.yMax - (status?.worldBound.yMax ?? 0f);
                float pad = Theme.For(Body).Sizes.Pad;
                check("kv editor: the preview fills its column to the bottom, and the inspector column takes the cards' row inset",
                    slack <= pad + 2f && side != null && Near(side.resolvedStyle.paddingLeft, pad),
                    $"left under the status line={slack:0.#} inset={side?.resolvedStyle.paddingLeft:0.#} pad={pad:0.#}");
                check("kv editor: the grid and elements are on the stage once the inspector moves out", KvOnStage(ed, out string seen), seen);

                // Layouts is a menu title like File: its text over the first tab's, its box centred with File's, no focus outline.
                var menuBar = ed.Menu.Root;
                Button layouts = menuBar.Q<Button>("kle-layouts"), file = menuBar.childCount > 1 ? menuBar[1] as Button : null;
                VisualElement firstTab = ed.Root.Q(name: "kle-strip")?.Q(name: "kle-hands-title");
                static float TextLeft(VisualElement e) => e.worldBound.xMin + e.resolvedStyle.borderLeftWidth + e.resolvedStyle.paddingLeft;
                layouts?.Focus();
                yield return Frames(1);
                float dx = layouts != null && firstTab != null ? TextLeft(layouts) - TextLeft(firstTab) : float.NaN;
                float dy = layouts != null && file != null ? layouts.worldBound.center.y - file.worldBound.center.y : float.NaN;
                float ring = layouts?.resolvedStyle.borderTopColor.a ?? 1f;
                layouts?.Blur();
                check("kv editor: Layouts' text lines up with the first tab's, centred with File, with no box or outline",
                    Near(dx, 0f, 2f) && Near(dy, 0f) && ring < 0.01f, $"text dx={dx:0.#} centre dy={dy:0.#} border alpha={ring:0.##}");
            }

            QuartzShell.Go("overlay.combo");
            yield return Frames(2);
            QuartzShell.Go("overlay.keyviewer");
            yield return Frames(3);
            if(KvEditorDoor.Live && KvEditorPage.Editor is { } back)
                check("kv editor: the grid and elements are on the stage after a page switch", KvOnStage(back, out string seen), seen);
        } finally {
            if(mode >= 0) Restriction.Conf.JRestrictMode = mode;
            for(int i = on.Count - 1; i >= 0; i--) RestoreFeature(on[i].id, on[i].was, on[i].had, on[i].stored);
        }
        yield return Frames(2);
    }

    // The logical canvas (the grid) and at least one element box overlap the stage, each with a size.
    private static bool KvOnStage(PrismLib.UI.Toolkit.KeyLayoutEditor<Quartz.Features.KeyViewer.Layout.KvElement> ed, out string seen) {
        Rect stage = ed.Canvas.Stage.worldBound;
        VisualElement content = ed.Canvas.Content;
        bool grid = content.worldBound.width > 0f && content.worldBound.Overlaps(stage);
        int boxes = 0, on = 0;
        foreach(VisualElement v in content.Query<VisualElement>().ToList()) {
            if(v == content || v.parent == content || v.worldBound.width <= 0f) continue;   // the layers themselves are not elements
            boxes++;
            if(v.worldBound.Overlaps(stage)) on++;
        }
        seen = $"stage={stage} content={content.worldBound} boxes on stage={on}/{boxes} zoom={ed.Canvas.Zoom:0.##} pan={ed.Canvas.Pan}";
        return stage.width > 0f && stage.height > 0f && grid && on > 0;
    }
}
#endif
