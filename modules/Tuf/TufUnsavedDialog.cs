using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Quartz.Features.Tuf;
internal static class TufUnsavedDialog {
    internal const int Pending = -1;
    internal const int Save = 0;
    internal const int Discard = 1;
    internal const int Cancel = 2;
    private const float CardWidth = 560f;
    private const float CardHeight = 232f;
    private const float ButtonWidth = 136f;
    private const float ButtonHeight = 42f;
    private static GameObject root;
    private static Action<int> callback;
    internal static bool IsOpen => root != null;
    internal static void Show(Action<int> onChoice) {
        Close();
        callback = onChoice;
        root = UnityUtils.CreateOverlayCanvas(
            "QuartzTufUnsavedDialog", MainCore.Root.transform, 32767, out GraphicRaycaster raycaster);
        raycaster.enabled = true;
        Scrim(root.transform);
        RectTransform card = Card(root.transform);
        Label(card,
            MainCore.Tr.Get("TUF_UNSAVED_TITLE", "Unsaved editor changes"),
            22f, Color.white, new Vector2(30f, -68f), new Vector2(-30f, -26f), false);
        Label(card,
            MainCore.Tr.Get("TUF_UNSAVED_BODY",
                "The level open in the editor has changes you have not saved. "
                + "Save them before loading this TUF level?"),
            16f, new Color(1f, 1f, 1f, 0.72f), new Vector2(30f, -150f), new Vector2(-30f, -76f), true);
        RectTransform row = Row(card);
        Button(row, MainCore.Tr.Get("TUF_UNSAVED_CANCEL", "Cancel"), UIColors.ObjectBG, Cancel);
        Button(row, MainCore.Tr.Get("TUF_UNSAVED_DISCARD", "Discard"), UIColors.ObjectBG, Discard);
        Button(row, MainCore.Tr.Get("TUF_UNSAVED_SAVE", "Save"), UIColors.ObjectButton, Save);
    }
    internal static void Close() {
        callback = null;
        if(root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }
    private static void Pick(int choice) {
        Action<int> pending = callback;
        Close();
        pending?.Invoke(choice);
    }
    private static void Scrim(Transform parent) {
        GameObject obj = new("Scrim");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);
        image.raycastTarget = true;
    }
    private static RectTransform Card(Transform parent) {
        GameObject obj = new("Card");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        Image image = obj.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        image.type = Image.Type.Sliced;
        image.color = UIColors.PanelBG;
        image.raycastTarget = true;
        GameObject border = new("Border");
        border.transform.SetParent(obj.transform, false);
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2f, -2f);
        borderRect.offsetMax = new Vector2(2f, 2f);
        Image borderImage = border.AddComponent<Image>();
        borderImage.sprite = MainCore.Spr.GetRing(14.5f, 3f);
        borderImage.type = Image.Type.Sliced;
        borderImage.color = UIColors.ObjectButton;
        borderImage.raycastTarget = false;
        return rect;
    }
    private static void Label(
        RectTransform card, string text, float size, Color color,
        Vector2 offsetMin, Vector2 offsetMax, bool wrap
    ) {
        GameObject obj = new("Label");
        obj.transform.SetParent(card, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.font = FontManager.Current;
        label.fontSize = size;
        label.color = color;
        label.text = text;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.characterSpacing = -3f;
        label.raycastTarget = false;
        TextCompat.SetWrap(label, wrap);
    }
    private static RectTransform Row(RectTransform card) {
        GameObject obj = new("Buttons");
        obj.transform.SetParent(card, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(26f, 24f);
        rect.offsetMax = new Vector2(-26f, 24f + ButtonHeight);
        HorizontalLayoutGroup layout = obj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return rect;
    }
    private static void Button(RectTransform row, string text, Color normal, int choice) {
        GameObject obj = new("Button");
        obj.transform.SetParent(row, false);
        obj.AddComponent<RectTransform>();
        Image image = obj.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        image.type = Image.Type.Sliced;
        image.color = normal;
        LayoutElement element = obj.AddComponent<LayoutElement>();
        element.minWidth = ButtonWidth;
        element.preferredWidth = ButtonWidth;
        element.minHeight = ButtonHeight;
        element.preferredHeight = ButtonHeight;
        GameObject labelObj = new("Label");
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.font = FontManager.Current;
        label.fontSize = 16f;
        label.color = Color.white;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.characterSpacing = -3f;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        TextCompat.NoWrap(label);
        Color hover = Color.Lerp(normal, Color.white, 0.18f);
        EventTrigger trigger = obj.AddComponent<EventTrigger>();
        UnityUtils.AddClickEvent(trigger, _ => Pick(choice));
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => image.color = hover, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => image.color = normal, trigger);
    }
}
