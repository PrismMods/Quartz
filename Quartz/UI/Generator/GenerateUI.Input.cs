using System.Globalization;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using GTweens.Tweens;
using GTweens.Easings;
using GTweens.Extensions;
using GTweens.Builders;
using Quartz.Tween;
using GTweenExtensions = GTweens.Extensions.GTweenExtensions;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Generator;
public static partial class GenerateUI {
    public static UIInput Input(
        Transform parent,
        string defaultValue,
        string value,
        Action<string> onChanged,
        string placeholder,
        Sprite icon,
        string id,
        float rightInset = 250f
    ) {
        RectTransform rect = BackGround(rightInset);
        rect.SetParent(parent, false);
        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();
        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(rect, false);
        RectTransform circleRect = iconObj.AddComponent<RectTransform>();
        circleRect.anchorMin = new(1f, 0.5f);
        circleRect.anchorMax = new(1f, 0.5f);
        circleRect.pivot = new(0.5f, 0.5f);
        circleRect.anchoredPosition = new(-23f, 0f);
        circleRect.sizeDelta = new(26f, 26f);
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.sprite = icon;
        iconImage.color = new Color(1f, 1f, 1f, 0.2f);
        GameObject inputObj = new("Input");
        inputObj.transform.SetParent(rect, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new(16f, 4f);
        inputRect.offsetMax = new(-12f, -4f);
        inputObj.AddComponent<RectMask2D>();
        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        var text = AddText(inputObj.transform);
        text.font = FontManager.Current;
        text.text = value ?? string.Empty;
        text.alignment = TextAlignmentOptions.Left;
        TextCompat.NoWrap(text);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var placeholderText = AddText(inputObj.transform);
        placeholderText.font = FontManager.Current;
        placeholderText.text = placeholder;
        placeholderText.alignment = TextAlignmentOptions.Left;
        TextCompat.NoWrap(placeholderText);
        placeholderText.color = new Color(1, 1, 1, 0.2f);
        LocalizeById(placeholderText, id, placeholder);
        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        inputField.textViewport = inputRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.richText = false;
        var input = new UIInput(
            id,
            rect,
            inputField,
            placeholderText,
            iconImage,
            changeImg,
            defaultValue,
            value,
            onChanged
        );
        AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Middle:
                    if(MainCore.Conf.MiddleClickToDefault && input.Value != input.DefaultValue)
                        input.Reset();
                    break;
            }
        });
        return input;
    }
}
