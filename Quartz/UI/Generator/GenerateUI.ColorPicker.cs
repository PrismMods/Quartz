using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using TMPro;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    private const float PickerWheelSize = 280f;
    private const float PickerVerticalWidth = 360f;
    private const float PickerVerticalHeight = 586f;
    private const float PickerHorizontalWidth = 640f;
    private const float PickerHorizontalHeight = 344f;
    public static UIColorPicker ColorPicker(
        Transform parent,
        Color defaultValue,
        Color value,
        Action<Color> onChanged,
        Action<Color> onComplete,
        string text,
        string id,
        bool showAlpha = true,
        float rightInset = 250f
    ) {
        bool horizontal = MainCore.Conf.ColorPickerHorizontal;
        int channelCount = showAlpha ? 4 : 3;
        GameObject rootObject = new("ColorPicker");
        rootObject.transform.SetParent(parent, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        RectTransform header = BackGround(rightInset);
        header.name = "Header";
        header.SetParent(root, false);
        // O5Kit field: swatch and label on the left half, hex on the right.
        GameObject previewObject = new("Preview");
        previewObject.transform.SetParent(header, false);
        RectTransform previewRect = previewObject.AddComponent<RectTransform>();
        previewRect.anchorMin = new(0f, 0f);
        previewRect.anchorMax = new(0.5f, 1f);
        previewRect.offsetMin = new(9f, 8f);
        previewRect.offsetMax = new(-5f, -8f);
        Image preview = previewObject.AddComponent<Image>();
        preview.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        preview.type = Image.Type.Sliced;
        TextMeshProUGUI previewLabel = AddText(previewRect, true);
        previewLabel.name = "ColorLabel";
        previewLabel.text = text;
        previewLabel.fontSize = 18f;
        previewLabel.alignment = TextAlignmentOptions.Center;
        previewLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
        previewLabel.raycastTarget = false;
        previewLabel.rectTransform.offsetMin = new(8f, 0f);
        previewLabel.rectTransform.offsetMax = new(-8f, 0f);
        LocalizeById(previewLabel, id, text);
        FitLine(previewLabel, 12f);
        TextMeshProUGUI label = previewLabel;
        UIColorPicker picker = null;
        UIInput hexInput = Input(
            header,
            ColorUtility.ToHtmlStringRGBA(defaultValue),
            ColorUtility.ToHtmlStringRGBA(value),
            hex => picker?.ValidateHex(hex),
            string.Empty,
            null,
            id + "_hex",
            rightInset
        );
        hexInput.OnComplete = hex => picker?.CompleteHex(hex);
        RectTransform hexRect = hexInput.Rect;
        hexRect.name = "HexInput";
        hexInput.ChangedImage.gameObject.SetActive(false);
        if(hexInput.IconImage != null) hexInput.IconImage.gameObject.SetActive(false);
        if(hexInput.InputField.textViewport is RectTransform hexViewport) {
            hexViewport.offsetMin = new(6f, hexViewport.offsetMin.y);
            hexViewport.offsetMax = new(-6f, hexViewport.offsetMax.y);
        }
        hexRect.anchorMin = new(0.62f, 0f);
        hexRect.anchorMax = new(1f, 1f);
        hexRect.offsetMin = new(0f, 4f);
        hexRect.offsetMax = new(-10f, -4f);
        hexRect.GetComponent<Image>().color = Color.clear;
        TMP_Text hexText = hexInput.InputField.textComponent;
        hexText.fontSize = 19f;
        hexText.alignment = TextAlignmentOptions.Right;
        hexInput.InputField.onFocusSelectAll = false;
        hexInput.InputField.characterLimit = 9;
        Transform hexHover = hexRect.Find("Hover");
        if(hexHover != null) hexHover.gameObject.SetActive(false);
        GameObject bodyObject = new("Body");
        bodyObject.transform.SetParent(root, false);
        RectTransform body = bodyObject.AddComponent<RectTransform>();
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.pivot = new(0.5f, 0.5f);
        Image bodyBackground = bodyObject.AddComponent<Image>();
        bodyBackground.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bodyBackground.type = Image.Type.Sliced;
        bodyBackground.color = UIColors.PanelBG;
        CanvasGroup bodyCanvas = bodyObject.AddComponent<CanvasGroup>();
        RectTransform wheelParent;
        RectTransform controlParent;
        if(horizontal) {
            HorizontalLayoutGroup split = bodyObject.AddComponent<HorizontalLayoutGroup>();
            split.padding = new RectOffset { left = 14, right = 14, top = 12, bottom = 12 };
            split.spacing = 14f;
            split.childControlWidth = true;
            split.childControlHeight = true;
            split.childForceExpandWidth = true;
            split.childForceExpandHeight = true;
            GameObject wheelColumn = new("WheelColumn");
            wheelColumn.transform.SetParent(body, false);
            wheelParent = wheelColumn.AddComponent<RectTransform>();
            LayoutElement wheelColumnLayout = wheelColumn.AddComponent<LayoutElement>();
            wheelColumnLayout.preferredWidth = PickerWheelSize;
            wheelColumnLayout.minWidth = PickerWheelSize;
            wheelColumnLayout.flexibleWidth = 0f;
            GameObject controlColumn = new("ControlColumn");
            controlColumn.transform.SetParent(body, false);
            controlParent = controlColumn.AddComponent<RectTransform>();
            VerticalLayoutGroup controlLayout = controlColumn.AddComponent<VerticalLayoutGroup>();
            controlLayout.spacing = 6f;
            controlLayout.childControlWidth = true;
            controlLayout.childControlHeight = true;
            controlLayout.childForceExpandWidth = true;
            controlLayout.childForceExpandHeight = false;
            controlLayout.childAlignment = TextAnchor.MiddleCenter;
        } else {
            VerticalLayoutGroup stack = bodyObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset { left = 14, right = 14, top = 12, bottom = 12 };
            stack.spacing = 6f;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            wheelParent = body;
            controlParent = body;
        }
        GameObject wheelObject = new("Wheel");
        wheelObject.transform.SetParent(wheelParent, false);
        RectTransform wheel = wheelObject.AddComponent<RectTransform>();
        if(horizontal) {
            wheel.anchorMin = Vector2.zero;
            wheel.anchorMax = Vector2.one;
            wheel.offsetMin = Vector2.zero;
            wheel.offsetMax = Vector2.zero;
        } else {
            LayoutElement wheelLayout = wheelObject.AddComponent<LayoutElement>();
            wheelLayout.preferredHeight = PickerWheelSize;
            wheelLayout.minHeight = PickerWheelSize;
        }
        Image wheelImage = wheelObject.AddComponent<Image>();
        wheelImage.preserveAspect = true;
        wheelImage.color = Color.white;
        RectTransform hueHandle = CreateHandle(wheel, "HueHandle", new(15f, 15f));
        RectTransform colorHandle = CreateHandle(wheel, "ColorHandle", new(13f, 13f));
        RectTransform modeRow = Row(controlParent, 30f);
        HorizontalLayoutGroup modeLayout = modeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        modeLayout.spacing = 4f;
        modeLayout.childControlWidth = true;
        modeLayout.childControlHeight = true;
        modeLayout.childForceExpandWidth = true;
        modeLayout.childForceExpandHeight = true;
        var (rgbModeBackground, rgbModeLabel) = CreateModeButton(modeRow, "RGB", () => picker?.SetMode(false));
        var (hsvModeBackground, hsvModeLabel) = CreateModeButton(modeRow, "HSV", () => picker?.SetMode(true));
        UISlider[] sliders = new UISlider[channelCount];
        string[] names = ["R", "G", "B", "A"];
        Color[] colors = [
            new(1f, 0.42f, 0.44f, 1f),
            new(0.48f, 0.82f, 0.48f, 1f),
            new(0.56f, 0.56f, 0.9f, 1f),
            new(0.45f, 0.45f, 0.45f, 1f)
        ];
        for(int i = 0; i < sliders.Length; i++) {
            int channel = i;
            RectTransform row = Row(controlParent, 36f);
            sliders[i] = Slider(
                row, defaultValue[i], 0f, 1f, value[i], null,
                next => picker?.SetChannel(channel, next),
                _ => { if(picker != null) onComplete?.Invoke(picker.Value); },
                names[i], id + "_" + names[i].ToLowerInvariant(), 0f
            );
            sliders[i].Format = "F2";
            sliders[i].Rect.offsetMax = Vector2.zero;
            // Channel colours are fixed: keep them through expression commits and accent repaints.
            sliders[i].SetAccent(colors[i]);
            sliders[i].FillImage.gameObject.AddComponent<ThemeExempt>();
            sliders[i].Label.fontSize = 18f;
            sliders[i].Label.fontSizeMax = 18f;
        }
        Image sharedOutline = AddOutlineHover(header.gameObject, header.gameObject.AddComponent<EventTrigger>());
        picker = new UIColorPicker(
            id, root, bodyObject, bodyCanvas, preview, previewLabel, label,
            wheel, hueHandle, colorHandle, hexInput, sharedOutline, sliders,
            rgbModeBackground, rgbModeLabel, hsvModeBackground, hsvModeLabel,
            defaultValue, value, onChanged, onComplete,
            horizontal ? PickerHorizontalWidth : PickerVerticalWidth,
            horizontal ? PickerHorizontalHeight : PickerVerticalHeight
        );
        EventTrigger wheelTrigger = wheelObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerDown, picker.BeginPointer, wheelTrigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, picker.DragPointer, wheelTrigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, picker.EndPointer, wheelTrigger);
        UnityUtils.AddEvent(EventTriggerType.EndDrag, picker.EndPointer, wheelTrigger);
        GameObject toggleArea = new("ToggleArea");
        toggleArea.transform.SetParent(header, false);
        RectTransform toggleRect = toggleArea.AddComponent<RectTransform>();
        toggleRect.anchorMin = Vector2.zero;
        toggleRect.anchorMax = new(0.6f, 1f);
        toggleRect.offsetMin = Vector2.zero;
        toggleRect.offsetMax = Vector2.zero;
        Image toggleTarget = toggleArea.AddComponent<Image>();
        toggleTarget.color = Color.clear;
        AddButton(toggleArea, button => {
            switch(button) {
                case InputButton.Left:
                    picker.ToggleExpanded();
                    break;
                case InputButton.Middle:
                    if(MainCore.Conf.MiddleClickToDefault) picker.Reset();
                    break;
            }
        }, false);
        return picker;
    }
    private static (Image Background, TextMeshProUGUI Label) CreateModeButton(
        Transform parent,
        string text,
        Action onClick
    ) {
        GameObject buttonObject = new(text);
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        Image background = buttonObject.AddComponent<Image>();
        background.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        background.type = Image.Type.Sliced;
        TextMeshProUGUI label = AddText(rect, true);
        label.text = text;
        label.fontSize = 15f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        AddButton(buttonObject, button => {
            if(button == InputButton.Left) onClick();
        });
        return (background, label);
    }
    private static RectTransform CreateHandle(RectTransform parent, string name, Vector2 size) {
        GameObject handleObject = new(name);
        handleObject.transform.SetParent(parent, false);
        RectTransform rect = handleObject.AddComponent<RectTransform>();
        rect.anchorMin = new(0.5f, 0.5f);
        rect.anchorMax = new(0.5f, 0.5f);
        rect.pivot = new(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = handleObject.AddComponent<Image>();
        image.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
        return rect;
    }
}
