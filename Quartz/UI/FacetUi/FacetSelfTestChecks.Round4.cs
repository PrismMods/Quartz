#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Features.Panels;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The fourth round of the player's look notes: the stat picker's titles are translated and inset, a card's tab rail starts
/// where its rows do, a row's description is an info icon whose card shows the whole text, TUF's sort row is inset like a
/// row, and Import's Prism suite tag is green. Runs from Polish.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator Round4(Action<string, bool, string> check) {
        // The stat picker: no raw Lang key in a category title, and the card's inset around its body.
        QuartzShell.Go("overlay.panels");
        yield return Frames(2);
        PanelConfig panel = PanelsOverlay.Conf.Panels.FirstOrDefault();
        Popup picker = panel == null ? null : OverlayPages.PanelsStats.Picker(PageContent(), panel, 1, null);
        yield return Frames(2);
        VisualElement content = picker?.Content;
        List<string> heads = content == null ? [] : [.. content.Query<Button>().ToList().Where(b => b.name.StartsWith("PANEL_CATEGORY_")).Select(b => b.text)];
        float pad = content == null ? 0f : Theme.For(content).Sizes.Pad;
        check("round 4: the stat picker's category titles are translated text, not Lang keys, inside the card's inset",
            heads.Count > 0 && heads.All(t => !t.Contains("PANEL_CATEGORY")) && Mathf.Abs(content!.resolvedStyle.paddingLeft - pad) < 0.5f,
            $"heads={string.Join("|", heads)} padLeft={content?.resolvedStyle.paddingLeft} pad={pad}");
        picker?.Close();

        // A card's tab rail: its first title's text starts where the first row's label does (Combo's card tabs).
        QuartzShell.Go("overlay.combo");
        yield return Frames(2);
        global::Facet.Tabs tabs = PageTabs();
        Button tab = tabs?.Element.Q<Button>();
        Label rowLabel = tabs?.Content?.Query<Label>().ToList().FirstOrDefault(l => l.resolvedStyle.display == DisplayStyle.Flex && l.text.Length > 0);
        float tabText = tab == null ? 0f : tab.worldBound.xMin + tab.resolvedStyle.paddingLeft + tab.resolvedStyle.borderLeftWidth;
        check("round 4: a card's tab rail starts its first title where the rows' labels start",
            tab != null && rowLabel != null && Mathf.Abs(tabText - rowLabel.worldBound.xMin) <= 2f,
            $"tabText={tabText} label={rowLabel?.worldBound.xMin} ({rowLabel?.text})");

        // A row's description: no line under the row, an info icon after its label, and a card with the whole text on hover.
        QuartzShell.Go("gameplay.keylimiter");
        yield return Frames(2);
        VisualElement row = PageContent().Q(name: "CHARTKEYLIMITER_ENABLE"), icon = row?.Q(name: "KL_CHART_NOTE");
        string full = QuartzHost.Translate("KL_CHART_NOTE");
        bool noLine = !PageContent().Query<Label>().ToList().Any(l => l.text == full);
        Label title = row?.Query<Label>().ToList().FirstOrDefault();
        bool after = icon != null && title != null && icon.worldBound.xMin >= title.worldBound.xMax - 1f && icon.worldBound.xMin - title.worldBound.xMax < 16f;
        VisualElement card = icon == null ? null : ShellRows.ShowInfo(icon, "KL_CHART_NOTE");
        yield return Frames(2);
        Label text = card?.Q<Label>("row-info-text");
        Rect win = WindowOf(icon);
        bool inside = card != null && card.worldBound.xMin >= win.xMin - 1f && card.worldBound.xMax <= win.xMax + 1f
            && card.worldBound.yMin >= win.yMin - 1f && card.worldBound.yMax <= win.yMax + 1f;
        bool raised = card?.parent != null && card.parent.IndexOf(card) == card.parent.childCount - 1;
        check("round 4: Chart Key Limiter's description is an info icon after its label whose card shows the whole text, wrapped, in the window, on top",
            noLine && after && text != null && text.text == full && text.resolvedStyle.whiteSpace == WhiteSpace.Normal
            && card!.resolvedStyle.maxWidth.value <= 361f && inside && raised,
            $"noLine={noLine} after={after} text={text?.text?.Length}/{full.Length} wrap={text?.resolvedStyle.whiteSpace} max={card?.resolvedStyle.maxWidth} "
            + $"card={card?.worldBound} win={win} raised={raised}");
        ShellRows.HideInfo(icon);
        icon?.Focus();
        yield return Frames(1);
        bool focused = PageContent().panel?.visualTree.Q(name: "row-info-card") != null;
        icon?.Blur();
        yield return Frames(1);
        bool gone = PageContent().panel?.visualTree.Q(name: "row-info-card") == null;
        check("round 4: keyboard focus on an info icon shows its card and leaving drops it", icon != null && focused && gone, $"focused={focused} gone={gone}");

        // TUF's sort row: the rows' Pad at both ends.
        QuartzShell.Go(TufLevelsKey);
        yield return Frames(2);
        VisualElement sort = PageContent().Q(name: "tuf-sort");
        float rowPad = sort == null ? 0f : Theme.For(sort).Sizes.Pad;
        check("round 4: TUF Levels' sort and Refresh row is inset like a card row at both ends",
            sort != null && Mathf.Abs(sort.resolvedStyle.paddingLeft - rowPad) < 0.5f && Mathf.Abs(sort.resolvedStyle.paddingRight - rowPad) < 0.5f,
            $"pad={sort?.resolvedStyle.paddingLeft}/{sort?.resolvedStyle.paddingRight} want={rowPad}");

        // Import's Prism suite tag is Quartz's ok green.
        QuartzShell.Go("import.main");
        yield return Frames(3);
        List<Label> suite = [.. PageContent().Query<Label>().ToList().Where(l => l.name.StartsWith("IMPORT_PRISM_SUITE#"))];
        check("round 4: Import's Prism suite tags are green", suite.All(l => l.resolvedStyle.color == UIColors.ObjectActiveMathOk),
            $"tags={suite.Count} colours={string.Join(",", suite.Select(l => l.resolvedStyle.color))}");
    }

    // The window an element sits in: its ancestor right under the panel's root.
    private static Rect WindowOf(VisualElement e) {
        if(e?.panel == null) return default;
        VisualElement top = e.panel.visualTree;
        while(e.parent != null && e.parent != top) e = e.parent;
        return e.worldBound;
    }
}
#endif
