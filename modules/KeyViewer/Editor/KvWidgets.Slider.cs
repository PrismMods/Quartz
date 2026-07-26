using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
namespace Quartz.UI.Editor;
internal static partial class KvWidgets {
    private const float MinFillFraction = 0.05f;
    internal static UISlider Slider(
        Transform parent,
        float defaultValue,
        float min,
        float max,
        float value,
        Func<float, float> filter,
        Action<float> onChanged,
        Action<float> onComplete,
        string text,
        string id,
        string labelKey = null
    ) {
        RectTransform rect = GenerateUI.BackGround(0f);
        rect.SetParent(parent, false);
        rect.gameObject.AddComponent<EventTrigger>();
        Image changedImg = GenerateUI.AddSmallChangedCircle(rect).GetComponent<Image>();
        GameObject fill = new("Fill");
        fill.transform.SetParent(rect, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        fillImg.type = Image.Type.Sliced;
        fill.AddComponent<Mask>().showMaskGraphic = true;
        Image changedUpImg = GenerateUI.AddSmallChangedCircle(fillRect).GetComponent<Image>();
        TextMeshProUGUI label = Caption(rect, text, id, ValueReserve, labelKey);
        TextMeshProUGUI valueText = GenerateUI.AddText(rect, true);
        valueText.fontSize = LabelSize;
        valueText.alignment = TextAlignmentOptions.Right;
        RectTransform valueRect = valueText.rectTransform;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = new Vector2(-Pad, 0f);
        GameObject outline = new("Outline");
        outline.transform.SetParent(rect, false);
        RectTransform outlineRect = outline.AddComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = Vector2.zero;
        outlineRect.offsetMax = Vector2.zero;
        Image outlineImg = outline.AddComponent<Image>();
        outlineImg.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        outlineImg.type = Image.Type.Sliced;
        outlineImg.color = new Color(1f, 1f, 1f, 0f);
        outlineImg.raycastTarget = false;
        TextMeshProUGUI previewLabel = GenerateUI.AddText(rect, true);
        previewLabel.fontSize = LabelSize;
        previewLabel.alignment = TextAlignmentOptions.Right;
        previewLabel.richText = true;
        previewLabel.raycastTarget = false;
        previewLabel.text = "";
        RectTransform previewRect = previewLabel.rectTransform;
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = new Vector2(-Pad, 0f);
        UISlider slider = new(
            id, rect, fillRect, fillImg, label, valueText,
            changedImg, changedUpImg, outlineImg, previewLabel,
            defaultValue, min, max, value, filter, onChanged, onComplete
        );
        slider.MinFill = MinFillFraction;
        float Apply(float v) => Mathf.Clamp(filter != null ? filter(v) : v, min, max);
        void SetFromMouse() {
            Vector2 local = rect.InverseTransformPoint(UnityEngine.Input.mousePosition);
            float width = rect.rect.width;
            if(width <= 0f) return;
            slider.Set(Apply(Mathf.Lerp(min, max, Mathf.Clamp01((local.x + (width * 0.5f)) / width))));
        }
        GenerateUI.AddButton(rect.gameObject, e => {
            switch(e) {
                case InputButton.Left:
                    SetFromMouse();
                    slider.OnComplete?.Invoke(slider.Value);
                    break;
                case InputButton.Middle:
                    if(!MainCore.Conf.MiddleClickToDefault) break;
                    slider.Set(Apply(defaultValue));
                    slider.OnComplete?.Invoke(slider.Value);
                    break;
            }
        }, true);
        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();
        bool dragging = false;
        UnityUtils.AddEvent(EventTriggerType.BeginDrag, _ => {
            if(!UnityEngine.Input.GetMouseButton(0)) return;
            dragging = true;
            SetFromMouse();
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, _ => {
            if(dragging && UnityEngine.Input.GetMouseButton(0)) SetFromMouse();
            else dragging = false;
        }, trigger);
        void EndDrag() {
            if(!dragging) return;
            dragging = false;
            slider.OnComplete?.Invoke(slider.Value);
        }
        UnityUtils.AddEvent(EventTriggerType.EndDrag, _ => EndDrag(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, _ => EndDrag(), trigger);
        GenerateUI.AddSliderValueEditor(slider, rect, valueText, () => Apply(defaultValue));
        slider.Set(Apply(value), false);
        return slider;
    }
}
