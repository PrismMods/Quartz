using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Utility;
// While reorganizing, every movable overlay shows a faint box with its name tag above it, so they can be found before
// they are clicked. The drag surface is only active in reorganize mode, so the frame lives and dies with it.
public sealed partial class ReorganizeHandle {
    private GameObject frameOutline;
    private TextMeshProUGUI frameLabel;
    private RectTransform tagHolder;
    private RectTransform tagRect;
    private bool tagBelow;
    private static readonly Vector3[] corners = new Vector3[4];
    private void RefreshFrame() {
        if(frameOutline == null) BuildFrame();
        if(frameLabel != null) frameLabel.text = GetName?.Invoke() ?? (Target != null ? Target.name : name);
        SetFrameSelected(Reorganizer.Selected == this);
    }
    // The selected outline (with grips) takes over; two boxes on one overlay read as noise.
    internal void SetFrameSelected(bool selected) {
        if(this == null || frameOutline == null) return;
        frameOutline.SetActive(!selected);
    }
    private void BuildFrame() {
        frameOutline = new GameObject("ReorganizeFrame");
        frameOutline.transform.SetParent(transform, false);
        RectTransform rect = frameOutline.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-6f, -6f);
        rect.offsetMax = new Vector2(6f, 6f);
        frameOutline.AddComponent<LayoutElement>().ignoreLayout = true;
        Image outline = frameOutline.AddComponent<Image>();
        outline.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        outline.type = Image.Type.Sliced;
        Color c = UIColors.ObjectActive;
        c.a = 0.45f;
        outline.color = c;
        outline.raycastTarget = false;
        // The tag sits outside the outline object so it stays visible while the overlay is selected.
        GameObject holder = new("ReorganizeNameTagRoot");
        holder.transform.SetParent(transform, false);
        RectTransform holderRect = holder.AddComponent<RectTransform>();
        holderRect.sizeDelta = Vector2.zero;
        tagHolder = holderRect;
        holder.AddComponent<LayoutElement>().ignoreLayout = true;
        GameObject tag = new("ReorganizeNameTag");
        tag.transform.SetParent(holder.transform, false);
        tagRect = tag.AddComponent<RectTransform>();
        tagRect.anchorMin = tagRect.anchorMax = Vector2.zero;
        Image bg = tag.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        bg.raycastTarget = false;
        HorizontalLayoutGroup layout = tag.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 3, 3);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        ContentSizeFitter fitter = tag.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        GameObject textObj = new("Text");
        textObj.transform.SetParent(tag.transform, false);
        textObj.AddComponent<RectTransform>();
        frameLabel = textObj.AddComponent<TextMeshProUGUI>();
        // Chrome, not overlay content: keep the settings-window font when overlay fonts are re-applied.
        textObj.AddComponent<FontExempt>();
        frameLabel.font = FontManager.UiFont;
        frameLabel.fontSize = 18f;
        frameLabel.color = Color.white;
        frameLabel.raycastTarget = false;
        TextCompat.NoWrap(frameLabel);
        holder.AddComponent<ReorganizeGripScaler>().Grips = [tag.transform];
        PlaceTag(false);
    }
    private void PlaceTag(bool below) {
        tagBelow = below;
        tagHolder.anchorMin = tagHolder.anchorMax = new Vector2(0f, below ? 0f : 1f);
        tagHolder.anchoredPosition = new Vector2(-6f, below ? -6f : 6f);
        tagRect.pivot = new Vector2(0f, below ? 1.15f : -0.15f);
    }
    // Overlays pinned to the top edge would push the tag off screen; drop it under the box instead.
    private void LateUpdate() {
        if(tagRect == null) return;
        ((RectTransform)transform).GetWorldCorners(corners);
        float top = Mathf.Max(Mathf.Max(corners[0].y, corners[1].y), Mathf.Max(corners[2].y, corners[3].y));
        float tagH = tagRect.rect.height * 1.15f * tagRect.lossyScale.y;
        bool below = top + tagH > Screen.height;
        if(below != tagBelow) PlaceTag(below);
    }
}
