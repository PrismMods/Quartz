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
    public static UIButton Button(
        Transform parent,
        Action onClick,
        string text,
        string id,
        float rightInset = 250f
    ) {
        RectTransform rect = BackGround(rightInset);
        rect.SetParent(parent, false);
        TextMeshProUGUI tmp = AddText(rect, true);
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        LocalizeById(tmp, id, text);
        Image bg = rect.GetComponent<Image>();
        bg.color = UIColors.ObjectButton;
        UIButton button = new(
            id,
            rect,
            tmp,
            bg,
            onClick
        );
        AddButton(rect.gameObject, btn => {
            if(btn == InputButton.Left) button.Click();
        }, false);
        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, e => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, e => button.OnHoverExit(), trigger);
        return button;
    }
    public static void AddButton(GameObject obj, Action<InputButton> onClick, bool outline = true) {
        EventTrigger trigger = obj.AddComponent<EventTrigger>();
        AddClick(trigger, onClick);
        if(outline) {
            AddOutlineHover(obj, trigger);
        }
    }
    private static void AddClick(EventTrigger trigger, Action<InputButton> onClick)
        => UnityUtils.AddClickEvent(trigger, e => onClick?.Invoke(e.button));
    internal static Image AddOutlineHover(GameObject obj, EventTrigger trigger) {
        GTween hoverSeq = null;
        GameObject hover = new("Hover");
        hover.transform.SetParent(obj.transform, false);
        hover.transform.SetAsFirstSibling();
        RectTransform hoverRect = hover.AddComponent<RectTransform>();
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.pivot = new Vector2(0.5f, 0.5f);
        hoverRect.offsetMin = Vector2.zero;
        hoverRect.offsetMax = Vector2.zero;
        hover.AddComponent<LayoutElement>().ignoreLayout = true;
        Image hoverImage = hover.AddComponent<Image>();
        hoverImage.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        hoverImage.type = Image.Type.Sliced;
        Color baseColor = UIColors.ObjectActive;
        hoverImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        void FadeOutline(float target) {
            hoverSeq?.Kill();
            hoverSeq = hoverImage.GTAlpha(target, 0.1f).SetEasing(Easing.OutSine);
            MainCore.TC.Play(hoverSeq);
        }
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, e => FadeOutline(1f), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, e => FadeOutline(0f), trigger);
        return hoverImage;
    }
    public static void FixWidth(UIButton button, float width) {
        LayoutElement le = button.Rect.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        le.flexibleWidth = 0f;
    }
    public static void MiniButton(Transform parent, string text, string key, float rightOffset, float width, Action onClick) {
        GameObject obj = new("MiniBtn_" + text);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(rightOffset, 0f);
        rect.sizeDelta = new Vector2(width, 36f);
        Image img = obj.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        img.type = Image.Type.Sliced;
        img.color = UIColors.ObjectButton;
        var label = AddText(obj.transform, true);
        if(string.IsNullOrEmpty(key)) label.text = text;
        else Localize(label, key, text);
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        AddButton(obj, btn => {
            if(btn == InputButton.Left) onClick();
        });
    }
    public static Action<T> SegmentedControl<T>(
        Transform row,
        IReadOnlyList<T> values,
        Func<T, string> display,
        Func<T, string> localeKey,
        T value,
        Action<T> onChanged
    ) {
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if(layout == null) {
            layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 250, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }
        var options = new List<(T value, Image bg, TextMeshProUGUI label)>();
        T current = value;
        void Refresh() {
            foreach((T optValue, Image bg, TextMeshProUGUI label) in options) {
                bool selected = EqualityComparer<T>.Default.Equals(optValue, current);
                bg.color = selected ? UIColors.ObjectActive : UIColors.ObjectBG;
                label.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            }
        }
        foreach(T optValue in values) {
            string text = display(optValue);
            GameObject obj = new("Segment_" + text.Replace(" ", ""));
            obj.transform.SetParent(row, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 50f;
            le.preferredHeight = 50f;
            Image bg = obj.AddComponent<Image>();
            bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
            bg.type = Image.Type.Sliced;
            TextMeshProUGUI label = AddText(obj.transform, true);
            if(localeKey != null) Localize(label, localeKey(optValue), text);
            else label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 22f;
            T captured = optValue;
            AddButton(obj, btn => {
                if(btn != InputButton.Left) return;
                if(EqualityComparer<T>.Default.Equals(current, captured)) return;
                current = captured;
                Refresh();
                onChanged?.Invoke(captured);
            });
            options.Add((optValue, bg, label));
        }
        Refresh();
        return v => {
            current = v;
            Refresh();
        };
    }
}
