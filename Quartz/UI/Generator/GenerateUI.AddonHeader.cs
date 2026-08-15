using Quartz.Compat.Game;
using Quartz.Resource;
using Quartz.UI.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public static Transform AddonHeader(
        Transform parent,
        string name,
        string description,
        string author,
        string iconPath
    ) => AddonHeader(
        parent,
        name,
        description,
        author,
        !string.IsNullOrEmpty(iconPath) && File.Exists(iconPath) ? File.ReadAllBytes(iconPath) : null
    );
    public static Transform AddonHeader(
        Transform parent,
        string name,
        string description,
        string author,
        byte[] iconPng = null
    ) {
        Transform card = HomeUI.Card(parent, null);
        RectTransform content = Row(card, 110f);
        HorizontalLayoutGroup horizontal = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(4, 4, 2, 2);
        horizontal.spacing = 16f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        GameObject textAreaObj = new("AddonInfo");
        textAreaObj.transform.SetParent(content, false);
        RectTransform textArea = textAreaObj.AddComponent<RectTransform>();
        textAreaObj.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup vertical = textAreaObj.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 2f;
        vertical.padding = new RectOffset(0, 0, 0, 0);
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childAlignment = TextAnchor.MiddleLeft;
        TextMeshProUGUI title = AddText(Row(textArea, 34f), true);
        title.text = name;
        title.fontSize = 35f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Left;
        title.verticalAlignment = VerticalAlignmentOptions.Middle;
        TextMeshProUGUI desc = AddMutedText(Row(textArea, 38f), 20f, 0.65f, true);
        desc.text = description;
        TextCompat.Wrap(desc);
        desc.alignment = TextAlignmentOptions.Left;
        desc.verticalAlignment = VerticalAlignmentOptions.Top;
        TextMeshProUGUI authorText = AddMutedText(Row(textArea, 24f), 20f, 0.45f, true);
        if(!string.IsNullOrEmpty(author)) authorText.text = $"by {author}";
        AddonHeaderIcon(content, iconPng);
        return card;
    }
    private static void AddonHeaderIcon(Transform content, byte[] iconPng) {
        if(iconPng == null || iconPng.Length == 0) return;
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        if(!texture.LoadImage(iconPng)) {
            Object.Destroy(texture);
            return;
        }
        GameObject iconObj = new("AddonIcon");
        iconObj.transform.SetParent(content, false);
        iconObj.AddComponent<RectTransform>();
        LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.minWidth = 105f;
        iconLayout.preferredWidth = 105f;
        iconLayout.minHeight = 105f;
        iconLayout.preferredHeight = 105f;
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = SpriteManager.Create(texture);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }
}
