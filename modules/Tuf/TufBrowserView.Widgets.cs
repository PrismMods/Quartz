using System.Text;
using Quartz.Core;
using Quartz.Features.Tuf;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal sealed partial class TufBrowserView : MonoBehaviour {
    private void AddLoadingStatus(string message, float height = 70f) {
        RectTransform row = FixedRow("Loading", height);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.35f);
        HorizontalLayoutGroup layout = AddHorizontal(row, 12f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = false;
        UISpinner spinner = UISpinner.Attach(row, 26f, new Color(1f, 1f, 1f, 0.55f));
        LayoutElement spinnerSize = spinner.gameObject.AddComponent<LayoutElement>();
        spinnerSize.minWidth = spinnerSize.preferredWidth = 26f;
        spinnerSize.minHeight = spinnerSize.preferredHeight = 26f;
        TMP_Text label = Text(row, message, 18f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, 0.48f);
    }
    private void AddOfflineStatus() {
        int installed = service.InstalledCount;
        AddStatus(Tr("TUF_OFFLINE", "TUF could not be reached — you may be offline.") + "\n" + service.Error, false, null, 78f);
        RectTransform row = FixedRow("Offline Actions", 58f);
        HorizontalLayoutGroup layout = AddHorizontal(row, 10f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = new RectOffset(0, 0, 6, 6);
        (Image switchChip, TMP_Text switchLabel) = Chip(row, "", 264f, () => {
            DisarmDelete();
            service.ShowInstalledLevels();
        });
        switchChip.color = installed > 0 ? UIColors.ObjectActive : UIColors.ObjectBG;
        switchLabel.text = installed > 0
            ? string.Format(Tr("TUF_OFFLINE_SWITCH", "Switch to Installed ({0})"), installed)
            : Tr("TUF_OFFLINE_SWITCH_EMPTY", "Switch to Installed");
        (Image _, TMP_Text retryLabel) = Chip(row, Tr("TUF_RETRY", "Retry"), 96f, service.Refresh);
        retryLabel.color = new(1f, 1f, 1f, 0.82f);
    }
    private void AddStatus(string message, bool button, Action action, float height = 70f) {
        RectTransform row = FixedRow("Status", height);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = button ? UIColors.ObjectBG : Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.35f);
        TMP_Text label = Text(row, message, 18f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, button ? 0.9f : 0.48f);
        if(action != null) GenerateUI.AddButton(row.gameObject, input => {
            if(input == PointerEventData.InputButton.Left) action();
        });
    }
    private RectTransform FixedRow(string name, float height) {
        RectTransform row = Rect(name, content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
        size.minHeight = height;
        size.preferredHeight = height;
        return row;
    }
    private static (Image, TMP_Text) Chip(Transform parent, string value, float width, Action action) {
        RectTransform rect = Rect("Chip " + value, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        image.color = UIColors.ObjectBG;
        TMP_Text label = Text(rect, value, 17f, TextAlignmentOptions.Center);
        GenerateUI.AddButton(rect.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) action?.Invoke();
        });
        return (image, label);
    }
    private static Image IconChip(Transform parent, UISprite sprite, float width, Action action) {
        RectTransform rect = Rect("Chip " + sprite, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        image.type = Image.Type.Sliced;
        image.color = UIColors.ObjectBG;
        RectTransform iconRect = Rect("Icon", rect, new(0.5f, 0.5f), new(0.5f, 0.5f), new(-11f, -11f), new(11f, 11f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = MainCore.Spr.Get(sprite, 22f);
        icon.color = new(1f, 1f, 1f, 0.9f);
        icon.raycastTarget = false;
        GenerateUI.AddButton(rect.gameObject, button => {
            if(button == PointerEventData.InputButton.Left) action?.Invoke();
        });
        return image;
    }
    private static HorizontalLayoutGroup AddHorizontal(Transform row, float spacing = 8f) {
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        return layout;
    }
    private static void AddFlexibleSpacer(Transform row) {
        RectTransform spacer = Rect("Spacer", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }
    private static TMP_Text Text(Transform parent, string value, float size, TextAlignmentOptions align) {
        TextMeshProUGUI text = GenerateUI.AddText(parent, true);
        text.text = value;
        text.font = FontManager.Current;
        text.fontSize = size;
        text.alignment = align;
        text.richText = false;
        SetFull(text.rectTransform, 0f, 0f);
        return text;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }
    private static void SetFull(RectTransform rect, float left, float right) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new(left, 0f);
        rect.offsetMax = new(-right, 0f);
    }
    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
}
