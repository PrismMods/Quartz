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
internal sealed partial class TufPacksView : MonoBehaviour {
    private void AddLoadingStatus(string message, float height = 70f) {
        RectTransform row = FixedRow("Loading", height);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, UIColors.PanelBG, 0.35f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
        UISpinner spinner = UISpinner.Attach(row, 26f, new Color(1f, 1f, 1f, 0.55f));
        LayoutElement spinnerSize = spinner.gameObject.AddComponent<LayoutElement>();
        spinnerSize.minWidth = spinnerSize.preferredWidth = 26f;
        spinnerSize.minHeight = spinnerSize.preferredHeight = 26f;
        TMP_Text label = Text(row, message, 18f, TextAlignmentOptions.Center);
        label.color = new(1f, 1f, 1f, 0.48f);
    }
    private void AddOfflineStatus(string detail, Action retry) {
        int installed = TufService.Instance?.InstalledCount ?? 0;
        AddStatus(Tr("TUF_OFFLINE", "TUF could not be reached — you may be offline.") + "\n" + detail, false, null, 78f);
        RectTransform row = FixedRow("Offline Actions", 58f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = new RectOffset(0, 0, 6, 6);
        (Image switchChip, TMP_Text switchLabel) = Chip(row, "", 264f, () => {
            TufService.Instance?.ShowInstalledLevels();
            MenuFactory.SetState(Quartz.UI.Nav.NavRegistry.StateFor(TufModule.LevelsPageKey));
        });
        switchChip.color = installed > 0 ? UIColors.ObjectActive : UIColors.ObjectBG;
        switchLabel.text = installed > 0
            ? string.Format(Tr("TUF_OFFLINE_SWITCH", "Switch to Installed ({0})"), installed)
            : Tr("TUF_OFFLINE_SWITCH_EMPTY", "Switch to Installed");
        (Image _, TMP_Text retryLabel) = Chip(row, Tr("TUF_RETRY", "Retry"), 96f, retry);
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
    private static void AddHorizontal(Transform row, float spacing = 8f) {
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
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
    private static void SetFull(RectTransform rect, float padX, float padY) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new(padX, padY);
        rect.offsetMax = new(-padX, -padY);
    }
    private static string Tr(string key, string fallback) => MainCore.Tr.Get(key, fallback);
}
