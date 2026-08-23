using Quartz.Core;
using Quartz.Resource;
using Quartz.Features.Discord;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Factory.Page;
public static partial class PageDiscord {
    private static RectTransform Node(Transform parent, string name) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static RectTransform Fill(Transform parent, string name, float left, float top, float right, float bottom) {
        RectTransform rect = Node(parent, name);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }
    private static RectTransform LeftBand(Transform parent, string name, float left, float width) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(width, 0f);
        return rect;
    }
    private static RectTransform Box(Transform parent, string name, float x, float y, float width, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static RectTransform BoxRight(Transform parent, string name, float right, float y, float width, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static RectTransform Strip(Transform parent, string name, float left, float y, float right, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(y + height));
        rect.offsetMax = new Vector2(-right, -y);
        return rect;
    }
    private static RectTransform StripBottom(Transform parent, string name, float left, float y, float right, float height) {
        RectTransform rect = Node(parent, name);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, y);
        rect.offsetMax = new Vector2(-right, y + height);
        return rect;
    }
    private static RectTransform AutoRow(Transform parent, float x, float y, float height, float spacing) {
        RectTransform rect = Box(parent, "AutoRow", x, y, 10f, height);
        HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }
    private static RectTransform Avatar(
        Transform parent, float x, float y, float size, Color color, string initial,
        string url = null, int radius = 3
    ) {
        RectTransform rect = Box(parent, "Avatar", x, y, size, size);
        Sprite sprite = AvatarCache.Get(url);
        if(sprite == null) {
            Paint(rect, color, radius);
            Label(rect, initial, size * 0.44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            return rect;
        }
        Paint(rect, Color.white, radius);
        Mask mask = rect.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform inner = Node(rect, "Image");
        Image image = inner.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }
    private static void Status(RectTransform avatar, Color color, Color ring) {
        float size = Mathf.Round(avatar.sizeDelta.x * 0.4f);
        RectTransform outer = Node(avatar, "Status");
        outer.anchorMin = new Vector2(1f, 0f);
        outer.anchorMax = new Vector2(1f, 0f);
        outer.pivot = new Vector2(1f, 0f);
        outer.anchoredPosition = new Vector2(2f, -2f);
        outer.sizeDelta = new Vector2(size, size);
        Paint(outer, ring, 3);
        RectTransform inner = Node(outer, "Dot");
        inner.anchorMin = new Vector2(0.5f, 0.5f);
        inner.anchorMax = new Vector2(0.5f, 0.5f);
        inner.pivot = new Vector2(0.5f, 0.5f);
        inner.anchoredPosition = Vector2.zero;
        inner.sizeDelta = new Vector2(size - 5f, size - 5f);
        Paint(inner, color, 3);
    }
    private static Image Paint(RectTransform rect, Color color, int radius = 0) {
        Image image = rect.gameObject.GetComponent<Image>();
        if(image == null) image = rect.gameObject.AddComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        if(radius == 1) {
            image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            image.type = Image.Type.Sliced;
        } else if(radius == 2) {
            image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
            image.type = Image.Type.Sliced;
        } else if(radius == 3) {
            image.sprite = MainCore.Spr.Get(UISprite.Circle256);
        }
        return image;
    }
    private static TextMeshProUGUI Label(
        Transform parent, string value, float size, Color color,
        FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Left
    ) {
        TextMeshProUGUI text = GenerateUI.AddText(parent, true);
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = align;
        text.characterSpacing = 0f;
        text.raycastTarget = false;
        text.richText = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
}
