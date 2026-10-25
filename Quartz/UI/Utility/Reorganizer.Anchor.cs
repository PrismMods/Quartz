using Quartz.Core;
using Quartz.Overlay;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.UI.Utility;
public sealed partial class ReorganizeHandle {
    // Set by overlays with a nine-way anchor: Closest re-anchors to the nearest one while dragging,
    // SetAnchor moves the overlay onto another anchor without moving it on screen.
    public Func<bool> AnchorClosest;
    public Action<OverlayAnchor> SetAnchor;
    internal bool HasAnchor => Target != null && Target.anchorMin == Target.anchorMax;
    internal OverlayAnchor CurrentAnchor => OverlayAnchors.Parse(
        Mathf.RoundToInt(Target.anchorMin.x * 2f) + Mathf.RoundToInt((1f - Target.anchorMin.y) * 2f) * 3);
    public static void SetAnchorSource(GameObject drag, Func<bool> closest, Action<OverlayAnchor> setAnchor) {
        if(drag == null || drag.GetComponent<ReorganizeHandle>() is not { } handle) return;
        handle.AnchorClosest = closest;
        handle.SetAnchor = setAnchor;
        if(!anchored.Contains(handle)) anchored.Add(handle);
    }
    // Every overlay with an anchor source, live or hidden, so picking Closest in settings can apply it at once.
    internal static readonly List<ReorganizeHandle> anchored = [];
}
// The selected overlay's anchor point, joined to its origin by a line, like osu!'s skin editor.
internal sealed class ReorganizeAnchorLine : MonoBehaviour {
    private const float Thickness = 2f;
    private const float DotSize = 12f;
    public ReorganizeHandle Owner;
    private RectTransform line;
    private RectTransform dot;
    internal static ReorganizeAnchorLine Create(Transform parent, ReorganizeHandle owner) {
        GameObject obj = new("ReorganizeAnchorLine");
        obj.transform.SetParent(parent, false);
        obj.transform.SetAsFirstSibling();
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        ReorganizeAnchorLine self = obj.AddComponent<ReorganizeAnchorLine>();
        self.Owner = owner;
        self.line = self.Part("Line", null, new Vector2(0f, 0.5f));
        self.dot = self.Part("Dot", MainCore.Spr.Get(UISprite.Circle256, DotSize), new Vector2(0.5f, 0.5f));
        self.dot.sizeDelta = new Vector2(DotSize, DotSize);
        self.LateUpdate();
        return self;
    }
    private RectTransform Part(string name, Sprite sprite, Vector2 pivot) {
        GameObject obj = new(name);
        obj.transform.SetParent(transform, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = pivot;
        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.color = UIColors.SoftRed;
        image.raycastTarget = false;
        return rect;
    }
    private void LateUpdate() {
        RectTransform target = Owner != null && Owner.HasAnchor ? Owner.Target : null;
        bool show = target != null && target.parent is RectTransform;
        if(line.gameObject.activeSelf != show) {
            line.gameObject.SetActive(show);
            dot.gameObject.SetActive(show);
        }
        if(!show) return;
        RectTransform parent = (RectTransform)target!.parent;
        Vector3 anchorWorld = parent.TransformPoint(Rect.NormalizedToPoint(parent.rect, target.anchorMin));
        RectTransform self = (RectTransform)transform;
        Vector2 from = self.InverseTransformPoint(anchorWorld);
        Vector2 to = self.InverseTransformPoint(target.position);
        Vector2 delta = to - from;
        dot.anchoredPosition = from;
        line.anchoredPosition = from;
        line.sizeDelta = new Vector2(delta.magnitude, Thickness);
        line.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }
}
public static partial class Reorganizer {
    private static ReorganizeAnchorLine anchorLine;
    private static void BuildAnchorLine(ReorganizeHandle handle) {
        ClearAnchorLine();
        if(canvasObj != null && handle != null) anchorLine = ReorganizeAnchorLine.Create(canvasObj.transform, handle);
    }
    private static void ClearAnchorLine() {
        if(anchorLine != null) Object.Destroy(anchorLine.gameObject);
        anchorLine = null;
    }
    public static void SnapAllAnchors() {
        ReorganizeHandle.anchored.RemoveAll(static h => h == null);
        foreach(ReorganizeHandle handle in ReorganizeHandle.anchored.ToArray()) SnapAnchor(handle);
    }
    // Closest mode: the anchor follows whichever third of the screen the overlay's middle sits in.
    internal static void SnapAnchor(ReorganizeHandle handle) {
        if(handle?.SetAnchor == null || handle.AnchorClosest?.Invoke() != true || !handle.HasAnchor) return;
        if(handle.Target.parent is not RectTransform parent) return;
        RectTransform measure = handle.MeasureRect;
        Vector2 local = parent.InverseTransformPoint(measure.TransformPoint(measure.rect.center));
        Vector2 n = Rect.PointToNormalized(parent.rect, local);
        int col = n.x < 1f / 3f ? 0 : n.x < 2f / 3f ? 1 : 2;
        int row = n.y > 2f / 3f ? 0 : n.y > 1f / 3f ? 1 : 2;
        OverlayAnchor want = (OverlayAnchor)(row * 3 + col);
        if(want != handle.CurrentAnchor) handle.SetAnchor(want);
    }
}
