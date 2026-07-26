using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    private sealed class Visual {
        internal KvElement El;
        internal RectTransform Rect;
        internal Image Fill;
        internal Image Border;
        internal Image Outline;
        internal TextMeshProUGUI Label;
        internal TextMeshProUGUI Counter;
        internal float Radius = float.NaN;
        internal float BorderWidth = float.NaN;
    }
    private const float DimAlpha = 0.35f;
    private RectTransform root;
    private RectTransform viewport;
    private RectTransform content;
    private RectTransform overlay;
    private KvGrid grid;
    private KvCanvasDriver driver;
    private KvDocument doc;
    private string tab = "";
    private float zoom = 1f;
    private readonly List<Visual> visuals = [];
    private readonly List<KvElement> selection = [];
    private readonly List<KvElement> selectionScratch = [];
    private readonly KvHistory history = new();
    internal IReadOnlyList<KvElement> Selection => selection;
    internal KvDocument Document => doc;
    internal string Tab => tab;
    internal float Zoom => zoom;
    internal RectTransform Rect => root;
    internal bool CanUndo => history.CanUndo;
    internal bool CanRedo => history.CanRedo;
    internal event Action SelectionChanged;
    internal event Action<KvElement> ElementActivated;
    internal event Action Changed;
    internal event Action<KvDocument> DocumentReplaced;
    internal static KvCanvas Create(Transform parent) {
        KvCanvas c = new();
        GameObject rootObj = new("KvCanvas");
        rootObj.transform.SetParent(parent, false);
        c.root = rootObj.AddComponent<RectTransform>();
        c.root.anchorMin = Vector2.zero;
        c.root.anchorMax = Vector2.one;
        c.root.offsetMin = Vector2.zero;
        c.root.offsetMax = Vector2.zero;
        GameObject viewObj = new("Viewport");
        viewObj.transform.SetParent(rootObj.transform, false);
        c.viewport = viewObj.AddComponent<RectTransform>();
        c.viewport.anchorMin = Vector2.zero;
        c.viewport.anchorMax = Vector2.one;
        c.viewport.offsetMin = Vector2.zero;
        c.viewport.offsetMax = Vector2.zero;
        Image bg = viewObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 8f / 6f;
        bg.color = KvPalette.CanvasWell;
        bg.raycastTarget = true;
        viewObj.AddComponent<RectMask2D>();
        GameObject gridObj = new("Grid");
        gridObj.transform.SetParent(viewObj.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = Vector2.zero;
        c.grid = gridObj.AddComponent<KvGrid>();
        c.grid.raycastTarget = false;
        GameObject contentObj = new("Content");
        contentObj.transform.SetParent(viewObj.transform, false);
        c.content = contentObj.AddComponent<RectTransform>();
        TopLeft(c.content);
        GameObject overlayObj = new("Overlay");
        overlayObj.transform.SetParent(viewObj.transform, false);
        c.overlay = overlayObj.AddComponent<RectTransform>();
        TopLeft(c.overlay);
        c.BuildGuides();
        c.BuildHandles();
        c.BuildZoomLabel();
        c.driver = viewObj.AddComponent<KvCanvasDriver>();
        c.driver.Owner = c;
        UIScrollController.AddInputCapture(c.viewport);
        c.SyncOverlay();
        return c;
    }
    private static void TopLeft(RectTransform rt) {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
    internal void Bind(KvDocument document, string tabId) {
        doc = document;
        tab = tabId ?? "";
        selection.Clear();
        history.Clear();
        zoom = 1f;
        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
        Rebuild();
        needsCentre = true;
        SelectionChanged?.Invoke();
    }
    internal void Rebuild() {
        foreach(Visual v in visuals) {
            if(v.Counter != null) Object.Destroy(v.Counter.gameObject);
            if(v.Rect != null) Object.Destroy(v.Rect.gameObject);
        }
        visuals.Clear();
        if(doc == null || string.IsNullOrEmpty(tab)) {
            SyncOverlay();
            return;
        }
        List<KvElement> all = doc.AllElements(tab);
        for(int i = selection.Count - 1; i >= 0; i--)
            if(!all.Contains(selection[i])) selection.RemoveAt(i);
        foreach(KvElement el in all) {
            try {
                visuals.Add(BuildVisual(el));
            } catch(Exception e) {
                MainCore.Log.Wrn($"[KvCanvas] element visual failed: {e.Message}");
            }
        }
        Refresh();
    }
    internal void Refresh() {
        foreach(Visual v in visuals) {
            if(v.Rect == null) continue;
            try {
                Paint(v);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[KvCanvas] element paint failed: {e.Message}");
            }
        }
        SyncOverlay();
    }
    private void SyncGeometry() {
        foreach(Visual v in visuals) {
            if(v.Rect == null) continue;
            v.Rect.anchoredPosition = new Vector2(v.El.X, -v.El.Y);
            v.Rect.sizeDelta = new Vector2(v.El.W, v.El.H);
        }
        SyncOverlay();
    }
    private void SyncOverlay() {
        SyncHandles();
        SyncZoomLabel();
        grid?.Sync(zoom, content.anchoredPosition);
    }
    internal void SetSelection(IEnumerable<KvElement> elements) {
        selectionScratch.Clear();
        if(elements != null)
            foreach(KvElement el in elements)
                if(el != null && !selectionScratch.Contains(el)) selectionScratch.Add(el);
        selection.Clear();
        selection.AddRange(selectionScratch);
        AfterSelectionChanged();
    }
    internal void ClearSelection() {
        if(selection.Count == 0) return;
        selection.Clear();
        AfterSelectionChanged();
    }
    private void AfterSelectionChanged() {
        foreach(Visual v in visuals)
            if(v.Outline != null) v.Outline.enabled = selection.Contains(v.El);
        SyncOverlay();
        SelectionChanged?.Invoke();
    }
    private void Select(KvElement el, bool additive) {
        if(el == null) {
            if(!additive) ClearSelection();
            return;
        }
        if(additive) {
            if(!selection.Remove(el)) selection.Add(el);
        } else {
            if(selection.Count == 1 && selection[0] == el) return;
            selection.Clear();
            selection.Add(el);
        }
        AfterSelectionChanged();
    }
    private KvElement HitTest(Vector2 layout) {
        for(int i = visuals.Count - 1; i >= 0; i--) {
            KvElement el = visuals[i].El;
            if(el.Hidden) continue;
            if(layout.x >= el.X && layout.x <= el.X + el.W
                && layout.y >= el.Y && layout.y <= el.Y + el.H) return el;
        }
        return null;
    }
    private static KvRect RectOf(KvElement el) => new(el.X, el.Y, el.W, el.H);
    private Vector2 ScreenToLayout(Vector2 screen) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, null, out Vector2 local);
        return new Vector2(local.x, -local.y);
    }
    private Vector2 ScreenToOverlay(Vector2 screen) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, null, out Vector2 local);
        return local;
    }
    private Vector2 LayoutToOverlay(float lx, float ly) =>
        content.anchoredPosition + new Vector2(lx * zoom, -ly * zoom);
    private bool PointerOverViewport() =>
        viewport != null
        && RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, null);
    internal void PushHistory() {
        if(doc == null) return;
        try {
            history.Push(doc.ToJson());
        } catch(Exception e) {
            MainCore.Log.Wrn($"[KvCanvas] history snapshot failed: {e.Message}");
        }
    }
    internal void Mutated() => Changed?.Invoke();
    internal void Dispose() {
        UIScrollController.RemoveInputCapture(viewport);
        if(driver != null) driver.Owner = null;
        if(root != null) Object.Destroy(root.gameObject);
        root = null;
        visuals.Clear();
        selection.Clear();
    }
    internal void OnDriverDestroyed() => UIScrollController.RemoveInputCapture(viewport);
}
