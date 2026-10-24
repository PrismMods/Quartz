using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
internal static partial class PageKeyViewer {
    private const float EditorCanvasMinHeight = 200f;
    private const float ThinRowHeight = 26f;
    private const float RegionSpacing = 8f;
    private const float DividerWidth = 10f;
    private readonly struct EditorPanel(
        RectTransform tabs, RectTransform props, RectTransform settings, UIScrollController scroll
    ) {
        internal RectTransform Tabs { get; } = tabs;
        internal RectTransform Props { get; } = props;
        internal RectTransform Settings { get; } = settings;
        internal UIScrollController Scroll { get; } = scroll;
    }
    private readonly struct EditorSplit(
        RectTransform canvasHost, RectTransform paneHost, RectTransform divider
    ) {
        internal RectTransform CanvasHost { get; } = canvasHost;
        internal RectTransform PaneHost { get; } = paneHost;
        internal RectTransform Divider { get; } = divider;
    }
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
        cardObj.AddComponent<LayoutElement>().ignoreLayout = true;
        Image card = cardObj.AddComponent<Image>();
        card.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        card.type = Image.Type.Sliced;
        card.color = Color.Lerp(UIColors.PanelBG, UIColors.ObjectBG, 0.45f);
        card.raycastTarget = false;
        VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RegionSpacing;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        obj.AddComponent<LayoutElement>();
        if(pageScroll != null) {
            KvFillHeight fill = obj.AddComponent<KvFillHeight>();
            fill.Content = pageScroll.content;
            fill.Viewport = pageScroll.viewport;
        }
        return rect;
    }
    private static EditorSplit EditorSplitRow(RectTransform editor) {
        GameObject obj = new("Split");
        obj.transform.SetParent(editor, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = EditorCanvasMinHeight;
        le.preferredHeight = EditorCanvasMinHeight;
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
        drag.Invert = true;
        drag.MinSize = KvWidgets.MinPaneWidth;
        drag.MaxSize = 2000f;
        drag.OnResized = split.SetWidth;
        drag.OnResizeEnd = _ => {
            MainCore.Conf.KvInspectorWidth = split.Applied;
            MainCore.ConfMgr.RequestSave();
        };
        UIScrollController.AddInputCapture(divider);
        return new EditorSplit(canvasHost, pane, divider);
    }
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
    private sealed class KvCanvasTeardown : MonoBehaviour {
        public Action OnDestroyed;
        private void OnDestroy() => OnDestroyed?.Invoke();
    }
}
