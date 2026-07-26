using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Home;
public static class HomeUI {
    public const float CardHeight = 148f;
    public const float StatHeight = 96f;
    public static RectTransform Grid(Transform parent, float height) {
        RectTransform row = GenerateUI.Row(parent, height);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(16, 16, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.UpperLeft;
        return row;
    }
    public static Transform Card(Transform grid, string title) {
        GameObject cardObj = new("Card");
        cardObj.transform.SetParent(grid, false);
        cardObj.AddComponent<RectTransform>();
        Image bg = cardObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.PanelBG, UIColors.ObjectBG, 0.55f);
        bg.raycastTarget = false;
        VerticalLayoutGroup layout = cardObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;
        if(!string.IsNullOrEmpty(title)) {
            TextMeshProUGUI header = GenerateUI.AddText(GenerateUI.Row(cardObj.transform, 30f), true);
            header.text = title;
            header.fontSize = 19f;
            header.color = new Color(1f, 1f, 1f, 0.92f);
            header.alignment = TextAlignmentOptions.Left;
            header.verticalAlignment = VerticalAlignmentOptions.Middle;
            header.overflowMode = TextOverflowModes.Ellipsis;
        }
        return cardObj.transform;
    }
    public static void Stat(Transform grid, string value, string label) {
        Transform card = Card(grid, null);
        TextMeshProUGUI number = GenerateUI.AddText(GenerateUI.Row(card, 40f), true);
        number.text = value;
        number.fontSize = 30f;
        number.color = UIColors.ObjectActiveBright;
        number.alignment = TextAlignmentOptions.Left;
        number.verticalAlignment = VerticalAlignmentOptions.Middle;
        number.overflowMode = TextOverflowModes.Ellipsis;
        TextMeshProUGUI caption = GenerateUI.AddMutedText(GenerateUI.Row(card, 24f), 15f, 0.5f, true);
        caption.text = label;
        caption.overflowMode = TextOverflowModes.Ellipsis;
    }
    public static TextMeshProUGUI Line(Transform parent, string text) {
        TextMeshProUGUI label = GenerateUI.AddMutedText(GenerateUI.Row(parent, 26f), 15f, 0.5f, true);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = text;
        return label;
    }
}
