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
    public static UIToggle Toggle(
        Transform parent,
        bool defaultValue,
        bool value,
        Action<bool> onChanged,
        string text,
        string id,
        float rightInset = 250f
    ) {
        RectTransform rect = BackGround(rightInset);
        rect.SetParent(parent, false);
        TextMeshProUGUI tmp = AddText(rect);
        tmp.text = text;
        LocalizeById(tmp, id, text);
        GameObject change = AddSmallChangedCircle(rect);
        Image changeImg = change.GetComponent<Image>();
        GameObject toggleCircle = new("ToggleCircle");
        toggleCircle.transform.SetParent(rect, false);
        RectTransform circleRect = toggleCircle.AddComponent<RectTransform>();
        circleRect.anchorMin = new(1f, 0.5f);
        circleRect.anchorMax = new(1f, 0.5f);
        circleRect.pivot = new(0.5f, 0.5f);
        circleRect.anchoredPosition = new(-23f, 0f);
        circleRect.sizeDelta = new(26f, 26f);
        Image circleImage = toggleCircle.AddComponent<Image>();
        UIToggle toggle = new(
            id,
            rect,
            tmp,
            circleImage,
            circleRect,
            changeImg,
            defaultValue,
            value,
            onChanged
        );
        KeyCapture bind = AttachToggleBind(rect, toggle, id);
        AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Left:
                    toggle.Toggle();
                    break;
                case InputButton.Middle:
                    if(MainCore.Conf.MiddleClickToDefault && toggle.Value != toggle.DefaultValue)
                        toggle.Reset();
                    break;
                case InputButton.Right:
                    bind?.Begin();
                    break;
            }
        });
        return toggle;
    }
    public static UIToggle ToggleTip(
        Transform parent,
        bool defaultValue,
        bool value,
        Action<bool> onChanged,
        string label,
        string id,
        string tooltip
    ) {
        UIToggle toggle = Toggle(Row(parent), defaultValue, value, onChanged, label, id);
        toggle.Rect.AddToolTip("DESC_" + id.ToUpperInvariant(), tooltip);
        return toggle;
    }
}
