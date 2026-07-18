using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    /// <summary>A floor, not a size: the split takes every pixel the chrome around it does not.</summary>
    private const float EditorCanvasMinHeight = 200f;
    private const float ThinRowHeight = 26f;
    private const float RegionSpacing = 8f;
    /// <summary>Wide enough to hit without hunting for it; the visible hairline inside is 2px.</summary>
    private const float DividerWidth = 10f;
    /// <summary>The rects the inspector rebuilds into, handed back as one so the panel's internals
    /// stay this file's business.</summary>
    private readonly struct EditorPanel(
        RectTransform tabs, RectTransform props, RectTransform settings, UIScrollController scroll
    ) {
        internal RectTransform Tabs { get; } = tabs;
        internal RectTransform Props { get; } = props;
        internal RectTransform Settings { get; } = settings;
        internal UIScrollController Scroll { get; } = scroll;
    }
    /// <summary>
    /// The canvas's half of the split and the inspector's, plus the divider between them — which
    /// the page needs back only to drop its scroll capture at teardown.
    /// </summary>
    private readonly struct EditorSplit(
        RectTransform canvasHost, RectTransform paneHost, RectTransform divider
    ) {
        internal RectTransform CanvasHost { get; } = canvasHost;
        internal RectTransform PaneHost { get; } = paneHost;
        internal RectTransform Divider { get; } = divider;
    }
    /// <summary>
    /// The card the toolbar, canvas and inspector share, so the three read as one editor rather
    /// than as three unrelated stretches of page — and, in this mode, as the page itself:
    /// <see cref="KvFillHeight"/> sizes it to the scroll viewport.
    /// </summary>
    private static RectTransform EditorRegion(RectTransform body, UIScrollController pageScroll) {
        GameObject obj = new("LayoutEditor");
        obj.transform.SetParent(body, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        GameObject cardObj = new("Card");
        cardObj.transform.SetParent(rect, false);
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        // Image is itself an ILayoutElement and would compete with the column's own preferred
        // height at equal priority, so the backdrop is an ignored child rather than a component
        // on the column: ignoreLayout drops it from rectChildren and frees its anchors to fill.
        cardObj.AddComponent<LayoutElement>().ignoreLayout = true;
        Image card = cardObj.AddComponent<Image>();
        card.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        card.type = Image.Type.Sliced;
        // Between the page behind it and the canvas well inside it, so both stay legible.
        card.color = Color.Lerp(UIColors.PanelBG, UIColors.ObjectBG, 0.45f);
        card.raycastTarget = false;
        VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RegionSpacing;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        // No ContentSizeFitter, unlike the other columns on this page: the region is sized to the
        // page rather than to its rows, and a fitter would overwrite that with the sum of them —
        // collapsing the canvas back onto its minimum however much room the page had.
        //
        // minHeight is left unset on purpose. The group already reports one, KvFillHeight reads it
        // as the floor to stop shrinking at, and a value here would outrank it.
        obj.AddComponent<LayoutElement>();
        if(pageScroll != null) {
            KvFillHeight fill = obj.AddComponent<KvFillHeight>();
            fill.Content = pageScroll.content;
            fill.Viewport = pageScroll.viewport;
        }
        return rect;
    }
    /// <summary>
    /// The editor's middle band: the canvas, a draggable divider, and the property pane on the
    /// right of it — where DM Note puts its own, and where a property panel reads as belonging to
    /// the thing it describes rather than as a second page under it.
    ///
    /// The pane's width is the split's business, not this method's; see <see cref="KvSplit"/>.
    /// </summary>
    private static EditorSplit EditorSplitRow(RectTransform editor) {
        GameObject obj = new("Split");
        obj.transform.SetParent(editor, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = EditorCanvasMinHeight;
        le.preferredHeight = EditorCanvasMinHeight;
        // The band is the only thing in the region that grows, so the whole of a page taller than
        // the chrome needs lands here rather than being shared with a panel that would only pad
        // itself with blank rows.
        le.flexibleHeight = 1f;
        RectTransform canvasHost = Fill(rect, "CanvasHost");
        RectTransform pane = new GameObject("InspectorPane").AddComponent<RectTransform>();
        pane.SetParent(rect, false);
        pane.anchorMin = new Vector2(1f, 0f);
        pane.anchorMax = new Vector2(1f, 1f);
        pane.pivot = new Vector2(1f, 0.5f);
        pane.anchoredPosition = Vector2.zero;
        pane.sizeDelta = new Vector2(KvWidgets.DefaultPaneWidth, 0f);
        RectTransform divider = new GameObject("Divider").AddComponent<RectTransform>();
        divider.SetParent(rect, false);
        divider.anchorMin = new Vector2(1f, 0f);
        divider.anchorMax = new Vector2(1f, 1f);
        divider.pivot = new Vector2(1f, 0.5f);
        divider.sizeDelta = new Vector2(DividerWidth, 0f);
        // Transparent but raycastable: the grab area has to be wide enough to hit, and the line
        // the user aims at is the hairline drawn inside it.
        divider.gameObject.AddComponent<EmptyGraphic>().raycastTarget = true;
        GameObject hairline = new("Hairline");
        hairline.transform.SetParent(divider, false);
        RectTransform hairlineRect = hairline.AddComponent<RectTransform>();
        hairlineRect.anchorMin = new Vector2(0.5f, 0f);
        hairlineRect.anchorMax = new Vector2(0.5f, 1f);
        hairlineRect.pivot = new Vector2(0.5f, 0.5f);
        hairlineRect.sizeDelta = new Vector2(2f, -16f);
        hairlineRect.anchoredPosition = Vector2.zero;
        Image hairlineImg = hairline.AddComponent<Image>();
        hairlineImg.color = new Color(1f, 1f, 1f, 0.12f);
        hairlineImg.raycastTarget = false;
        KvSplit split = obj.AddComponent<KvSplit>();
        split.Pane = pane;
        split.CanvasHost = canvasHost;
        split.Divider = divider;
        split.DividerWidth = DividerWidth;
        split.SetWidth(MainCore.Conf.KvInspectorWidth > 0f
            ? MainCore.Conf.KvInspectorWidth
            : KvWidgets.DefaultPaneWidth);
        PaneDivider drag = divider.gameObject.AddComponent<PaneDivider>();
        drag.Target = pane;
        drag.CoordinateSpace = rect;
        drag.Axis = PaneDividerAxis.Horizontal;
        // The pane is docked right, so it widens as the divider is dragged left.
        drag.Invert = true;
        drag.MinSize = KvWidgets.MinPaneWidth;
        // The real ceiling is a share of the split's live width, which this cannot know; KvSplit
        // clamps and writes back, so this only has to be past anything it would allow.
        drag.MaxSize = 2000f;
        drag.OnResized = split.SetWidth;
        drag.OnResizeEnd = _ => {
            MainCore.Conf.KvInspectorWidth = split.Applied;
            MainCore.ConfMgr.RequestSave();
        };
        // The page's scroll polls Input rather than consuming an event, so without this a
        // right-drag begun on the divider would jump the page to the pointer while it resizes.
        UIScrollController.AddInputCapture(divider);
        return new EditorSplit(canvasHost, pane, divider);
    }
    /// <summary>
    /// DM Note's PropertiesPanel surface: `bg-[#1F1F24] border-l border-[#3A3943]`. Both are
    /// ignored, anchor-filling children — on the panel itself they would be extra
    /// <c>ILayoutElement</c>s competing with its column.
    ///
    /// Square, not rounded: DM Note's panel is flush against the right edge of the canvas and only
    /// its left edge is drawn, so a radius here would show the card through four corners the real
    /// one does not have.
    /// </summary>
    private static void PanelBackdrop(RectTransform parent) {
        GameObject obj = new("PanelBg");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        obj.AddComponent<LayoutElement>().ignoreLayout = true;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image img = obj.AddComponent<Image>();
        img.color = KvPalette.PanelBg;
        img.raycastTarget = false;
        GameObject edge = new("LeftEdge");
        edge.transform.SetParent(parent, false);
        RectTransform edgeRect = edge.AddComponent<RectTransform>();
        edge.AddComponent<LayoutElement>().ignoreLayout = true;
        edgeRect.anchorMin = Vector2.zero;
        edgeRect.anchorMax = new Vector2(0f, 1f);
        edgeRect.pivot = new Vector2(0f, 0.5f);
        edgeRect.sizeDelta = new Vector2(1f, 0f);
        edgeRect.anchoredPosition = Vector2.zero;
        Image edgeImg = edge.AddComponent<Image>();
        edgeImg.color = KvPalette.Border;
        edgeImg.raycastTarget = false;
    }
    private static RectTransform Fill(RectTransform parent, string name) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
    /// <summary>
    /// The property panel: a tab strip pinned over a scroll of its own, filling the pane.
    ///
    /// It scrolls internally rather than growing with its content because its height is the
    /// split's, not its own — a Style tab's worth of colour pickers would otherwise push the
    /// canvas out of the editor entirely.
    /// </summary>
    private static EditorPanel InspectorPanel(RectTransform host) {
        GameObject obj = new("Inspector");
        obj.transform.SetParent(host, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        PanelBackdrop(rect);
        VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        RectTransform tabs = Column(rect, "Tabs", 0f);
        // The panel draws its 1px left border (PanelBackdrop's LeftEdge) at x=0 and this layout
        // has no padding, so the full-width tab track sat exactly on the border line and covered
        // it. Border plus a pixel of air; only the strip needs it — the scroll rows carry their
        // own padding.
        tabs.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(2, 0, 0, 0);
        GameObject scrollObj = new("Scroll");
        scrollObj.transform.SetParent(rect, false);
        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        LayoutElement scrollLe = scrollObj.AddComponent<LayoutElement>();
        scrollLe.minHeight = 0f;
        scrollLe.flexibleHeight = 1f;
        RectTransform content = PageFactory.CreateScrollablePage(scrollRect, out UIScrollController scroll);
        RectTransform props = GenerateUI.MakeBody(content, "Properties");
        RectTransform settings = GenerateUI.MakeBody(content, "ViewerSettings");
        // The page's own controller polls Input rather than handling an event it could consume, so
        // without this the wheel here would scroll the page as well as the panel. The controller
        // on this viewport skips its own registration; only the page behind it yields.
        UIScrollController.AddInputCapture(scroll.viewport);
        return new EditorPanel(tabs, props, settings, scroll);
    }
    private static RectTransform Column(RectTransform parent, string name, float spacing = 8f) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        GenerateUI.FitVertical(obj, spacing);
        return rect;
    }
    /// <summary>
    /// The canvas and the inspector panel both register an input capture with a static list;
    /// dropping the page without disposing them would leave one entry behind per rebuild.
    /// </summary>
    private sealed class KvCanvasTeardown : MonoBehaviour {
        public Action OnDestroyed;
        private void OnDestroy() => OnDestroyed?.Invoke();
    }
}
