using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    private const float GuideThickness = 1f;
    private static readonly Color GuideColor = new(1f, 0.35f, 0.6f, 0.9f);
    private readonly List<Image> guideLines = [];
    private RectTransform marquee;
    private void BuildGuides() {
        GameObject obj = new("Marquee");
        obj.transform.SetParent(overlay, false);
        marquee = obj.AddComponent<RectTransform>();
        marquee.anchorMin = new Vector2(0f, 1f);
        marquee.anchorMax = new Vector2(0f, 1f);
        marquee.pivot = new Vector2(0f, 1f);
        Image fill = obj.AddComponent<Image>();
        fill.color = new Color(UIColors.ObjectActive.r, UIColors.ObjectActive.g, UIColors.ObjectActive.b, 0.14f);
        fill.raycastTarget = false;
        GameObject ringObj = new("Ring");
        ringObj.transform.SetParent(obj.transform, false);
        RectTransform ringRect = ringObj.AddComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = Vector2.zero;
        ringRect.offsetMax = Vector2.zero;
        Image ring = ringObj.AddComponent<Image>();
        ring.sprite = MainCore.Spr.GetRing(2f, 1f);
        ring.type = Image.Type.Sliced;
        ring.color = UIColors.ObjectActive;
        ring.raycastTarget = false;
        obj.SetActive(false);
    }
    private Image GuideLine(int index) {
        while(guideLines.Count <= index) {
            GameObject obj = new("Guide");
            obj.transform.SetParent(overlay, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image img = obj.AddComponent<Image>();
            img.color = GuideColor;
            img.raycastTarget = false;
            obj.SetActive(false);
            guideLines.Add(img);
        }
        return guideLines[index];
    }
    private void ShowGuides(IReadOnlyList<KvGuide> guides) {
        int count = 0;
        if(guides != null) {
            count = guides.Count;
            for(int i = 0; i < count; i++) {
                KvGuide g = guides[i];
                Vector2 a = g.Vertical ? LayoutToOverlay(g.Position, g.From) : LayoutToOverlay(g.From, g.Position);
                Vector2 b = g.Vertical ? LayoutToOverlay(g.Position, g.To) : LayoutToOverlay(g.To, g.Position);
                Image img = GuideLine(i);
                RectTransform rect = img.rectTransform;
                rect.anchoredPosition = (a + b) * 0.5f;
                rect.sizeDelta = g.Vertical
                    ? new Vector2(GuideThickness, Mathf.Abs(b.y - a.y))
                    : new Vector2(Mathf.Abs(b.x - a.x), GuideThickness);
                if(!img.gameObject.activeSelf) img.gameObject.SetActive(true);
            }
        }
        for(int i = count; i < guideLines.Count; i++)
            if(guideLines[i].gameObject.activeSelf) guideLines[i].gameObject.SetActive(false);
    }
    private void ClearGuides() => ShowGuides(null);
    private void ShowMarquee(KvRect r) {
        if(marquee == null) return;
        Vector2 topLeft = LayoutToOverlay(r.Left, r.Top);
        Vector2 bottomRight = LayoutToOverlay(r.Right, r.Bottom);
        marquee.anchoredPosition = topLeft;
        marquee.sizeDelta = new Vector2(Mathf.Abs(bottomRight.x - topLeft.x), Mathf.Abs(topLeft.y - bottomRight.y));
        if(!marquee.gameObject.activeSelf) marquee.gameObject.SetActive(true);
    }
    private void HideMarquee() {
        if(marquee != null && marquee.gameObject.activeSelf) marquee.gameObject.SetActive(false);
    }
}
